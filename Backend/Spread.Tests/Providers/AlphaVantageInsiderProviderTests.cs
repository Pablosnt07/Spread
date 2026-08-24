using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Spread.Api.Configuration;
using Spread.Api.Domain.Activity;
using Spread.Api.Domain.Assets;
using Spread.Api.Providers;
using Spread.Api.Providers.AlphaVantage;

namespace Spread.Tests.Providers;

public sealed class AlphaVantageInsiderProviderTests
{
    [Fact]
    public async Task GetInsiderTransactionsAsync_MapsHistoricalTransactionsForTicker()
    {
        const string json = """
            {
              "data": [
                {
                  "transaction_date": "2026-08-20",
                  "filing_date": "2026-08-21",
                  "ticker": "AAPL",
                  "executive": "Example Executive",
                  "executive_title": "Chief Financial Officer",
                  "security_type": "Common Stock",
                  "acquisition_or_disposal": "A",
                  "transaction_type": "Purchase",
                  "shares": "1250",
                  "share_price": "220.50",
                  "shares_owned_after_transaction": "20000"
                }
              ]
            }
            """;
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        using var client = CreateClient(handler);
        var provider = new AlphaVantageInsiderProvider(client, EnabledOptions());

        var result = await provider.GetInsiderTransactionsAsync(new AssetIdentifier("AAPL"));

        Assert.NotNull(result);
        var transaction = Assert.Single(result.Transactions);
        Assert.Equal("Example Executive", transaction.ReportingName);
        Assert.Equal(InsiderTransactionCategory.Purchase, transaction.Category);
        Assert.Equal(1_250m, transaction.SecuritiesTransacted);
        Assert.Equal(220.50m, transaction.Price);
        Assert.Equal(275_625m, transaction.TransactionValue);
        Assert.Equal("AlphaVantage", result.Provider);
        Assert.Contains("function=INSIDER_TRANSACTIONS", handler.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("symbol=AAPL", handler.RequestUri.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("apikey", handler.RequestUri.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetInsiderTransactionsAsync_DoesNotInferPurchaseFromAcquisitionDirection()
    {
        const string json = """
            {"data":[{"transaction_date":"2026-08-20","ticker":"MSFT","executive":"Example Officer","acquisition_or_disposal":"A","shares":"10","share_price":"0"}]}
            """;
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        using var client = CreateClient(handler);
        var provider = new AlphaVantageInsiderProvider(client, EnabledOptions());

        var result = await provider.GetInsiderTransactionsAsync(new AssetIdentifier("MSFT"));

        var transaction = Assert.Single(result!.Transactions);
        Assert.Equal(InsiderTransactionCategory.Other, transaction.Category);
        Assert.Equal("Acquisition", transaction.TransactionType);
    }

    [Fact]
    public async Task GetInsiderTransactionsAsync_MapsProviderNoteToRateLimit()
    {
        const string json = """{"Note":"API call frequency exceeded."}""";
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        using var client = CreateClient(handler);
        var provider = new AlphaVantageInsiderProvider(client, EnabledOptions());

        var exception = await Assert.ThrowsAsync<FinancialDataProviderException>(() =>
            provider.GetInsiderTransactionsAsync(new AssetIdentifier("AAPL")));

        Assert.Equal(FinancialDataProviderFailure.RateLimited, exception.Failure);
        Assert.DoesNotContain("API call frequency", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetInsiderTransactionsAsync_WhenDisabled_DoesNotCallProvider()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("No request expected."));
        using var client = CreateClient(handler);
        var provider = new AlphaVantageInsiderProvider(
            client,
            Options.Create(new AlphaVantageOptions { Enabled = false }));

        var result = await provider.GetInsiderTransactionsAsync(new AssetIdentifier("AAPL"));

        Assert.Null(result);
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public async Task ApiKeyHandler_RestoresRedactedRequestUriAfterSending()
    {
        var innerHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var apiKeyHandler = new AlphaVantageApiKeyHandler(EnabledOptions())
        {
            InnerHandler = innerHandler
        };
        using var invoker = new HttpMessageInvoker(apiKeyHandler);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://www.alphavantage.co/query?function=INSIDER_TRANSACTIONS&symbol=AAPL");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Contains("apikey=test-alpha-key", innerHandler.RequestUri!.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("apikey", request.RequestUri!.Query, StringComparison.OrdinalIgnoreCase);
    }

    private static IOptions<AlphaVantageOptions> EnabledOptions()
        => Options.Create(new AlphaVantageOptions
        {
            Enabled = true,
            ApiKey = "test-alpha-key",
            LookbackYears = 5,
            OutputLimit = 20
        });

    private static HttpClient CreateClient(HttpMessageHandler handler)
        => new(handler)
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(responseFactory(request));
        }
    }
}
