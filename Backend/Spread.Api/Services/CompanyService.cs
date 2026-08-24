using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Spread.Api.Domain.Activity;
using Spread.Api.Domain.Assets;
using Spread.Api.Domain.Companies;
using Spread.Api.Domain.Financials;
using Spread.Api.Domain.MarketData;
using Spread.Api.Providers;
using Spread.Api.Providers.Insiders;
using Spread.Api.Infrastructure.Observability;
using System.Diagnostics;

namespace Spread.Api.Services;

public sealed class CompanyService(
    IFinancialDataProvider provider,
    IInsiderTransactionProvider insiderProvider,
    IMemoryCache cache,
    ILogger<CompanyService> logger) : ICompanyService
{
    private static readonly MemoryCache SearchCache = new(new MemoryCacheOptions
    {
        SizeLimit = 500,
        CompactionPercentage = 0.2
    });
    private static readonly SemaphoreSlim SearchProviderConcurrency = new(2, 2);
    private static readonly SemaphoreSlim HistoricalProviderConcurrency = new(1, 1);
    private static readonly Action<ILogger, string, Exception?> LogCacheHit =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(1001, nameof(LogCacheHit)),
            "Company profile cache hit for {Ticker}");

    private static readonly Action<ILogger, string, Exception?> LogProviderFetch =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1002, nameof(LogProviderFetch)),
            "Fetching company profile for {Ticker} from financial provider");

    private static readonly Action<ILogger, string, Exception?> LogFinancialsCacheHit =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(1003, nameof(LogFinancialsCacheHit)),
            "Company financials cache hit for {Ticker}");

    private static readonly Action<ILogger, string, Exception?> LogFinancialsProviderFetch =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1004, nameof(LogFinancialsProviderFetch)),
            "Fetching company financials for {Ticker} from financial provider");

    private static readonly Action<ILogger, string, Exception?> LogActivityCacheHit =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(1005, nameof(LogActivityCacheHit)),
            "Company market activity cache hit for {Ticker}");

    private static readonly Action<ILogger, string, Exception?> LogActivityProviderFetch =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1006, nameof(LogActivityProviderFetch)),
            "Fetching company market activity for {Ticker} from financial provider");

    private static readonly Action<ILogger, string, FinancialDataProviderFailure, Exception?> LogInsiderFallback =
        LoggerMessage.Define<string, FinancialDataProviderFailure>(
            LogLevel.Warning,
            new EventId(1007, nameof(LogInsiderFallback)),
            "Specialized insider provider failed for {Ticker} with category {Failure}; using FMP fallback");

    private static readonly Action<ILogger, int, int, Exception?> LogSearchCompleted =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(1008, nameof(LogSearchCompleted)),
            "Company search completed with query length {QueryLength} and {ResultCount} results");

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CacheLocks = new();
    private static readonly TimeSpan ProfileCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan FinancialsCacheDuration = TimeSpan.FromHours(12);
    private static readonly TimeSpan ActivityCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan EmptySearchCacheDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MissingProfileCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan HistoricalPriceCacheDuration = TimeSpan.FromHours(6);

    public async Task<HistoricalPriceSeries?> GetPriceHistoryAsync(
        AssetIdentifier asset,
        HistoricalPriceRange range,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var cacheKey = $"fmp:history:{asset.Ticker}:{range}:v1";
        if (cache.TryGetValue(cacheKey, out HistoricalPriceSeries? cached))
        {
            return cached;
        }

        var cacheLock = CacheLocks.GetOrAdd(cacheKey, static _ => new SemaphoreSlim(1, 1));
        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var from = range switch
            {
                HistoricalPriceRange.YearToDate => new DateOnly(today.Year, 1, 1),
                HistoricalPriceRange.OneYear => today.AddYears(-1),
                HistoricalPriceRange.ThreeYears => today.AddYears(-3),
                HistoricalPriceRange.FiveYears => today.AddYears(-5),
                HistoricalPriceRange.Maximum => today.AddYears(-25),
                _ => throw new ArgumentOutOfRangeException(nameof(range))
            };
            await HistoricalProviderConcurrency.WaitAsync(cancellationToken);
            IReadOnlyList<HistoricalPricePoint> points;
            try
            {
                try
                {
                    points = await provider.GetHistoricalPricesAsync(asset, from, today, cancellationToken);
                }
                catch (FinancialDataProviderException exception) when (exception.Failure == FinancialDataProviderFailure.RateLimited)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    points = await provider.GetHistoricalPricesAsync(asset, from, today, cancellationToken);
                }
            }
            finally
            {
                HistoricalProviderConcurrency.Release();
            }
            if (points.Count == 0)
            {
                return null;
            }

            var series = new HistoricalPriceSeries(asset.Ticker, range, points, DateTimeOffset.UtcNow, "FMP");
            cache.Set(cacheKey, series, HistoricalPriceCacheDuration);
            return series;
        }
        finally
        {
            cacheLock.Release();
            if (cacheLock.CurrentCount == 1)
            {
                CacheLocks.TryRemove(new KeyValuePair<string, SemaphoreSlim>(cacheKey, cacheLock));
            }
        }
    }

    public async Task<IReadOnlyList<CompanySearchResult>> SearchAsync(
        CompanySearchQuery query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (limit is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        CompanySearchMetrics.RecordRequest();
        var cacheKey = $"search:{query.Value.ToUpperInvariant()}:{limit}";
        if (SearchCache.TryGetValue(cacheKey, out IReadOnlyList<CompanySearchResult>? cached))
        {
            CompanySearchMetrics.RecordCacheHit();
            return cached!;
        }

        var cacheLock = CacheLocks.GetOrAdd(cacheKey, static _ => new SemaphoreSlim(1, 1));
        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (SearchCache.TryGetValue(cacheKey, out cached))
            {
                CompanySearchMetrics.RecordCacheHit();
                return cached!;
            }

            await SearchProviderConcurrency.WaitAsync(cancellationToken);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                CompanySearchMetrics.RecordProviderCall();
                cached = await provider.SearchCompaniesAsync(query, limit, cancellationToken);
            }
            finally
            {
                stopwatch.Stop();
                CompanySearchMetrics.RecordProviderDuration(stopwatch.Elapsed);
                SearchProviderConcurrency.Release();
            }

            SearchCache.Set(
                cacheKey,
                cached,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = cached.Count == 0
                        ? EmptySearchCacheDuration
                        : SearchCacheDuration,
                    Size = 1
                });
            LogSearchCompleted(logger, query.Value.Length, cached.Count, null);
            return cached;
        }
        finally
        {
            cacheLock.Release();
            if (cacheLock.CurrentCount == 1)
            {
                CacheLocks.TryRemove(new KeyValuePair<string, SemaphoreSlim>(cacheKey, cacheLock));
            }
        }
    }

    public async Task<CompanyProfile?> GetProfileAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var cacheKey = $"fmp:profile:{asset.Ticker}";

        if (cache.TryGetValue(cacheKey, out ProfileCacheEntry? cachedProfile))
        {
            LogCacheHit(logger, asset.Ticker, null);
            return cachedProfile!.Profile;
        }

        var cacheLock = CacheLocks.GetOrAdd(cacheKey, static _ => new SemaphoreSlim(1, 1));
        await cacheLock.WaitAsync(cancellationToken);

        try
        {
            if (cache.TryGetValue(cacheKey, out cachedProfile))
            {
                return cachedProfile!.Profile;
            }

            LogProviderFetch(logger, asset.Ticker, null);
            var profile = await provider.GetCompanyProfileAsync(asset, cancellationToken);

            cache.Set(
                cacheKey,
                new ProfileCacheEntry(profile),
                profile is null ? MissingProfileCacheDuration : ProfileCacheDuration);

            return profile;
        }
        finally
        {
            cacheLock.Release();
            if (cacheLock.CurrentCount == 1)
            {
                CacheLocks.TryRemove(new KeyValuePair<string, SemaphoreSlim>(cacheKey, cacheLock));
            }
        }
    }

    private sealed record ProfileCacheEntry(CompanyProfile? Profile);

    public async Task<CompanyFinancials?> GetFinancialsAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var cacheKey = $"fmp:financials:{asset.Ticker}:annual:5";

        if (cache.TryGetValue(cacheKey, out CompanyFinancials? cachedFinancials))
        {
            LogFinancialsCacheHit(logger, asset.Ticker, null);
            return cachedFinancials;
        }

        var cacheLock = CacheLocks.GetOrAdd(cacheKey, static _ => new SemaphoreSlim(1, 1));
        await cacheLock.WaitAsync(cancellationToken);

        try
        {
            if (cache.TryGetValue(cacheKey, out cachedFinancials))
            {
                return cachedFinancials;
            }

            LogFinancialsProviderFetch(logger, asset.Ticker, null);
            var financials = await provider.GetCompanyFinancialsAsync(asset, cancellationToken);

            if (financials is not null)
            {
                cache.Set(cacheKey, financials, FinancialsCacheDuration);
            }

            return financials;
        }
        finally
        {
            cacheLock.Release();
            if (cacheLock.CurrentCount == 1)
            {
                CacheLocks.TryRemove(new KeyValuePair<string, SemaphoreSlim>(cacheKey, cacheLock));
            }
        }
    }

    public async Task<CompanyMarketActivity?> GetMarketActivityAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var cacheKey = $"providers:activity:{asset.Ticker}:v2";

        if (cache.TryGetValue(cacheKey, out CompanyMarketActivity? cachedActivity))
        {
            LogActivityCacheHit(logger, asset.Ticker, null);
            return cachedActivity;
        }

        var cacheLock = CacheLocks.GetOrAdd(cacheKey, static _ => new SemaphoreSlim(1, 1));
        await cacheLock.WaitAsync(cancellationToken);

        try
        {
            if (cache.TryGetValue(cacheKey, out cachedActivity))
            {
                return cachedActivity;
            }

            LogActivityProviderFetch(logger, asset.Ticker, null);
            var activity = await GetCombinedMarketActivityAsync(asset, cancellationToken);

            if (activity is not null)
            {
                cache.Set(cacheKey, activity, ActivityCacheDuration);
            }

            return activity;
        }
        finally
        {
            cacheLock.Release();
            if (cacheLock.CurrentCount == 1)
            {
                CacheLocks.TryRemove(new KeyValuePair<string, SemaphoreSlim>(cacheKey, cacheLock));
            }
        }
    }

    private async Task<CompanyMarketActivity?> GetCombinedMarketActivityAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken)
    {
        var fmpTask = provider.GetCompanyMarketActivityAsync(asset, cancellationToken);
        var specializedTask = insiderProvider.GetInsiderTransactionsAsync(asset, cancellationToken);
        CompanyMarketActivity? fmpActivity = null;
        FinancialDataProviderException? fmpFailure = null;
        try
        {
            fmpActivity = await fmpTask;
        }
        catch (FinancialDataProviderException exception)
        {
            fmpFailure = exception;
        }

        InsiderTransactionSnapshot? specializedInsiders = null;
        try
        {
            specializedInsiders = await specializedTask;
        }
        catch (FinancialDataProviderException exception)
        {
            LogInsiderFallback(logger, asset.Ticker, exception.Failure, null);
        }

        if (fmpActivity is null && specializedInsiders is null)
        {
            if (fmpFailure is not null)
            {
                throw fmpFailure;
            }

            return null;
        }

        var providerNames = new[] { specializedInsiders?.Provider, fmpActivity?.Provider }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var fetchedAt = new[] { specializedInsiders?.FetchedAt, fmpActivity?.FetchedAt }
            .Where(value => value.HasValue)
            .Max()!.Value;

        return new CompanyMarketActivity(
            asset.Ticker,
            specializedInsiders?.Transactions ?? fmpActivity?.InsiderTransactions ?? [],
            fmpActivity?.Dividends ?? [],
            specializedInsiders is not null || fmpActivity?.InsiderDataAvailable == true,
            fmpActivity?.DividendDataAvailable == true,
            fetchedAt,
            string.Join("+", providerNames));
    }
}
