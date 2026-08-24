using Spread.Api.Domain.Assets;
using Spread.Api.Domain.Activity;
using Spread.Api.Domain.Companies;
using Spread.Api.Domain.Financials;
using Spread.Api.Domain.MarketData;
using Spread.Api.Services;
using Spread.Api.Infrastructure.Observability;

namespace Spread.Api.Features.Companies;

public static class CompanyEndpoints
{
    public static IEndpointRouteBuilder MapCompanyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/companies/search", async (
            string? q,
            int? limit,
            HttpContext httpContext,
            ICompanyService companyService,
            CancellationToken cancellationToken) =>
        {
            if (!CompanySearchQuery.TryCreate(q, out var query))
            {
                CompanySearchMetrics.RecordRejection("invalid_query");
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid company search",
                    detail: "Search must contain 2 to 64 letters, numbers, spaces, dots, hyphens, apostrophes, or ampersands.",
                    type: "https://spread.local/problems/invalid-company-search");
            }

            var resultLimit = limit ?? 6;
            if (resultLimit is < 1 or > 8)
            {
                CompanySearchMetrics.RecordRejection("invalid_limit");
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid result limit",
                    detail: "Search result limit must be between 1 and 8.",
                    type: "https://spread.local/problems/invalid-search-limit");
            }

            var results = await companyService.SearchAsync(query!, resultLimit, cancellationToken);
            httpContext.Response.Headers.CacheControl = "public, max-age=60";
            return Results.Ok(results.Select(CompanySearchResponse.FromDomain));
        })
        .RequireRateLimiting("company-search")
        .WithName("SearchCompanies");

        endpoints.MapGet("/api/companies/{ticker}", async (
            string ticker,
            ICompanyService companyService,
            CancellationToken cancellationToken) =>
        {
            if (!AssetIdentifier.TryCreate(ticker, out var asset))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid ticker",
                    detail: "Ticker must contain 1 to 12 letters, numbers, dots, or hyphens.",
                    type: "https://spread.local/problems/invalid-ticker");
            }

            var profile = await companyService.GetProfileAsync(asset!, cancellationToken);
            return profile is null
                ? Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Ticker not found",
                    detail: "No company profile was found for the requested ticker.",
                    type: "https://spread.local/problems/ticker-not-found")
                : Results.Ok(CompanyProfileResponse.FromDomain(profile));
        })
        .RequireRateLimiting("public-read")
        .WithName("GetCompanyProfile");

        endpoints.MapGet("/api/companies/{ticker}/financials", async (
            string ticker,
            ICompanyService companyService,
            CancellationToken cancellationToken) =>
        {
            if (!AssetIdentifier.TryCreate(ticker, out var asset))
            {
                return InvalidTicker();
            }

            var financials = await companyService.GetFinancialsAsync(asset!, cancellationToken);
            return financials is null
                ? Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Financial statements not found",
                    detail: "No annual financial statements were found for the requested ticker.",
                    type: "https://spread.local/problems/financials-not-found")
                : Results.Ok(CompanyFinancialsResponse.FromDomain(financials));
        })
        .RequireRateLimiting("provider-read")
        .WithName("GetCompanyFinancials");

        endpoints.MapGet("/api/companies/{ticker}/activity", async (
            string ticker,
            ICompanyService companyService,
            CancellationToken cancellationToken) =>
        {
            if (!AssetIdentifier.TryCreate(ticker, out var asset))
            {
                return InvalidTicker();
            }

            var activity = await companyService.GetMarketActivityAsync(asset!, cancellationToken);
            return activity is null
                ? Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Market activity not found",
                    detail: "No recent insider transactions or dividends were found for the requested ticker.",
                    type: "https://spread.local/problems/activity-not-found")
                : Results.Ok(CompanyMarketActivityResponse.FromDomain(activity));
        })
        .RequireRateLimiting("provider-read")
        .WithName("GetCompanyMarketActivity");

        endpoints.MapGet("/api/companies/{ticker}/history", async (
            string ticker,
            string? range,
            HttpContext httpContext,
            ICompanyService companyService,
            CancellationToken cancellationToken) =>
        {
            if (!AssetIdentifier.TryCreate(ticker, out var asset))
            {
                return InvalidTicker();
            }

            if (!TryParseRange(range, out var parsedRange))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid historical range",
                    detail: "Range must be one of: ytd, 1y, 3y, 5y, max.",
                    type: "https://spread.local/problems/invalid-historical-range");
            }

            var history = await companyService.GetPriceHistoryAsync(asset!, parsedRange, cancellationToken);
            if (history is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Price history not found",
                    detail: "No historical prices were found for the requested ticker and range.",
                    type: "https://spread.local/problems/price-history-not-found");
            }

            httpContext.Response.Headers.CacheControl = "public, max-age=300, stale-while-revalidate=3600";
            return Results.Ok(HistoricalPriceResponse.FromDomain(history));
        })
        .RequireRateLimiting("provider-read")
        .WithName("GetCompanyPriceHistory");

        return endpoints;
    }

    private static IResult InvalidTicker()
        => Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid ticker",
            detail: "Ticker must contain 1 to 12 letters, numbers, dots, or hyphens.",
            type: "https://spread.local/problems/invalid-ticker");

    private static bool TryParseRange(string? value, out HistoricalPriceRange range)
    {
        range = value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "5y" => HistoricalPriceRange.FiveYears,
            "ytd" => HistoricalPriceRange.YearToDate,
            "1y" => HistoricalPriceRange.OneYear,
            "3y" => HistoricalPriceRange.ThreeYears,
            "max" => HistoricalPriceRange.Maximum,
            _ => (HistoricalPriceRange)(-1)
        };
        return range >= HistoricalPriceRange.YearToDate;
    }
}

public sealed record HistoricalPriceResponse(
    string Ticker,
    string Range,
    IReadOnlyList<HistoricalPricePointResponse> Points,
    DateTimeOffset FetchedAt,
    string Provider)
{
    public static HistoricalPriceResponse FromDomain(HistoricalPriceSeries series)
        => new(series.Ticker, series.Range.ToString(), [.. series.Points.Select(point => new HistoricalPricePointResponse(point.Date, point.Price))], series.FetchedAt, series.Provider);
}

public sealed record HistoricalPricePointResponse(DateOnly Date, decimal Price);

public sealed record CompanySearchResponse(
    string Ticker,
    string CompanyName,
    string? Exchange,
    string? Currency,
    string Provider)
{
    public static CompanySearchResponse FromDomain(CompanySearchResult result)
        => new(result.Ticker, result.CompanyName, result.Exchange, result.Currency, result.Provider);
}

public sealed record CompanyMarketActivityResponse(
    string Ticker,
    IReadOnlyList<InsiderTransactionResponse> InsiderTransactions,
    IReadOnlyList<DividendEventResponse> Dividends,
    bool InsiderDataAvailable,
    bool DividendDataAvailable,
    DateTimeOffset FetchedAt,
    string Provider)
{
    public static CompanyMarketActivityResponse FromDomain(CompanyMarketActivity activity)
        => new(
            activity.Ticker,
            [.. activity.InsiderTransactions.Select(InsiderTransactionResponse.FromDomain)],
            [.. activity.Dividends.Select(DividendEventResponse.FromDomain)],
            activity.InsiderDataAvailable,
            activity.DividendDataAvailable,
            activity.FetchedAt,
            activity.Provider);
}

public sealed record InsiderTransactionResponse(
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
    string Source,
    string? FilingUrl)
{
    public static InsiderTransactionResponse FromDomain(InsiderTransaction transaction)
        => new(
            transaction.FilingDate,
            transaction.TransactionDate,
            transaction.ReportingName,
            transaction.OwnerType,
            transaction.TransactionType,
            transaction.AcquisitionOrDisposition,
            transaction.Category,
            transaction.SecuritiesTransacted,
            transaction.Price,
            transaction.TransactionValue,
            transaction.SecuritiesOwned,
            transaction.SecurityName,
            transaction.Source,
            transaction.FilingUrl);
}

public sealed record DividendEventResponse(
    DateOnly ExDividendDate,
    DateOnly? DeclarationDate,
    DateOnly? RecordDate,
    DateOnly? PaymentDate,
    decimal? Dividend,
    decimal? AdjustedDividend,
    decimal? Yield,
    string? Frequency)
{
    public static DividendEventResponse FromDomain(DividendEvent dividend)
        => new(
            dividend.ExDividendDate,
            dividend.DeclarationDate,
            dividend.RecordDate,
            dividend.PaymentDate,
            dividend.Dividend,
            dividend.AdjustedDividend,
            dividend.Yield,
            dividend.Frequency);
}

public sealed record CompanyProfileResponse(
    string Ticker,
    string CompanyName,
    AssetType AssetType,
    string? Sector,
    string? Industry,
    string? Exchange,
    string? Currency,
    string? Country,
    decimal? MarketCapitalization,
    decimal? Beta,
    bool IsActivelyTrading,
    string? Website,
    string? LogoUrl,
    DateTimeOffset FetchedAt,
    string Provider)
{
    public static CompanyProfileResponse FromDomain(CompanyProfile profile)
        => new(
            profile.Ticker,
            profile.CompanyName,
            profile.AssetType,
            profile.Sector,
            profile.Industry,
            profile.Exchange,
            profile.Currency,
            profile.Country,
            profile.MarketCapitalization,
            profile.Beta,
            profile.IsActivelyTrading,
            profile.Website,
            profile.LogoUrl,
            profile.FetchedAt,
            profile.Provider);
}

public sealed record CompanyFinancialsResponse(
    string Ticker,
    IReadOnlyList<FinancialPeriodResponse> Periods,
    DateTimeOffset FetchedAt,
    string Provider)
{
    public static CompanyFinancialsResponse FromDomain(CompanyFinancials financials)
        => new(
            financials.Ticker,
            [.. financials.Periods.Select(FinancialPeriodResponse.FromDomain)],
            financials.FetchedAt,
            financials.Provider);
}

public sealed record FinancialPeriodResponse(
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
    decimal? FreeCashFlow)
{
    public static FinancialPeriodResponse FromDomain(FinancialPeriod period)
        => new(
            period.PeriodEnd,
            period.FiscalYear,
            period.Period,
            period.FilingDate,
            period.ReportedCurrency,
            period.Revenue,
            period.GrossProfit,
            period.OperatingIncome,
            period.NetIncome,
            period.Ebitda,
            period.DilutedEps,
            period.DilutedSharesOutstanding,
            period.CashAndCashEquivalents,
            period.TotalDebt,
            period.TotalAssets,
            period.TotalEquity,
            period.CurrentAssets,
            period.CurrentLiabilities,
            period.OperatingCashFlow,
            period.CapitalExpenditure,
            period.FreeCashFlow);
}
