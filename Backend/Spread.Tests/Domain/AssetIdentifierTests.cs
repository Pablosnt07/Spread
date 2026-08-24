using Spread.Api.Domain.Assets;

namespace Spread.Tests.Domain;

public sealed class AssetIdentifierTests
{
    [Theory]
    [InlineData(" nvda ", "NVDA")]
    [InlineData("brk.b", "BRK.B")]
    [InlineData("BF-B", "BF-B")]
    public void TryCreate_NormalizesValidTicker(string raw, string expected)
    {
        var valid = AssetIdentifier.TryCreate(raw, out var identifier);

        Assert.True(valid);
        Assert.Equal(expected, identifier!.Ticker);
    }

    [Theory]
    [InlineData("")]
    [InlineData("AAPL/../../")]
    [InlineData("TOO-LONG-TICKER")]
    public void TryCreate_RejectsInvalidTicker(string raw)
    {
        Assert.False(AssetIdentifier.TryCreate(raw, out _));
    }
}
