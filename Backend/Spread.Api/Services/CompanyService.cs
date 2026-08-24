using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Spread.Api.Domain.Activity;
using Spread.Api.Domain.Assets;
using Spread.Api.Domain.Companies;
using Spread.Api.Domain.Financials;
using Spread.Api.Providers;

namespace Spread.Api.Services;

public sealed class CompanyService(
    IFinancialDataProvider provider,
    IMemoryCache cache,
    ILogger<CompanyService> logger) : ICompanyService
{
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

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CacheLocks = new();
    private static readonly TimeSpan ProfileCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan FinancialsCacheDuration = TimeSpan.FromHours(12);
    private static readonly TimeSpan ActivityCacheDuration = TimeSpan.FromHours(1);

    public async Task<CompanyProfile?> GetProfileAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var cacheKey = $"fmp:profile:{asset.Ticker}";

        if (cache.TryGetValue(cacheKey, out CompanyProfile? cachedProfile))
        {
            LogCacheHit(logger, asset.Ticker, null);
            return cachedProfile;
        }

        var cacheLock = CacheLocks.GetOrAdd(cacheKey, static _ => new SemaphoreSlim(1, 1));
        await cacheLock.WaitAsync(cancellationToken);

        try
        {
            if (cache.TryGetValue(cacheKey, out cachedProfile))
            {
                return cachedProfile;
            }

            LogProviderFetch(logger, asset.Ticker, null);
            var profile = await provider.GetCompanyProfileAsync(asset, cancellationToken);

            if (profile is not null)
            {
                cache.Set(cacheKey, profile, ProfileCacheDuration);
            }

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
        var cacheKey = $"fmp:activity:{asset.Ticker}:latest";

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
            var activity = await provider.GetCompanyMarketActivityAsync(asset, cancellationToken);

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
}
