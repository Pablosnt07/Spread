using Spread.Api.Domain.Portfolios;

namespace Spread.Api.Services;

public interface IPortfolioAllocationCalculator
{
    PortfolioAllocationSummary Calculate(IReadOnlyList<PortfolioPosition> positions);
}
