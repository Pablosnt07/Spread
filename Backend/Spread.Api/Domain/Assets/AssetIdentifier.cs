namespace Spread.Api.Domain.Assets;

public sealed record AssetIdentifier(string Ticker, string? Exchange = null)
{
    private const int MaximumTickerLength = 12;

    public static bool TryCreate(string? rawTicker, out AssetIdentifier? identifier)
    {
        identifier = null;
        var ticker = rawTicker?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(ticker) || ticker.Length > MaximumTickerLength)
        {
            return false;
        }

        if (ticker.Any(character =>
                !(character is >= 'A' and <= 'Z' || character is >= '0' and <= '9' || character is '.' or '-')))
        {
            return false;
        }

        identifier = new AssetIdentifier(ticker);
        return true;
    }
}
