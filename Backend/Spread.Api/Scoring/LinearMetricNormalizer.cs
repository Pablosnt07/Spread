namespace Spread.Api.Scoring;

public static class LinearMetricNormalizer
{
    public static decimal HigherIsBetter(decimal value, decimal lowerAnchor, decimal upperAnchor)
        => Normalize(value, lowerAnchor, upperAnchor, invert: false);

    public static decimal LowerIsBetter(decimal value, decimal lowerAnchor, decimal upperAnchor)
        => Normalize(value, lowerAnchor, upperAnchor, invert: true);

    public static decimal BlendWithPeers(decimal absoluteScore, decimal? peerScore)
    {
        ValidateScore(absoluteScore, nameof(absoluteScore));
        if (!peerScore.HasValue)
        {
            return absoluteScore;
        }

        ValidateScore(peerScore.Value, nameof(peerScore));
        return 0.45m * absoluteScore + 0.55m * peerScore.Value;
    }

    private static decimal Normalize(decimal value, decimal lowerAnchor, decimal upperAnchor, bool invert)
    {
        if (upperAnchor <= lowerAnchor)
        {
            throw new ArgumentException("Upper anchor must be greater than lower anchor.");
        }

        var normalized = 100m * (value - lowerAnchor) / (upperAnchor - lowerAnchor);
        normalized = Math.Clamp(normalized, 0m, 100m);
        return invert ? 100m - normalized : normalized;
    }

    private static void ValidateScore(decimal score, string name)
    {
        if (score is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(name, "Score must be between 0 and 100.");
        }
    }
}
