using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Spread.Api.Domain.Activity;
using Spread.Api.Domain.Assets;
using Spread.Api.Domain.Companies;
using Spread.Api.Domain.Financials;
using Spread.Api.Domain.MarketData;

namespace Spread.Api.Providers.Fmp;

public sealed class FmpFinancialDataProvider(HttpClient httpClient) : IFinancialDataProvider
{
    private const long MaximumResponseBytes = 1_048_576;
    private const int AnnualPeriodLimit = 5;
    private const int InsiderRequestLimit = 100;
    private const int InsiderOutputLimit = 12;
    private const int DividendOutputLimit = 8;
    private const int HistoricalOutputLimit = 320;

    public async Task<IReadOnlyList<HistoricalPricePoint>> GetHistoricalPricesAsync(
        AssetIdentifier asset,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (startDate > endDate || startDate < endDate.AddYears(-25))
        {
            throw new ArgumentOutOfRangeException(nameof(startDate));
        }

        var rows = await GetArrayAsync<FmpHistoricalPriceDto>(
            $"historical-price-eod/light?symbol={Uri.EscapeDataString(asset.Ticker)}&from={startDate:yyyy-MM-dd}&to={endDate:yyyy-MM-dd}",
            cancellationToken);

        var points = new List<HistoricalPricePoint>(rows.Length);
        foreach (var row in rows)
        {
            if (row.Symbol is not null)
            {
                ValidateSymbol(row.Symbol, asset, "A historical price");
            }

            if (!row.Date.HasValue || !row.Price.HasValue || row.Price <= 0 || row.Date < startDate || row.Date > endDate)
            {
                throw InvalidResponse("A historical price returned invalid values.");
            }

            points.Add(new HistoricalPricePoint(row.Date.Value, row.Price.Value));
        }

        var ordered = points
            .DistinctBy(point => point.Date)
            .OrderBy(point => point.Date)
            .ToArray();
        if (ordered.Length <= HistoricalOutputLimit)
        {
            return ordered;
        }

        var step = (double)(ordered.Length - 1) / (HistoricalOutputLimit - 1);
        return [.. Enumerable.Range(0, HistoricalOutputLimit)
            .Select(index => ordered[(int)Math.Round(index * step)])
            .DistinctBy(point => point.Date)];
    }

    public async Task<IReadOnlyList<CompanySearchResult>> SearchCompaniesAsync(
        CompanySearchQuery query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (limit is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var tickerQuery = AssetIdentifier.TryCreate(query.Value, out _)
            && query.Value.All(character => !char.IsLetter(character) || char.IsUpper(character));
        var endpoint = tickerQuery
            ? "search-symbol"
            : "search-name";
        var rows = await GetArrayAsync<FmpSearchResultDto>(
            $"{endpoint}?query={Uri.EscapeDataString(query.Value)}",
            cancellationToken);
        if (!tickerQuery && rows.Length == 0)
        {
            rows = await GetArrayAsync<FmpSearchResultDto>(
                $"search-symbol?query={Uri.EscapeDataString(query.Value)}",
                cancellationToken);
        }

        return [.. rows
            .Select(MapSearchResult)
            .Where(result => result is not null)
            .Cast<CompanySearchResult>()
            .DistinctBy(result => result.Ticker, StringComparer.Ordinal)
            .Take(limit)];
    }

    public async Task<CompanyProfile?> GetCompanyProfileAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"profile?symbol={Uri.EscapeDataString(asset.Ticker)}");

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new FinancialDataProviderException(
                    "The financial data provider rate limit was reached.",
                    FinancialDataProviderFailure.RateLimited);
            }

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new FinancialDataProviderException(
                    "The financial data provider is temporarily unavailable.",
                    FinancialDataProviderFailure.Unavailable);
            }

            await response.Content.LoadIntoBufferAsync(MaximumResponseBytes, cancellationToken);
            var profiles = await response.Content.ReadFromJsonAsync<FmpProfileDto[]>(cancellationToken);
            var profile = profiles?.FirstOrDefault();

            if (profile is null)
            {
                return null;
            }

            var ticker = profile.Symbol?.Trim().ToUpperInvariant();
            if (!string.Equals(ticker, asset.Ticker, StringComparison.Ordinal))
            {
                throw new FinancialDataProviderException(
                    "The financial data provider returned an inconsistent symbol.",
                    FinancialDataProviderFailure.InvalidResponse);
            }

            if (string.IsNullOrWhiteSpace(profile.CompanyName))
            {
                throw new FinancialDataProviderException(
                    "The financial data provider returned an incomplete company profile.",
                    FinancialDataProviderFailure.InvalidResponse);
            }

            return new CompanyProfile(
                asset.Ticker,
                profile.CompanyName.Trim(),
                ClassifyAsset(profile),
                NormalizeOptional(profile.Sector),
                NormalizeOptional(profile.Industry),
                NormalizeOptional(profile.Exchange),
                NormalizeOptional(profile.Currency),
                NormalizeOptional(profile.Country),
                profile.MarketCapitalization,
                profile.Beta,
                profile.IsActivelyTrading,
                NormalizeOptional(profile.Website),
                NormalizeLogoUrl(profile.Image),
                DateTimeOffset.UtcNow,
                "FMP");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FinancialDataProviderException(
                "The financial data provider request timed out.",
                FinancialDataProviderFailure.Timeout,
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new FinancialDataProviderException(
                "The financial data provider could not be reached.",
                FinancialDataProviderFailure.Unavailable,
                exception);
        }
    }

    public async Task<CompanyFinancials?> GetCompanyFinancialsAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var incomeTask = GetStatementsAsync<FmpIncomeStatementDto>(
            "income-statement",
            asset,
            cancellationToken);
        var balanceTask = GetStatementsAsync<FmpBalanceSheetDto>(
            "balance-sheet-statement",
            asset,
            cancellationToken);
        var cashFlowTask = GetStatementsAsync<FmpCashFlowStatementDto>(
            "cash-flow-statement",
            asset,
            cancellationToken);

        await Task.WhenAll(incomeTask, balanceTask, cashFlowTask);

        var incomeRows = await incomeTask;
        var balanceRows = await balanceTask;
        var cashFlowRows = await cashFlowTask;

        if (incomeRows.Length == 0 && balanceRows.Length == 0 && cashFlowRows.Length == 0)
        {
            return null;
        }

        var incomeByPeriod = IndexStatements(incomeRows, asset);
        var balanceByPeriod = IndexStatements(balanceRows, asset);
        var cashFlowByPeriod = IndexStatements(cashFlowRows, asset);

        var periodKeys = incomeByPeriod.Keys
            .Concat(balanceByPeriod.Keys)
            .Concat(cashFlowByPeriod.Keys)
            .Distinct()
            .OrderByDescending(key => key.PeriodEnd)
            .ThenByDescending(key => key.FiscalYear, StringComparer.Ordinal)
            .ToArray();

        var periods = periodKeys.Select(key =>
        {
            incomeByPeriod.TryGetValue(key, out var income);
            balanceByPeriod.TryGetValue(key, out var balance);
            cashFlowByPeriod.TryGetValue(key, out var cashFlow);

            var rows = new FmpStatementDto?[] { income, balance, cashFlow }
                .Where(row => row is not null)
                .Cast<FmpStatementDto>()
                .ToArray();
            var currencies = rows
                .Select(row => NormalizeOptional(row.ReportedCurrency))
                .Where(currency => currency is not null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (currencies.Length > 1)
            {
                throw InvalidResponse("Financial statements reported inconsistent currencies.");
            }

            var filingDate = rows
                .Select(row => row.FilingDate)
                .Where(date => date.HasValue)
                .Max();

            return new FinancialPeriod(
                key.PeriodEnd,
                key.FiscalYear,
                key.Period,
                filingDate,
                currencies.FirstOrDefault(),
                income?.Revenue,
                income?.GrossProfit,
                income?.OperatingIncome,
                income?.NetIncome,
                income?.Ebitda,
                income?.DilutedEps,
                income?.DilutedSharesOutstanding,
                balance?.CashAndCashEquivalents,
                balance?.TotalDebt,
                balance?.TotalAssets,
                balance?.TotalEquity,
                balance?.CurrentAssets,
                balance?.CurrentLiabilities,
                cashFlow?.OperatingCashFlow,
                cashFlow?.CapitalExpenditure,
                cashFlow?.FreeCashFlow);
        }).ToArray();

        return new CompanyFinancials(
            asset.Ticker,
            periods,
            DateTimeOffset.UtcNow,
            "FMP");
    }

    public async Task<CompanyMarketActivity?> GetCompanyMarketActivityAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var insidersTask = GetActivityDatasetAsync<FmpInsiderTransactionDto>(
            $"insider-trading/latest?page=0&limit={InsiderRequestLimit}",
            cancellationToken);
        var dividendsTask = GetActivityDatasetAsync<FmpDividendDto>(
            $"dividends?symbol={Uri.EscapeDataString(asset.Ticker)}",
            cancellationToken);

        await Task.WhenAll(insidersTask, dividendsTask);

        var insiderDataset = await insidersTask;
        var dividendDataset = await dividendsTask;
        if (insiderDataset.Failure is not null && dividendDataset.Failure is not null)
        {
            throw insiderDataset.Failure;
        }

        var insiders = insiderDataset.Rows
            .Where(row => string.Equals(row.Symbol?.Trim(), asset.Ticker, StringComparison.OrdinalIgnoreCase))
            .Select(row => MapInsiderTransaction(row, asset))
            .OrderByDescending(transaction => transaction.FilingDate)
            .ThenByDescending(transaction => transaction.TransactionDate)
            .Take(InsiderOutputLimit)
            .ToArray();
        var dividends = dividendDataset.Rows
            .Select(row => MapDividend(row, asset))
            .OrderByDescending(dividend => dividend.ExDividendDate)
            .Take(DividendOutputLimit)
            .ToArray();

        return new CompanyMarketActivity(
            asset.Ticker,
            insiders,
            dividends,
            insiderDataset.Failure is null,
            dividendDataset.Failure is null,
            DateTimeOffset.UtcNow,
            "FMP");
    }

    private async Task<ActivityDatasetResult<T>> GetActivityDatasetAsync<T>(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            return new ActivityDatasetResult<T>(
                await GetArrayAsync<T>(relativeUrl, cancellationToken),
                null);
        }
        catch (FinancialDataProviderException exception)
        {
            return new ActivityDatasetResult<T>([], exception);
        }
    }

    private async Task<TStatement[]> GetStatementsAsync<TStatement>(
        string endpoint,
        AssetIdentifier asset,
        CancellationToken cancellationToken)
        where TStatement : FmpStatementDto
        => await GetArrayAsync<TStatement>(
            $"{endpoint}?symbol={Uri.EscapeDataString(asset.Ticker)}&period=annual&limit={AnnualPeriodLimit}",
            cancellationToken);

    private async Task<T[]> GetArrayAsync<T>(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new FinancialDataProviderException(
                    "The financial data provider rate limit was reached.",
                    FinancialDataProviderFailure.RateLimited);
            }

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
            {
                return [];
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new FinancialDataProviderException(
                    "The financial data provider is temporarily unavailable.",
                    FinancialDataProviderFailure.Unavailable);
            }

            await response.Content.LoadIntoBufferAsync(MaximumResponseBytes, cancellationToken);
            return await response.Content.ReadFromJsonAsync<T[]>(cancellationToken) ?? [];
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FinancialDataProviderException(
                "The financial data provider request timed out.",
                FinancialDataProviderFailure.Timeout,
                exception);
        }
        catch (JsonException exception)
        {
            throw new FinancialDataProviderException(
                "The financial data provider returned malformed JSON.",
                FinancialDataProviderFailure.InvalidResponse,
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new FinancialDataProviderException(
                "The financial data provider could not be reached.",
                FinancialDataProviderFailure.Unavailable,
                exception);
        }
    }

    private static InsiderTransaction MapInsiderTransaction(
        FmpInsiderTransactionDto row,
        AssetIdentifier asset)
    {
        ValidateSymbol(row.Symbol, asset, "An insider transaction");
        if (!row.FilingDate.HasValue || string.IsNullOrWhiteSpace(row.ReportingName))
        {
            throw InvalidResponse("An insider transaction is missing filing identity.");
        }

        if (row.Price < 0 || row.SecuritiesOwned < 0)
        {
            throw InvalidResponse("An insider transaction returned invalid numeric values.");
        }

        decimal? securitiesTransacted = row.SecuritiesTransacted.HasValue
            ? Math.Abs(row.SecuritiesTransacted.Value)
            : null;
        decimal? transactionValue = securitiesTransacted.HasValue && row.Price.HasValue
            ? securitiesTransacted.Value * row.Price.Value
            : null;

        return new InsiderTransaction(
            row.FilingDate.Value,
            row.TransactionDate,
            row.ReportingName.Trim(),
            NormalizeOptional(row.TypeOfOwner),
            NormalizeOptional(row.TransactionType),
            NormalizeOptional(row.AcquisitionOrDisposition)?.ToUpperInvariant(),
            ClassifyInsiderTransaction(row.TransactionType),
            securitiesTransacted,
            row.Price,
            transactionValue,
            row.SecuritiesOwned,
            NormalizeOptional(row.SecurityName),
            "FMP",
            NormalizeProviderUrl(row.Url ?? row.Link));
    }

    private static DividendEvent MapDividend(FmpDividendDto row, AssetIdentifier asset)
    {
        ValidateSymbol(row.Symbol, asset, "A dividend event");
        if (!row.Date.HasValue)
        {
            throw InvalidResponse("A dividend event is missing its ex-dividend date.");
        }

        if (row.Dividend < 0 || row.AdjustedDividend < 0 || row.Yield < 0)
        {
            throw InvalidResponse("A dividend event returned invalid numeric values.");
        }

        return new DividendEvent(
            row.Date.Value,
            ParseOptionalDate(row.DeclarationDate, "declaration"),
            ParseOptionalDate(row.RecordDate, "record"),
            ParseOptionalDate(row.PaymentDate, "payment"),
            row.Dividend,
            row.AdjustedDividend,
            row.Yield,
            NormalizeOptional(row.Frequency));
    }

    private static void ValidateSymbol(string? symbol, AssetIdentifier asset, string dataset)
    {
        var normalizedSymbol = symbol?.Trim().ToUpperInvariant();
        if (!string.Equals(normalizedSymbol, asset.Ticker, StringComparison.Ordinal))
        {
            throw InvalidResponse($"{dataset} returned an inconsistent symbol.");
        }
    }

    private static DateOnly? ParseOptionalDate(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", out var date)
            ? date
            : throw InvalidResponse($"A dividend event returned an invalid {field} date.");
    }

    private static InsiderTransactionCategory ClassifyInsiderTransaction(string? transactionType)
    {
        var normalizedType = transactionType?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalizedType.Contains("GIFT", StringComparison.Ordinal))
        {
            return InsiderTransactionCategory.Gift;
        }

        if (normalizedType.Contains("AWARD", StringComparison.Ordinal)
            || normalizedType.Contains("GRANT", StringComparison.Ordinal))
        {
            return InsiderTransactionCategory.Award;
        }

        if (normalizedType.Contains("EXERCISE", StringComparison.Ordinal)
            || normalizedType.StartsWith("M-", StringComparison.Ordinal))
        {
            return InsiderTransactionCategory.Exercise;
        }

        if (normalizedType.Contains("PURCHASE", StringComparison.Ordinal)
            || normalizedType.StartsWith("P-", StringComparison.Ordinal))
        {
            return InsiderTransactionCategory.Purchase;
        }

        if (normalizedType.Contains("SALE", StringComparison.Ordinal)
            || normalizedType.StartsWith("S-", StringComparison.Ordinal))
        {
            return InsiderTransactionCategory.Sale;
        }

        // SEC direction A/D means acquired/disposed, not necessarily bought/sold
        // on the open market. Only an explicit transaction type earns that label.
        return InsiderTransactionCategory.Other;
    }

    private static string? NormalizeProviderUrl(string? value)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null)
        {
            return null;
        }

        return Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                ? uri.AbsoluteUri
                : null;
    }

    private static string? NormalizeLogoUrl(string? value)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null || !Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, "images.financialmodelingprep.com", StringComparison.OrdinalIgnoreCase)
                ? uri.AbsoluteUri
                : null;
    }

    private static CompanySearchResult? MapSearchResult(FmpSearchResultDto row)
    {
        if (!AssetIdentifier.TryCreate(row.Symbol, out var asset)
            || string.IsNullOrWhiteSpace(row.Name))
        {
            return null;
        }

        var name = row.Name.Trim();
        if (name.Length > 160)
        {
            return null;
        }

        return new CompanySearchResult(
            asset!.Ticker,
            name,
            LimitOptional(row.ExchangeShortName ?? row.Exchange ?? row.StockExchange, 80),
            LimitOptional(row.Currency, 12),
            "FMP");
    }

    private static string? LimitOptional(string? value, int maximumLength)
    {
        var normalized = NormalizeOptional(value);
        return normalized is not null && normalized.Length <= maximumLength ? normalized : null;
    }

    private static Dictionary<FinancialPeriodKey, TStatement> IndexStatements<TStatement>(
        IEnumerable<TStatement> rows,
        AssetIdentifier asset)
        where TStatement : FmpStatementDto
    {
        var result = new Dictionary<FinancialPeriodKey, TStatement>();

        foreach (var row in rows)
        {
            var symbol = row.Symbol?.Trim().ToUpperInvariant();
            if (!string.Equals(symbol, asset.Ticker, StringComparison.Ordinal))
            {
                throw InvalidResponse("A financial statement returned an inconsistent symbol.");
            }

            if (!row.Date.HasValue
                || string.IsNullOrWhiteSpace(row.FiscalYear)
                || string.IsNullOrWhiteSpace(row.Period))
            {
                throw InvalidResponse("A financial statement is missing period identity.");
            }

            var key = new FinancialPeriodKey(
                row.Date.Value,
                row.FiscalYear.Trim(),
                row.Period.Trim().ToUpperInvariant());

            if (!result.TryAdd(key, row))
            {
                throw InvalidResponse("Duplicate financial statements were returned for one period.");
            }
        }

        return result;
    }

    private static FinancialDataProviderException InvalidResponse(string message)
        => new(message, FinancialDataProviderFailure.InvalidResponse);

    private static AssetType ClassifyAsset(FmpProfileDto profile)
    {
        if (profile.IsEtf || profile.IsFund)
        {
            return AssetType.ExchangeTradedFund;
        }

        if (profile.Industry?.Contains("REIT", StringComparison.OrdinalIgnoreCase) == true)
        {
            return AssetType.RealEstateInvestmentTrust;
        }

        if (string.Equals(profile.Sector, "Financial Services", StringComparison.OrdinalIgnoreCase))
        {
            return AssetType.FinancialInstitution;
        }

        return AssetType.StandardCompany;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private readonly record struct FinancialPeriodKey(
        DateOnly PeriodEnd,
        string FiscalYear,
        string Period);

    private sealed record ActivityDatasetResult<T>(
        T[] Rows,
        FinancialDataProviderException? Failure);
}
