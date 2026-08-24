using Spread.Api.Domain.Assets;

namespace Spread.Api.Domain.Companies;

public sealed record CompanyProfile(
    string Ticker,
    string CompanyName,
    AssetType AssetType,
    string? Sector,
    string? Industry,
    string? Exchange,
    string? Currency,
    string? Country,
    decimal? MarketCapitalization,
    decimal? Beta,
    bool IsActivelyTrading,
    string? Website,
    string? LogoUrl,
    DateTimeOffset FetchedAt,
    string Provider);
