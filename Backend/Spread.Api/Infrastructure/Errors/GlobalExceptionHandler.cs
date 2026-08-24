using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Spread.Api.Providers;

namespace Spread.Api.Infrastructure.Errors;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private static readonly Action<ILogger, FinancialDataProviderFailure, Exception?> LogProviderFailure =
        LoggerMessage.Define<FinancialDataProviderFailure>(
            LogLevel.Warning,
            new EventId(2001, nameof(LogProviderFailure)),
            "Financial provider request failed with category {Failure}");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not FinancialDataProviderException providerException)
        {
            return false;
        }

        LogProviderFailure(logger, providerException.Failure, null);

        var detail = providerException.Failure switch
        {
            FinancialDataProviderFailure.RateLimited => "The upstream data source is rate limited. Try again later.",
            FinancialDataProviderFailure.Timeout => "The upstream data source did not respond in time.",
            FinancialDataProviderFailure.InvalidResponse => "The upstream data source returned inconsistent data.",
            _ => "The upstream data source is temporarily unavailable."
        };

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Financial data unavailable",
                Detail = detail,
                Type = "https://spread.local/problems/financial-data-unavailable"
            },
            Exception = exception
        });
    }
}
