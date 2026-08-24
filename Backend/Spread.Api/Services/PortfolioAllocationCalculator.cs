using Spread.Api.Domain.Assets;
using Spread.Api.Domain.Portfolios;

namespace Spread.Api.Services;

public sealed class PortfolioAllocationCalculator : IPortfolioAllocationCalculator
{
    public const int MaximumPositionCount = 100;
    public const decimal MaximumInvestedAmount = 1_000_000_000_000_000m;

    public PortfolioAllocationSummary Calculate(IReadOnlyList<PortfolioPosition> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);

        if (positions.Count > MaximumPositionCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(positions),
                $"A portfolio cannot contain more than {MaximumPositionCount} positions.");
        }

        if (positions.Count == 0)
        {
            return new PortfolioAllocationSummary(0, 0m, []);
        }

        var tickerOrder = new List<string>();
        var investedByTicker = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var position in positions)
        {
            if (!AssetIdentifier.TryCreate(position.Ticker, out var asset))
            {
                throw new ArgumentException("Every position must contain a valid ticker.", nameof(positions));
            }

            if (position.InvestedAmount <= 0m || position.InvestedAmount > MaximumInvestedAmount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(positions),
                    $"Every invested amount must be greater than zero and at most {MaximumInvestedAmount}.");
            }

            var ticker = asset!.Ticker;
            if (!investedByTicker.TryGetValue(ticker, out var existingAmount))
            {
                tickerOrder.Add(ticker);
            }

            investedByTicker[ticker] = checked(existingAmount + position.InvestedAmount);
        }

        var totalInvested = investedByTicker.Values.Sum();
        var allocations = tickerOrder
            .Select(ticker => new PortfolioAllocation(
                ticker,
                investedByTicker[ticker],
                Math.Round(
                    investedByTicker[ticker] / totalInvested * 100m,
                    2,
                    MidpointRounding.AwayFromZero)))
            .ToArray();

        var roundingResidual = 100m - allocations.Sum(position => position.AllocationPercentage);
        if (roundingResidual != 0m)
        {
            var largestPositionIndex = 0;
            for (var index = 1; index < allocations.Length; index++)
            {
                if (allocations[index].InvestedAmount > allocations[largestPositionIndex].InvestedAmount)
                {
                    largestPositionIndex = index;
                }
            }

            allocations[largestPositionIndex] = allocations[largestPositionIndex] with
            {
                AllocationPercentage = allocations[largestPositionIndex].AllocationPercentage + roundingResidual
            };
        }

        return new PortfolioAllocationSummary(allocations.Length, totalInvested, allocations);
    }
}
