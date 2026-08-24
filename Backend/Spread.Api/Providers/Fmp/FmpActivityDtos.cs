using System.Text.Json.Serialization;

namespace Spread.Api.Providers.Fmp;

internal sealed class FmpInsiderTransactionDto
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    [JsonPropertyName("filingDate")]
    public DateOnly? FilingDate { get; init; }

    [JsonPropertyName("transactionDate")]
    public DateOnly? TransactionDate { get; init; }

    [JsonPropertyName("reportingName")]
    public string? ReportingName { get; init; }

    [JsonPropertyName("typeOfOwner")]
    public string? TypeOfOwner { get; init; }

    [JsonPropertyName("transactionType")]
    public string? TransactionType { get; init; }

    [JsonPropertyName("acquisitionOrDisposition")]
    public string? AcquisitionOrDisposition { get; init; }

    [JsonPropertyName("securitiesTransacted")]
    public decimal? SecuritiesTransacted { get; init; }

    [JsonPropertyName("price")]
    public decimal? Price { get; init; }

    [JsonPropertyName("securitiesOwned")]
    public decimal? SecuritiesOwned { get; init; }

    [JsonPropertyName("securityName")]
    public string? SecurityName { get; init; }

    [JsonPropertyName("link")]
    public string? Link { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }
}

internal sealed class FmpDividendDto
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    [JsonPropertyName("date")]
    public DateOnly? Date { get; init; }

    [JsonPropertyName("declarationDate")]
    public string? DeclarationDate { get; init; }

    [JsonPropertyName("recordDate")]
    public string? RecordDate { get; init; }

    [JsonPropertyName("paymentDate")]
    public string? PaymentDate { get; init; }

    [JsonPropertyName("dividend")]
    public decimal? Dividend { get; init; }

    [JsonPropertyName("adjDividend")]
    public decimal? AdjustedDividend { get; init; }

    [JsonPropertyName("yield")]
    public decimal? Yield { get; init; }

    [JsonPropertyName("frequency")]
    public string? Frequency { get; init; }
}
