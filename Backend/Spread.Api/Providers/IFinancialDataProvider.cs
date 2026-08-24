using Spread.Api.Domain.Assets;
using Spread.Api.Domain.Activity;
using Spread.Api.Domain.Companies;
using Spread.Api.Domain.Financials;
using Spread.Api.Domain.MarketData;
using Spread.Api.Domain.Scoring;

namespace Spread.Api.Providers;

public interface IFinancialDataProvider
{
    Task<IReadOnlyList<CompanySearchResult>> SearchCompaniesAsync(
        CompanySearchQuery query,
        int limit,
        CancellationToken cancellationToken = default);

    Task<CompanyProfile?> GetCompanyProfileAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken = default);

    Task<CompanyFinancials?> GetCompanyFinancialsAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken = default);

    Task<CompanyMarketActivity?> GetCompanyMarketActivityAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HistoricalPricePoint>> GetHistoricalPricesAsync(
        AssetIdentifier asset,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}

public sealed record MarketDataResult(
    AssetIdentifier Asset,
    string Provider,
    DateTimeOffset FetchedAt,
    IReadOnlyCollection<NormalizedMetric> Metrics,
    IReadOnlyCollection<string> Warnings);

public sealed record NormalizedMetric(
    string Name,
    decimal? Value,
    string Unit,
    string? Currency,
    DateOnly? PeriodEnd,
    MetricStatus Status,
    string Source);
