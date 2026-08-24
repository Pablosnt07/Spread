using Spread.Api.Domain.Scoring;

namespace Spread.Api.Configuration;

public static class SpreadScoringDefaults
{
    public static readonly IReadOnlyDictionary<ScoreDimension, decimal> DimensionWeights
        = new Dictionary<ScoreDimension, decimal>
        {
            [ScoreDimension.Quality] = 0.22m,
            [ScoreDimension.Growth] = 0.18m,
            [ScoreDimension.Profitability] = 0.18m,
            [ScoreDimension.Valuation] = 0.18m,
            [ScoreDimension.FinancialHealth] = 0.14m,
            [ScoreDimension.Risk] = 0.10m
        };
}
