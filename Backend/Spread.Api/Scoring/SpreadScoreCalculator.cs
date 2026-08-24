using Spread.Api.Configuration;
using Spread.Api.Domain.Scoring;
using Microsoft.Extensions.Options;

namespace Spread.Api.Scoring;

public sealed class SpreadScoreCalculator : ISpreadScoreCalculator
{
    private readonly IReadOnlyDictionary<ScoreDimension, decimal> _weights;

    public SpreadScoreCalculator()
        : this(SpreadScoringDefaults.DimensionWeights)
    {
    }

    public SpreadScoreCalculator(IOptions<ScoringOptions> options)
        : this(options?.Value.DimensionWeights ?? throw new ArgumentNullException(nameof(options)))
    {
    }

    public SpreadScoreCalculator(IReadOnlyDictionary<ScoreDimension, decimal> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        if (weights.Count == 0 || weights.Values.Any(weight => weight <= 0m))
        {
            throw new ArgumentException("Dimension weights must be positive.", nameof(weights));
        }

        if (Math.Abs(weights.Values.Sum() - 1m) > 0.000001m)
        {
            throw new ArgumentException("Dimension weights must sum to 1.", nameof(weights));
        }

        _weights = weights;
    }

    public SpreadScoreResult Calculate(
        IReadOnlyCollection<DimensionInput> dimensions,
        ConfidenceInput confidence,
        string modelVersion,
        decimal minimumCoverage,
        decimal minimumConfidence)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelVersion);

        ValidateUnitInterval(minimumCoverage, nameof(minimumCoverage));
        ValidateScore(minimumConfidence, nameof(minimumConfidence));
        ValidateConfidence(confidence);

        var available = dimensions
            .Where(dimension => dimension.Score.HasValue && _weights.ContainsKey(dimension.Dimension))
            .ToDictionary(dimension => dimension.Dimension, dimension => dimension);

        foreach (var dimension in dimensions)
        {
            ValidateUnitInterval(dimension.Coverage, $"{dimension.Dimension} coverage");
            if (dimension.Score.HasValue)
            {
                ValidateScore(dimension.Score.Value, dimension.Dimension.ToString());
                if (dimension.Coverage == 0m)
                {
                    throw new ArgumentException(
                        $"{dimension.Dimension} cannot have a score with zero coverage.",
                        nameof(dimensions));
                }
            }
        }

        var totalCoverage = _weights.Sum(pair =>
            available.TryGetValue(pair.Key, out var input) ? pair.Value * input.Coverage : 0m);
        var confidenceScore = CalculateConfidence(confidence with { Coverage = totalCoverage });

        var resultDimensions = available.ToDictionary(pair => pair.Key, pair => pair.Value.Score!.Value);
        var canPublish = totalCoverage >= minimumCoverage && confidenceScore.Score >= minimumConfidence;

        decimal? score = null;
        if (canPublish)
        {
            var availableWeight = available.Keys.Sum(dimension => _weights[dimension]);
            score = available.Sum(pair => _weights[pair.Key] * pair.Value.Score!.Value) / availableWeight;
            score = Math.Clamp(score.Value, 0m, 100m);
        }

        return new SpreadScoreResult(
            canPublish ? ScorePublicationStatus.Published : ScorePublicationStatus.InsufficientData,
            score,
            totalCoverage,
            confidenceScore,
            resultDimensions,
            modelVersion);
    }

    private static ConfidenceResult CalculateConfidence(ConfidenceInput input)
    {
        var score = 100m * (
            0.45m * input.Coverage
            + 0.20m * input.Freshness
            + 0.20m * input.PeerQuality
            + 0.15m * input.Consistency);

        var label = score switch
        {
            >= 80m => "High",
            >= 60m => "Medium",
            >= 40m => "Low",
            _ => "Insufficient"
        };

        return new ConfidenceResult(score, label);
    }

    private static void ValidateConfidence(ConfidenceInput input)
    {
        ValidateUnitInterval(input.Coverage, nameof(input.Coverage));
        ValidateUnitInterval(input.Freshness, nameof(input.Freshness));
        ValidateUnitInterval(input.PeerQuality, nameof(input.PeerQuality));
        ValidateUnitInterval(input.Consistency, nameof(input.Consistency));
    }

    private static void ValidateUnitInterval(decimal value, string name)
    {
        if (value is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(name, "Value must be between 0 and 1.");
        }
    }

    private static void ValidateScore(decimal value, string name)
    {
        if (value is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(name, "Score must be between 0 and 100.");
        }
    }
}
