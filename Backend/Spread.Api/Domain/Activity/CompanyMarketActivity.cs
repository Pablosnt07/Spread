namespace Spread.Api.Domain.Activity;

public sealed record CompanyMarketActivity(
    string Ticker,
    IReadOnlyList<InsiderTransaction> InsiderTransactions,
    IReadOnlyList<DividendEvent> Dividends,
    bool InsiderDataAvailable,
    bool DividendDataAvailable,
    DateTimeOffset FetchedAt,
    string Provider);

public sealed record InsiderTransaction(
    DateOnly FilingDate,
    DateOnly? TransactionDate,
    string ReportingName,
    string? OwnerType,
    string? TransactionType,
    string? AcquisitionOrDisposition,
    InsiderTransactionCategory Category,
    decimal? SecuritiesTransacted,
    decimal? Price,
    decimal? TransactionValue,
    decimal? SecuritiesOwned,
    string? SecurityName,
    string? FilingUrl);

public enum InsiderTransactionCategory
{
    Purchase,
    Sale,
    Award,
    Exercise,
    Gift,
    Other
}

public sealed record DividendEvent(
    DateOnly ExDividendDate,
    DateOnly? DeclarationDate,
    DateOnly? RecordDate,
    DateOnly? PaymentDate,
    decimal? Dividend,
    decimal? AdjustedDividend,
    decimal? Yield,
    string? Frequency);
