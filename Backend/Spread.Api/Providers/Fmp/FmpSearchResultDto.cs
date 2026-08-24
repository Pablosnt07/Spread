using System.Text.Json.Serialization;

namespace Spread.Api.Providers.Fmp;

internal sealed record FmpSearchResultDto
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("stockExchange")]
    public string? StockExchange { get; init; }

    [JsonPropertyName("exchange")]
    public string? Exchange { get; init; }

    [JsonPropertyName("exchangeShortName")]
    public string? ExchangeShortName { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }
}
