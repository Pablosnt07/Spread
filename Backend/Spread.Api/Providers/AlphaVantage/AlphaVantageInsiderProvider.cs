using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Spread.Api.Configuration;
using Spread.Api.Domain.Activity;
using Spread.Api.Domain.Assets;
using Spread.Api.Providers.Insiders;

namespace Spread.Api.Providers.AlphaVantage;

public sealed class AlphaVantageInsiderProvider(
    HttpClient httpClient,
    IOptions<AlphaVantageOptions> options) : IInsiderTransactionProvider
{
    private const long MaximumResponseBytes = 2_097_152;
    private const decimal MaximumShares = 1_000_000_000_000_000m;
    private const decimal MaximumPrice = 1_000_000_000m;

    public async Task<InsiderTransactionSnapshot?> GetInsiderTransactionsAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (!options.Value.Enabled)
        {
            return null;
        }

        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-options.Value.LookbackYears));
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"query?function=INSIDER_TRANSACTIONS&symbol={Uri.EscapeDataString(asset.Ticker)}&from={from:yyyy-MM-dd}");

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw ProviderFailure(
                    "The insider data provider rate limit was reached.",
                    FinancialDataProviderFailure.RateLimited);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw ProviderFailure(
                    "The insider data provider is temporarily unavailable.",
                    FinancialDataProviderFailure.Unavailable);
            }

            await response.Content.LoadIntoBufferAsync(MaximumResponseBytes, cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<AlphaVantageInsiderResponseDto>(cancellationToken);
            ValidatePayload(payload);

            var transactions = payload!.Data!
                .Select(row => TryMap(row, asset))
                .Where(transaction => transaction is not null)
                .Cast<InsiderTransaction>()
                .OrderByDescending(transaction => transaction.TransactionDate)
                .ThenByDescending(transaction => transaction.FilingDate)
                .Take(options.Value.OutputLimit)
                .ToArray();

            return new InsiderTransactionSnapshot(
                transactions,
                DateTimeOffset.UtcNow,
                "AlphaVantage");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw ProviderFailure(
                "The insider data provider request timed out.",
                FinancialDataProviderFailure.Timeout,
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw ProviderFailure(
                "The insider data provider could not be reached.",
                FinancialDataProviderFailure.Unavailable,
                exception);
        }
        catch (JsonException exception)
        {
            throw ProviderFailure(
                "The insider data provider returned invalid JSON.",
                FinancialDataProviderFailure.InvalidResponse,
                exception);
        }
    }

    private static void ValidatePayload(AlphaVantageInsiderResponseDto? payload)
    {
        if (!string.IsNullOrWhiteSpace(payload?.Note)
            || payload?.Information?.Contains("rate", StringComparison.OrdinalIgnoreCase) == true
            || payload?.Information?.Contains("frequency", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw ProviderFailure(
                "The insider data provider rate limit was reached.",
                FinancialDataProviderFailure.RateLimited);
        }

        if (!string.IsNullOrWhiteSpace(payload?.ErrorMessage))
        {
            throw ProviderFailure(
                "The insider data provider rejected the request.",
                FinancialDataProviderFailure.InvalidResponse);
        }

        if (payload?.Data is null)
        {
            throw ProviderFailure(
                "The insider data provider returned an incomplete response.",
                FinancialDataProviderFailure.InvalidResponse);
        }
    }

    private static InsiderTransaction? TryMap(JsonElement row, AssetIdentifier asset)
    {
        if (row.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var symbol = GetString(row, "ticker", "symbol");
        if (symbol is not null
            && !string.Equals(symbol.Trim(), asset.Ticker, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var reportingName = GetString(row, "executive", "executive_name", "owner_name");
        var transactionDate = GetDate(row, "transaction_date", "date");
        if (string.IsNullOrWhiteSpace(reportingName) || transactionDate is null)
        {
            return null;
        }

        var filingDate = GetDate(row, "filing_date", "report_date") ?? transactionDate.Value;
        var shares = GetBoundedDecimal(row, 0, MaximumShares, "shares", "transaction_shares");
        var price = GetBoundedDecimal(row, 0, MaximumPrice, "share_price", "price", "transaction_price_per_share");
        var transactionValue = GetBoundedDecimal(
            row,
            0,
            MaximumShares * MaximumPrice,
            "transaction_value");
        if (transactionValue is null && shares is not null && price is not null)
        {
            transactionValue = shares.Value * price.Value;
        }

        var direction = NormalizeDirection(GetString(row, "acquisition_or_disposal", "acquisition_or_disposition"));
        var rawTransactionType = GetString(row, "transaction_type");
        var displayTransactionType = rawTransactionType ?? direction switch
        {
            "A" => "Acquisition",
            "D" => "Disposition",
            _ => null
        };

        return new InsiderTransaction(
            filingDate,
            transactionDate,
            reportingName.Trim(),
            NormalizeOptional(GetString(row, "executive_title", "owner_title", "owner_relationship")),
            NormalizeOptional(displayTransactionType),
            direction,
            Classify(rawTransactionType),
            shares,
            price,
            transactionValue,
            GetBoundedDecimal(row, 0, MaximumShares, "shares_owned_after_transaction", "shares_owned"),
            NormalizeOptional(GetString(row, "security_type", "security_title", "security_name", "share_class")),
            "AlphaVantage",
            NormalizeFilingUrl(GetString(row, "filing_url")));
    }

    private static InsiderTransactionCategory Classify(string? transactionType)
    {
        var value = transactionType?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return InsiderTransactionCategory.Other;
        }

        if (value.Contains("purchase", StringComparison.OrdinalIgnoreCase)
            || value.Equals("P", StringComparison.OrdinalIgnoreCase))
        {
            return InsiderTransactionCategory.Purchase;
        }

        if (value.Contains("sale", StringComparison.OrdinalIgnoreCase)
            || value.Equals("S", StringComparison.OrdinalIgnoreCase))
        {
            return InsiderTransactionCategory.Sale;
        }

        if (value.Contains("award", StringComparison.OrdinalIgnoreCase)
            || value.Contains("grant", StringComparison.OrdinalIgnoreCase))
        {
            return InsiderTransactionCategory.Award;
        }

        if (value.Contains("exercise", StringComparison.OrdinalIgnoreCase))
        {
            return InsiderTransactionCategory.Exercise;
        }

        if (value.Contains("gift", StringComparison.OrdinalIgnoreCase))
        {
            return InsiderTransactionCategory.Gift;
        }

        return InsiderTransactionCategory.Other;
    }

    private static string? GetString(JsonElement row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return NormalizeOptional(value.GetString());
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.GetRawText();
            }
        }

        return null;
    }

    private static DateOnly? GetDate(JsonElement row, params string[] names)
    {
        var value = GetString(row, names);
        return DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date
            : null;
    }

    private static decimal? GetBoundedDecimal(
        JsonElement row,
        decimal minimum,
        decimal maximum,
        params string[] names)
    {
        var value = GetString(row, names);
        return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && parsed >= minimum
            && parsed <= maximum
            ? parsed
            : null;
    }

    private static string? NormalizeDirection(string? value)
        => value?.Trim().ToUpperInvariant() switch
        {
            "A" => "A",
            "D" => "D",
            _ => null
        };

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeFilingUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !(uri.Host.Equals("sec.gov", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".sec.gov", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return uri.AbsoluteUri;
    }

    private static FinancialDataProviderException ProviderFailure(
        string message,
        FinancialDataProviderFailure failure,
        Exception? innerException = null)
        => new(message, failure, innerException);
}
