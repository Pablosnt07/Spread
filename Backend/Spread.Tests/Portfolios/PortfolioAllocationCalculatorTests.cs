using Spread.Api.Domain.Portfolios;
using Spread.Api.Services;

namespace Spread.Tests.Portfolios;

public sealed class PortfolioAllocationCalculatorTests
{
    private readonly PortfolioAllocationCalculator _calculator = new();

    [Fact]
    public void Calculate_ReturnsAssetCountTotalAndPercentages()
    {
        var result = _calculator.Calculate(
        [
            new PortfolioPosition("AAPL", 6_000m),
            new PortfolioPosition("MSFT", 3_000m),
            new PortfolioPosition("NVDA", 1_000m)
        ]);

        Assert.Equal(3, result.AssetCount);
        Assert.Equal(10_000m, result.TotalInvested);
        Assert.Collection(
            result.Positions,
            position => Assert.Equal(new PortfolioAllocation("AAPL", 6_000m, 60m), position),
            position => Assert.Equal(new PortfolioAllocation("MSFT", 3_000m, 30m), position),
            position => Assert.Equal(new PortfolioAllocation("NVDA", 1_000m, 10m), position));
    }

    [Fact]
    public void Calculate_AggregatesDuplicateTickers()
    {
        var result = _calculator.Calculate(
        [
            new PortfolioPosition("aapl", 2_000m),
            new PortfolioPosition("MSFT", 1_000m),
            new PortfolioPosition("AAPL", 1_000m)
        ]);

        Assert.Equal(2, result.AssetCount);
        Assert.Equal(4_000m, result.TotalInvested);
        Assert.Collection(
            result.Positions,
            position => Assert.Equal(new PortfolioAllocation("AAPL", 3_000m, 75m), position),
            position => Assert.Equal(new PortfolioAllocation("MSFT", 1_000m, 25m), position));
    }

    [Fact]
    public void Calculate_RoundingAlwaysSumsToOneHundredPercent()
    {
        var result = _calculator.Calculate(
        [
            new PortfolioPosition("AAPL", 1m),
            new PortfolioPosition("MSFT", 1m),
            new PortfolioPosition("NVDA", 1m)
        ]);

        Assert.Equal(100m, result.Positions.Sum(position => position.AllocationPercentage));
        Assert.Equal(33.34m, result.Positions[0].AllocationPercentage);
        Assert.Equal(33.33m, result.Positions[1].AllocationPercentage);
        Assert.Equal(33.33m, result.Positions[2].AllocationPercentage);
    }

    [Fact]
    public void Calculate_EmptyPortfolioReturnsZeroSummary()
    {
        var result = _calculator.Calculate([]);

        Assert.Equal(0, result.AssetCount);
        Assert.Equal(0m, result.TotalInvested);
        Assert.Empty(result.Positions);
    }

    [Fact]
    public void Calculate_RejectsNonPositiveAmounts()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _calculator.Calculate([new PortfolioPosition("AAPL", 0m)]));

        Assert.Equal("positions", exception.ParamName);
    }
}
