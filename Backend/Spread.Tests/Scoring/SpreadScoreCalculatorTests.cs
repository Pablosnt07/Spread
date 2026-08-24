using Spread.Api.Domain.Scoring;
using Spread.Api.Scoring;

namespace Spread.Tests.Scoring;

public sealed class SpreadScoreCalculatorTests
{
    private readonly SpreadScoreCalculator _calculator = new();

    [Fact]
    public void Calculate_WithCompleteData_UsesCanonicalDimensionWeights()
    {
        var dimensions = new[]
        {
            new DimensionInput(ScoreDimension.Quality, 80m, 1m),
            new DimensionInput(ScoreDimension.Growth, 70m, 1m),
            new DimensionInput(ScoreDimension.Profitability, 90m, 1m),
            new DimensionInput(ScoreDimension.Valuation, 60m, 1m),
            new DimensionInput(ScoreDimension.FinancialHealth, 85m, 1m),
            new DimensionInput(ScoreDimension.Risk, 75m, 1m)
        };

        var result = _calculator.Calculate(
            dimensions,
            new ConfidenceInput(1m, 1m, 1m, 1m),
            "test-v1",
            0.70m,
            40m);

        Assert.Equal(ScorePublicationStatus.Published, result.Status);
        Assert.Equal(76.6m, result.Score);
        Assert.Equal(100m, result.Confidence.Score);
    }

    [Fact]
    public void Calculate_WithMissingDimension_RenormalizesAvailableWeights()
    {
        var dimensions = new[]
        {
            new DimensionInput(ScoreDimension.Quality, 80m, 1m),
            new DimensionInput(ScoreDimension.Growth, null, 0m),
            new DimensionInput(ScoreDimension.Profitability, 90m, 1m),
            new DimensionInput(ScoreDimension.Valuation, 60m, 1m),
            new DimensionInput(ScoreDimension.FinancialHealth, 85m, 1m),
            new DimensionInput(ScoreDimension.Risk, 75m, 1m)
        };

        var result = _calculator.Calculate(
            dimensions,
            new ConfidenceInput(0.82m, 1m, 1m, 1m),
            "test-v1",
            0.70m,
            40m);

        Assert.Equal(ScorePublicationStatus.Published, result.Status);
        Assert.Equal(0.82m, result.Coverage);
        Assert.Equal(78.048780487804878048780487805m, result.Score);
    }

    [Fact]
    public void Calculate_WhenCoverageIsTooLow_DoesNotPublishScore()
    {
        var dimensions = new[]
        {
            new DimensionInput(ScoreDimension.Quality, 95m, 1m),
            new DimensionInput(ScoreDimension.Growth, 95m, 1m)
        };

        var result = _calculator.Calculate(
            dimensions,
            new ConfidenceInput(0.40m, 1m, 1m, 1m),
            "test-v1",
            0.70m,
            40m);

        Assert.Equal(ScorePublicationStatus.InsufficientData, result.Status);
        Assert.Null(result.Score);
    }

    [Fact]
    public void Constructor_RejectsWeightsThatDoNotSumToOne()
    {
        var invalid = new Dictionary<ScoreDimension, decimal>
        {
            [ScoreDimension.Quality] = 0.5m,
            [ScoreDimension.Growth] = 0.4m
        };

        Assert.Throws<ArgumentException>(() => new SpreadScoreCalculator(invalid));
    }

    [Fact]
    public void Calculate_DerivesConfidenceCoverageFromDimensionCoverage()
    {
        var dimensions = Enum.GetValues<ScoreDimension>()
            .Select(dimension => new DimensionInput(dimension, 80m, 0.70m))
            .ToArray();

        var result = _calculator.Calculate(
            dimensions,
            new ConfidenceInput(1m, 1m, 1m, 1m),
            "test-v1",
            0.70m,
            40m);

        Assert.Equal(0.70m, result.Coverage);
        Assert.Equal(86.5m, result.Confidence.Score);
    }

    [Fact]
    public void Calculate_RejectsScoreWithZeroCoverage()
    {
        var dimensions = new[]
        {
            new DimensionInput(ScoreDimension.Quality, 80m, 0m)
        };

        Assert.Throws<ArgumentException>(() => _calculator.Calculate(
            dimensions,
            new ConfidenceInput(0m, 1m, 1m, 1m),
            "test-v1",
            0.70m,
            40m));
    }
}
