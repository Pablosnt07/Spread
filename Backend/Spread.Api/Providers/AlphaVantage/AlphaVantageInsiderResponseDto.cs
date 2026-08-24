using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spread.Api.Providers.AlphaVantage;

internal sealed record AlphaVantageInsiderResponseDto
{
    [JsonPropertyName("data")]
    public JsonElement[]? Data { get; init; }

    [JsonPropertyName("Note")]
    public string? Note { get; init; }

    [JsonPropertyName("Information")]
    public string? Information { get; init; }

    [JsonPropertyName("Error Message")]
    public string? ErrorMessage { get; init; }
}
