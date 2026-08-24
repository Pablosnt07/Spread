using System.Text.Json.Serialization;

namespace Spread.Api.Providers.Fmp;

internal sealed class FmpHistoricalPriceDto
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    [JsonPropertyName("date")]
    public DateOnly? Date { get; init; }

    [JsonPropertyName("price")]
    public decimal? Price { get; init; }
}
