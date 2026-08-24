using System.Text.Json.Serialization;

namespace Spread.Api.Providers.Fmp;

internal abstract record FmpStatementDto
{
    [JsonPropertyName("date")]
    public DateOnly? Date { get; init; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    [JsonPropertyName("reportedCurrency")]
    public string? ReportedCurrency { get; init; }

    [JsonPropertyName("fiscalYear")]
    public string? FiscalYear { get; init; }

    [JsonPropertyName("period")]
    public string? Period { get; init; }

    [JsonPropertyName("filingDate")]
    public DateOnly? FilingDate { get; init; }
}

internal sealed record FmpIncomeStatementDto : FmpStatementDto
{
    [JsonPropertyName("revenue")]
    public decimal? Revenue { get; init; }

    [JsonPropertyName("grossProfit")]
    public decimal? GrossProfit { get; init; }

    [JsonPropertyName("operatingIncome")]
    public decimal? OperatingIncome { get; init; }

    [JsonPropertyName("netIncome")]
    public decimal? NetIncome { get; init; }

    [JsonPropertyName("ebitda")]
    public decimal? Ebitda { get; init; }

    [JsonPropertyName("epsDiluted")]
    public decimal? DilutedEps { get; init; }

    [JsonPropertyName("weightedAverageShsOutDil")]
    public decimal? DilutedSharesOutstanding { get; init; }
}

internal sealed record FmpBalanceSheetDto : FmpStatementDto
{
    [JsonPropertyName("cashAndCashEquivalents")]
    public decimal? CashAndCashEquivalents { get; init; }

    [JsonPropertyName("totalDebt")]
    public decimal? TotalDebt { get; init; }

    [JsonPropertyName("totalAssets")]
    public decimal? TotalAssets { get; init; }

    [JsonPropertyName("totalStockholdersEquity")]
    public decimal? TotalEquity { get; init; }

    [JsonPropertyName("totalCurrentAssets")]
    public decimal? CurrentAssets { get; init; }

    [JsonPropertyName("totalCurrentLiabilities")]
    public decimal? CurrentLiabilities { get; init; }
}

internal sealed record FmpCashFlowStatementDto : FmpStatementDto
{
    [JsonPropertyName("netCashProvidedByOperatingActivities")]
    public decimal? OperatingCashFlow { get; init; }

    [JsonPropertyName("capitalExpenditure")]
    public decimal? CapitalExpenditure { get; init; }

    [JsonPropertyName("freeCashFlow")]
    public decimal? FreeCashFlow { get; init; }
}
