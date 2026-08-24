using System.Text.Json.Serialization;

namespace Spread.Api.Providers.Fmp;

internal sealed record FmpProfileDto
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    [JsonPropertyName("companyName")]
    public string? CompanyName { get; init; }

    [JsonPropertyName("sector")]
    public string? Sector { get; init; }

    [JsonPropertyName("industry")]
    public string? Industry { get; init; }

    [JsonPropertyName("exchange")]
    public string? Exchange { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("marketCap")]
    public decimal? MarketCapitalization { get; init; }

    [JsonPropertyName("beta")]
    public decimal? Beta { get; init; }

    [JsonPropertyName("website")]
    public string? Website { get; init; }

    [JsonPropertyName("image")]
    public string? Image { get; init; }

    [JsonPropertyName("isEtf")]
    public bool IsEtf { get; init; }

    [JsonPropertyName("isFund")]
    public bool IsFund { get; init; }

    [JsonPropertyName("isActivelyTrading")]
    public bool IsActivelyTrading { get; init; }
}
