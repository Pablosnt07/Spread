namespace Spread.Api.Providers;

public sealed class FinancialDataProviderException(
    string message,
    FinancialDataProviderFailure failure,
    Exception? innerException = null) : Exception(message, innerException)
{
    public FinancialDataProviderFailure Failure { get; } = failure;
}

public enum FinancialDataProviderFailure
{
    RateLimited,
    Timeout,
    Unavailable,
    InvalidResponse
}
