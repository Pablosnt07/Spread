using Spread.Api.Scoring;

namespace Spread.Tests.Scoring;

public sealed class LinearMetricNormalizerTests
{
    [Theory]
    [InlineData(-5, 0)]
    [InlineData(10, 50)]
    [InlineData(25, 100)]
    public void HigherIsBetter_InterpolatesAndClamps(decimal value, decimal expected)
    {
        Assert.Equal(expected, LinearMetricNormalizer.HigherIsBetter(value, 0m, 20m));
    }

    [Theory]
    [InlineData(-5, 100)]
    [InlineData(10, 50)]
    [InlineData(25, 0)]
    public void LowerIsBetter_InterpolatesAndClamps(decimal value, decimal expected)
    {
        Assert.Equal(expected, LinearMetricNormalizer.LowerIsBetter(value, 0m, 20m));
    }

    [Fact]
    public void BlendWithPeers_UsesAbsoluteScoreWhenPeersAreUnavailable()
    {
        Assert.Equal(73m, LinearMetricNormalizer.BlendWithPeers(73m, null));
    }

    [Fact]
    public void BlendWithPeers_UsesCanonicalBlend()
    {
        Assert.Equal(66.5m, LinearMetricNormalizer.BlendWithPeers(50m, 80m));
    }
}
