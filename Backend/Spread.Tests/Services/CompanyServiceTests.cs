using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Spread.Api.Domain.Assets;
using Spread.Api.Domain.Activity;
using Spread.Api.Domain.Companies;
using Spread.Api.Domain.Financials;
using Spread.Api.Domain.MarketData;
using Spread.Api.Providers;
using Spread.Api.Providers.Insiders;
using Spread.Api.Services;

namespace Spread.Tests.Services;

public sealed class CompanyServiceTests
{
    [Fact]
    public async Task GetProfileAsync_CachesRepeatedRequests()
    {
        var provider = new CountingProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(provider, cache);
        var asset = new AssetIdentifier("AAPL");

        var first = await service.GetProfileAsync(asset);
        var second = await service.GetProfileAsync(asset);

        Assert.Same(first, second);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task GetProfileAsync_CollapsesConcurrentRequestsForSameTicker()
    {
        var provider = new CountingProvider(delay: TimeSpan.FromMilliseconds(50));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(provider, cache);
        var asset = new AssetIdentifier("NVDA");

        var results = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => service.GetProfileAsync(asset)));

        Assert.All(results, Assert.NotNull);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task SearchAsync_CachesRepeatedQueriesAndCollapsesProviderCalls()
    {
        var provider = new CountingProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(provider, cache);
        Assert.True(CompanySearchQuery.TryCreate("ServiceNow", out var query));

        var first = await service.SearchAsync(query!, 6);
        var second = await service.SearchAsync(query!, 6);

        Assert.Equal(first, second);
        Assert.Equal(1, provider.SearchCallCount);
    }

    [Fact]
    public async Task GetFinancialsAsync_CachesAnnualSnapshot()
    {
        var provider = new CountingProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(provider, cache);
        var asset = new AssetIdentifier("AAPL");

        var first = await service.GetFinancialsAsync(asset);
        var second = await service.GetFinancialsAsync(asset);

        Assert.Same(first, second);
        Assert.Equal(1, provider.FinancialCallCount);
    }

    [Fact]
    public async Task GetMarketActivityAsync_CachesRecentActivity()
    {
        var provider = new CountingProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var insiders = new CountingInsiderProvider();
        var service = new CompanyService(provider, insiders, cache, NullLogger<CompanyService>.Instance);
        var asset = new AssetIdentifier("AAPL");

        var first = await service.GetMarketActivityAsync(asset);
        var second = await service.GetMarketActivityAsync(asset);

        Assert.Same(first, second);
        Assert.Equal(1, provider.ActivityCallCount);
        Assert.Equal(1, insiders.CallCount);
    }

    [Fact]
    public async Task GetMarketActivityAsync_PrefersSpecializedInsidersAndKeepsFmpDividends()
    {
        var provider = new CountingProvider();
        var insiders = new CountingInsiderProvider(includeTransaction: true);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CompanyService(provider, insiders, cache, NullLogger<CompanyService>.Instance);

        var result = await service.GetMarketActivityAsync(new AssetIdentifier("AAPL"));

        Assert.NotNull(result);
        var transaction = Assert.Single(result.InsiderTransactions);
        Assert.Equal("Alpha Executive", transaction.ReportingName);
        Assert.Single(result.Dividends);
        Assert.Equal("AlphaVantage+Test", result.Provider);
    }

    [Fact]
    public async Task GetPriceHistoryAsync_CachesRangeSpecificSeries()
    {
        var provider = new CountingProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(provider, cache);

        var first = await service.GetPriceHistoryAsync(new AssetIdentifier("AAPL"), HistoricalPriceRange.OneYear);
        var second = await service.GetPriceHistoryAsync(new AssetIdentifier("AAPL"), HistoricalPriceRange.OneYear);

        Assert.Same(first, second);
        Assert.Equal(1, provider.HistoryCallCount);
        Assert.Equal(HistoricalPriceRange.OneYear, first!.Range);
    }

    private static CompanyService CreateService(
        IFinancialDataProvider provider,
        IMemoryCache cache)
        => new(provider, new CountingInsiderProvider(), cache, NullLogger<CompanyService>.Instance);

    private sealed class CountingProvider(TimeSpan? delay = null) : IFinancialDataProvider
    {
        private int _callCount;
        private int _financialCallCount;
        private int _activityCallCount;
        private int _searchCallCount;
        private int _historyCallCount;

        public int CallCount => _callCount;

        public int FinancialCallCount => _financialCallCount;

        public int ActivityCallCount => _activityCallCount;

        public int SearchCallCount => _searchCallCount;

        public int HistoryCallCount => _historyCallCount;

        public Task<IReadOnlyList<HistoricalPricePoint>> GetHistoricalPricesAsync(
            AssetIdentifier asset,
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _historyCallCount);
            IReadOnlyList<HistoricalPricePoint> points =
                [new(startDate, 100m), new(endDate, 110m)];
            return Task.FromResult(points);
        }

        public Task<IReadOnlyList<CompanySearchResult>> SearchCompaniesAsync(
            CompanySearchQuery query,
            int limit,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _searchCallCount);
            IReadOnlyList<CompanySearchResult> results =
                [new("NOW", "ServiceNow, Inc.", "NYSE", "USD", "Test")];
            return Task.FromResult(results);
        }

        public async Task<CompanyProfile?> GetCompanyProfileAsync(
            AssetIdentifier asset,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            if (delay.HasValue)
            {
                await Task.Delay(delay.Value, cancellationToken);
            }

            return new CompanyProfile(
                asset.Ticker,
                $"{asset.Ticker} Corporation",
                AssetType.StandardCompany,
                "Technology",
                "Software",
                "NASDAQ",
                "USD",
                "US",
                100_000_000m,
                1.1m,
                true,
                null,
                "https://example.test/logo.png",
                DateTimeOffset.UtcNow,
                "Test");
        }

        public Task<CompanyFinancials?> GetCompanyFinancialsAsync(
            AssetIdentifier asset,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _financialCallCount);
            CompanyFinancials result = new(
                asset.Ticker,
                [
                    new FinancialPeriod(
                        new DateOnly(2025, 12, 31),
                        "2025",
                        "FY",
                        new DateOnly(2026, 2, 1),
                        "USD",
                        100m,
                        50m,
                        30m,
                        20m,
                        35m,
                        2m,
                        10m,
                        15m,
                        5m,
                        200m,
                        100m,
                        80m,
                        40m,
                        25m,
                        -5m,
                        20m)
                ],
                DateTimeOffset.UtcNow,
                "Test");
            return Task.FromResult<CompanyFinancials?>(result);
        }

        public Task<CompanyMarketActivity?> GetCompanyMarketActivityAsync(
            AssetIdentifier asset,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _activityCallCount);
            CompanyMarketActivity result = new(
                asset.Ticker,
                [],
                [new DividendEvent(new DateOnly(2026, 8, 10), null, null, null, 0.26m, 0.26m, null, "Quarterly")],
                true,
                true,
                DateTimeOffset.UtcNow,
                "Test");
            return Task.FromResult<CompanyMarketActivity?>(result);
        }
    }

    private sealed class CountingInsiderProvider(bool includeTransaction = false) : IInsiderTransactionProvider
    {
        private int _callCount;

        public int CallCount => _callCount;

        public Task<InsiderTransactionSnapshot?> GetInsiderTransactionsAsync(
            AssetIdentifier asset,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            InsiderTransaction[] transactions = includeTransaction
                ?
                [
                    new InsiderTransaction(
                        new DateOnly(2026, 8, 22),
                        new DateOnly(2026, 8, 21),
                        "Alpha Executive",
                        "Chief Financial Officer",
                        "Purchase",
                        "A",
                        InsiderTransactionCategory.Purchase,
                        1_000m,
                        100m,
                        100_000m,
                        5_000m,
                        "Common Stock",
                        "AlphaVantage",
                        null)
                ]
                : [];

            return Task.FromResult<InsiderTransactionSnapshot?>(new InsiderTransactionSnapshot(
                transactions,
                DateTimeOffset.UtcNow,
                "AlphaVantage"));
        }
    }
}
