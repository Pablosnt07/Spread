using System.ComponentModel.DataAnnotations;

namespace Spread.Api.Configuration;

public sealed class FmpOptions
{
    public const string SectionName = "FinancialData:Fmp";

    [Required]
    [Url]
    public string BaseUrl { get; init; } = "https://financialmodelingprep.com/stable/";

    [Required]
    [MinLength(1)]
    public string ApiKey { get; init; } = string.Empty;

    [Range(1, 60)]
    public int TimeoutSeconds { get; init; } = 15;
}
