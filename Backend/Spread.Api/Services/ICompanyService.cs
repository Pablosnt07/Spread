using Spread.Api.Domain.Assets;
using Spread.Api.Domain.Activity;
using Spread.Api.Domain.Companies;
using Spread.Api.Domain.Financials;
using Spread.Api.Domain.MarketData;

namespace Spread.Api.Services;

public interface ICompanyService
{
    Task<IReadOnlyList<CompanySearchResult>> SearchAsync(
        CompanySearchQuery query,
        int limit,
        CancellationToken cancellationToken = default);

    Task<CompanyProfile?> GetProfileAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken = default);

    Task<CompanyFinancials?> GetFinancialsAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken = default);

    Task<CompanyMarketActivity?> GetMarketActivityAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken = default);

    Task<HistoricalPriceSeries?> GetPriceHistoryAsync(
        AssetIdentifier asset,
        HistoricalPriceRange range,
        CancellationToken cancellationToken = default);
}
