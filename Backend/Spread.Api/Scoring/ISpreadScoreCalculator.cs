using Spread.Api.Domain.Scoring;

namespace Spread.Api.Scoring;

public interface ISpreadScoreCalculator
{
    SpreadScoreResult Calculate(
        IReadOnlyCollection<DimensionInput> dimensions,
        ConfidenceInput confidence,
        string modelVersion,
        decimal minimumCoverage,
        decimal minimumConfidence);
}
