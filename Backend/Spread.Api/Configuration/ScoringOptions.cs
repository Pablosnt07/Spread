using System.ComponentModel.DataAnnotations;
using Spread.Api.Domain.Scoring;

namespace Spread.Api.Configuration;

public sealed class ScoringOptions
{
    public const string SectionName = "Scoring";

    [Required]
    public string ModelVersion { get; init; } = "standard-company-v0.1.0";

    [Range(0, 1)]
    public decimal MinimumCoverage { get; init; } = 0.70m;

    [Range(0, 100)]
    public decimal MinimumConfidence { get; init; } = 40m;

    public IReadOnlyDictionary<ScoreDimension, decimal> DimensionWeights { get; init; }
        = SpreadScoringDefaults.DimensionWeights;
}
