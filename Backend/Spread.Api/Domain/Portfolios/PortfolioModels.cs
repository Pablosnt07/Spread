namespace Spread.Api.Domain.Portfolios;

public sealed record PortfolioPosition(string Ticker, decimal InvestedAmount);

public sealed record PortfolioAllocation(
    string Ticker,
    decimal InvestedAmount,
    decimal AllocationPercentage);

public sealed record PortfolioAllocationSummary(
    int AssetCount,
    decimal TotalInvested,
    IReadOnlyList<PortfolioAllocation> Positions);
