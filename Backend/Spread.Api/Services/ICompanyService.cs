using Spread.Api.Domain.Assets;
using Spread.Api.Domain.Activity;
using Spread.Api.Domain.Companies;
using Spread.Api.Domain.Financials;

namespace Spread.Api.Services;

public interface ICompanyService
{
    Task<CompanyProfile?> GetProfileAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken = default);

    Task<CompanyFinancials?> GetFinancialsAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken = default);

    Task<CompanyMarketActivity?> GetMarketActivityAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken = default);
}
