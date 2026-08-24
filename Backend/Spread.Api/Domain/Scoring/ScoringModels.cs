namespace Spread.Api.Domain.Scoring;

public sealed record DimensionInput(
    ScoreDimension Dimension,
    decimal? Score,
    decimal Coverage);

public sealed record ConfidenceInput(
    decimal Coverage,
    decimal Freshness,
    decimal PeerQuality,
    decimal Consistency);

public enum ScorePublicationStatus
{
    Published,
    InsufficientData
}

public sealed record ConfidenceResult(decimal Score, string Label);

public sealed record SpreadScoreResult(
    ScorePublicationStatus Status,
    decimal? Score,
    decimal Coverage,
    ConfidenceResult Confidence,
    IReadOnlyDictionary<ScoreDimension, decimal> Dimensions,
    string ModelVersion);
