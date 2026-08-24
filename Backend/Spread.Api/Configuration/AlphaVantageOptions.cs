using System.ComponentModel.DataAnnotations;

namespace Spread.Api.Configuration;

public sealed class AlphaVantageOptions
{
    public const string SectionName = "FinancialData:AlphaVantage";

    public bool Enabled { get; init; }

    [Required]
    [Url]
    public string BaseUrl { get; init; } = "https://www.alphavantage.co/";

    public string ApiKey { get; init; } = string.Empty;

    [Range(1, 60)]
    public int TimeoutSeconds { get; init; } = 15;

    [Range(1, 10)]
    public int LookbackYears { get; init; } = 5;

    [Range(1, 100)]
    public int OutputLimit { get; init; } = 20;
}
