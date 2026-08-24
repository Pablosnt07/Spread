namespace Spread.Api.Domain.Financials;

public sealed record CompanyFinancials(
    string Ticker,
    IReadOnlyList<FinancialPeriod> Periods,
    DateTimeOffset FetchedAt,
    string Provider);

public sealed record FinancialPeriod(
    DateOnly PeriodEnd,
    string FiscalYear,
    string Period,
    DateOnly? FilingDate,
    string? ReportedCurrency,
    decimal? Revenue,
    decimal? GrossProfit,
    decimal? OperatingIncome,
    decimal? NetIncome,
    decimal? Ebitda,
    decimal? DilutedEps,
    decimal? DilutedSharesOutstanding,
    decimal? CashAndCashEquivalents,
    decimal? TotalDebt,
    decimal? TotalAssets,
    decimal? TotalEquity,
    decimal? CurrentAssets,
    decimal? CurrentLiabilities,
    decimal? OperatingCashFlow,
    decimal? CapitalExpenditure,
    decimal? FreeCashFlow);
