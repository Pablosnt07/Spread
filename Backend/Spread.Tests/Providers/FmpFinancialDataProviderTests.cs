using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Spread.Api.Domain.Assets;
using Spread.Api.Domain.Activity;
using Spread.Api.Domain.Financials;
using Spread.Api.Domain.Companies;
using Spread.Api.Domain.MarketData;
using Spread.Api.Providers;
using Spread.Api.Providers.Fmp;

namespace Spread.Tests.Providers;

public sealed class FmpFinancialDataProviderTests
{
    [Fact]
    public async Task GetHistoricalPricesAsync_MapsSortsAndBoundsRequest()
    {
        const string json = """
            [
              {"symbol":"AAPL","date":"2026-08-22","price":231.5},
              {"symbol":"AAPL","date":"2026-08-21","price":229.2}
            ]
            """;
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = new FmpFinancialDataProvider(httpClient);

        var result = await provider.GetHistoricalPricesAsync(
            new AssetIdentifier("AAPL"),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 8, 24));

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateOnly(2026, 8, 21), result[0].Date);
        Assert.Equal(229.2m, result[0].Price);
        Assert.Equal("/stable/historical-price-eod/light", handler.RequestUri!.AbsolutePath);
        Assert.Equal("AAPL", GetQueryValue(handler.RequestUri, "symbol"));
        Assert.Equal("2026-01-01", GetQueryValue(handler.RequestUri, "from"));
        Assert.Equal("2026-08-24", GetQueryValue(handler.RequestUri, "to"));
        Assert.Null(GetQueryValue(handler.RequestUri, "apikey"));
        Assert.Equal("test-key", handler.ApiKeyHeader);
    }

    [Fact]
    public async Task GetHistoricalPricesAsync_RejectsInvalidProviderValues()
    {
        const string json = """[{"symbol":"AAPL","date":"2026-08-22","price":-1}]""";
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = new FmpFinancialDataProvider(httpClient);

        var exception = await Assert.ThrowsAsync<FinancialDataProviderException>(() =>
            provider.GetHistoricalPricesAsync(new AssetIdentifier("AAPL"), new DateOnly(2026, 1, 1), new DateOnly(2026, 8, 24)));

        Assert.Equal(FinancialDataProviderFailure.InvalidResponse, exception.Failure);
    }
    [Fact]
    public async Task SearchCompaniesAsync_MapsAndBoundsProviderResults()
    {
        const string json = """
            [
              {"symbol":"NOW","name":"ServiceNow, Inc.","exchangeShortName":"NYSE","currency":"USD"},
              {"symbol":"NOW","name":"Duplicate","exchangeShortName":"NYSE","currency":"USD"},
              {"symbol":"BAD<script>","name":"Unsafe"},
              {"symbol":"NOW2","name":"Second result","exchangeShortName":"NYSE","currency":"USD"}
            ]
            """;
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = new FmpFinancialDataProvider(httpClient);
        Assert.True(CompanySearchQuery.TryCreate("ServiceNow", out var query));

        var results = await provider.SearchCompaniesAsync(query!, 2);

        Assert.Equal(2, results.Count);
        Assert.Equal("NOW", results[0].Ticker);
        Assert.Equal("ServiceNow, Inc.", results[0].CompanyName);
        Assert.EndsWith("/search-name", handler.RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("ServiceNow", GetQueryValue(handler.RequestUri, "query"));
        Assert.Null(GetQueryValue(handler.RequestUri, "apikey"));
        Assert.Equal("test-key", handler.ApiKeyHeader);
    }

    [Fact]
    public async Task GetCompanyProfileAsync_MapsExternalDtoWithoutExposingProviderShape()
    {
        const string json = """
            [{
              "symbol": "AAPL",
              "companyName": "Apple Inc.",
              "sector": "Technology",
              "industry": "Consumer Electronics",
              "exchange": "NASDAQ",
              "currency": "USD",
              "country": "US",
              "marketCap": 3456789012345,
              "beta": 1.21,
              "website": "https://www.apple.com",
              "image": "https://images.financialmodelingprep.com/symbol/AAPL.png",
              "isEtf": false,
              "isFund": false,
              "isActivelyTrading": true
            }]
            """;

        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = new FmpFinancialDataProvider(httpClient);

        var result = await provider.GetCompanyProfileAsync(new AssetIdentifier("AAPL"));

        Assert.NotNull(result);
        Assert.Equal("AAPL", result.Ticker);
        Assert.Equal("Apple Inc.", result.CompanyName);
        Assert.Equal(AssetType.StandardCompany, result.AssetType);
        Assert.Equal(3_456_789_012_345m, result.MarketCapitalization);
        Assert.Equal("https://images.financialmodelingprep.com/symbol/AAPL.png", result.LogoUrl);
        Assert.Equal("FMP", result.Provider);
        Assert.Equal("AAPL", GetQueryValue(handler.RequestUri!, "symbol"));
        Assert.Null(GetQueryValue(handler.RequestUri!, "apikey"));
        Assert.Equal("test-key", handler.ApiKeyHeader);
    }

    [Fact]
    public async Task GetCompanyProfileAsync_ClassifiesUnsupportedFinancialCompany()
    {
        const string json = """
            [{
              "symbol": "JPM",
              "companyName": "JPMorgan Chase & Co.",
              "sector": "Financial Services",
              "industry": "Banks - Diversified",
              "isActivelyTrading": true
            }]
            """;

        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = new FmpFinancialDataProvider(httpClient);

        var result = await provider.GetCompanyProfileAsync(new AssetIdentifier("JPM"));

        Assert.NotNull(result);
        Assert.Equal(AssetType.FinancialInstitution, result.AssetType);
    }

    [Fact]
    public async Task GetCompanyProfileAsync_ClassifiesExchangeTradedFund()
    {
        const string json = """
            [{
              "symbol": "QQQ",
              "companyName": "Invesco QQQ Trust, Series 1",
              "sector": "Financial Services",
              "industry": "Asset Management",
              "isEtf": true,
              "isFund": true,
              "isActivelyTrading": true
            }]
            """;

        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = new FmpFinancialDataProvider(httpClient);

        var result = await provider.GetCompanyProfileAsync(new AssetIdentifier("QQQ"));

        Assert.NotNull(result);
        Assert.Equal(AssetType.ExchangeTradedFund, result.AssetType);
    }

    [Fact]
    public async Task GetCompanyProfileAsync_MapsRateLimitWithoutLeakingResponseBody()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        using var httpClient = CreateHttpClient(handler);
        var provider = new FmpFinancialDataProvider(httpClient);

        var exception = await Assert.ThrowsAsync<FinancialDataProviderException>(() =>
            provider.GetCompanyProfileAsync(new AssetIdentifier("AAPL")));

        Assert.Equal(FinancialDataProviderFailure.RateLimited, exception.Failure);
    }

    [Fact]
    public async Task GetCompanyProfileAsync_RejectsMismatchedTicker()
    {
        const string json = """[{"symbol":"MSFT","companyName":"Microsoft Corporation"}]""";
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = new FmpFinancialDataProvider(httpClient);

        var exception = await Assert.ThrowsAsync<FinancialDataProviderException>(() =>
            provider.GetCompanyProfileAsync(new AssetIdentifier("AAPL")));

        Assert.Equal(FinancialDataProviderFailure.InvalidResponse, exception.Failure);
    }

    [Fact]
    public async Task GetCompanyProfileAsync_DropsUnsafeLogoUrl()
    {
        const string json = """[{"symbol":"AAPL","companyName":"Apple Inc.","image":"javascript:alert(1)"}]""";
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = new FmpFinancialDataProvider(httpClient);

        var result = await provider.GetCompanyProfileAsync(new AssetIdentifier("AAPL"));

        Assert.NotNull(result);
        Assert.Null(result.LogoUrl);
    }

    [Fact]
    public async Task GetCompanyProfileAsync_DropsLogoFromUntrustedHttpsHost()
    {
        const string json = """[{"symbol":"AAPL","companyName":"Apple Inc.","image":"https://attacker.example/tracker.png"}]""";
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = new FmpFinancialDataProvider(httpClient);

        var result = await provider.GetCompanyProfileAsync(new AssetIdentifier("AAPL"));

        Assert.NotNull(result);
        Assert.Null(result.LogoUrl);
    }

    [Fact]
    public async Task GetCompanyFinancialsAsync_MergesStatementsByFiscalPeriod()
    {
        var responses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/stable/income-statement"] = """
                [{"date":"2025-09-27","symbol":"AAPL","reportedCurrency":"USD","fiscalYear":"2025","period":"FY","filingDate":"2025-10-31","revenue":416161000000,"grossProfit":195201000000,"operatingIncome":133050000000,"netIncome":112010000000,"ebitda":144427000000,"epsDiluted":7.46,"weightedAverageShsOutDil":15004697000}]
                """,
            ["/stable/balance-sheet-statement"] = """
                [{"date":"2025-09-27","symbol":"AAPL","reportedCurrency":"USD","fiscalYear":"2025","period":"FY","filingDate":"2025-10-31","cashAndCashEquivalents":35934000000,"totalDebt":98657000000,"totalAssets":359241000000,"totalStockholdersEquity":73733000000,"totalCurrentAssets":147957000000,"totalCurrentLiabilities":165631000000}]
                """,
            ["/stable/cash-flow-statement"] = """
                [{"date":"2025-09-27","symbol":"AAPL","reportedCurrency":"USD","fiscalYear":"2025","period":"FY","filingDate":"2025-10-31","netCashProvidedByOperatingActivities":135471000000,"capitalExpenditure":-12715000000,"freeCashFlow":122756000000}]
                """
        };
        var handler = new RoutingHandler(responses);
        using var httpClient = CreateHttpClient(handler);
        var provider = new FmpFinancialDataProvider(httpClient);

        var result = await provider.GetCompanyFinancialsAsync(new AssetIdentifier("AAPL"));

        Assert.NotNull(result);
        var period = Assert.Single(result.Periods);
        Assert.Equal(new DateOnly(2025, 9, 27), period.PeriodEnd);
        Assert.Equal("USD", period.ReportedCurrency);
        Assert.Equal(416_161_000_000m, period.Revenue);
        Assert.Equal(98_657_000_000m, period.TotalDebt);
        Assert.Equal(122_756_000_000m, period.FreeCashFlow);
        Assert.Equal(3, handler.RequestUris.Count);
        Assert.All(handler.RequestUris, uri =>
        {
            Assert.Equal("annual", GetQueryValue(uri, "period"));
            Assert.Equal("5", GetQueryValue(uri, "limit"));
            Assert.Null(GetQueryValue(uri, "apikey"));
        });
    }

    [Fact]
    public async Task GetCompanyFinancialsAsync_RejectsInconsistentCurrency()
    {
        var responses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/stable/income-statement"] = """[{"date":"2025-12-31","symbol":"TEST","reportedCurrency":"USD","fiscalYear":"2025","period":"FY"}]""",
            ["/stable/balance-sheet-statement"] = """[{"date":"2025-12-31","symbol":"TEST","reportedCurrency":"EUR","fiscalYear":"2025","period":"FY"}]""",
            ["/stable/cash-flow-statement"] = """[{"date":"2025-12-31","symbol":"TEST","reportedCurrency":"USD","fiscalYear":"2025","period":"FY"}]"""
        };
        var handler = new RoutingHandler(responses);
        using var httpClient = CreateHttpClient(handler);
        var provider = new FmpFinancialDataProvider(httpClient);

        var exception = await Assert.ThrowsAsync<FinancialDataProviderException>(() =>
            provider.GetCompanyFinancialsAsync(new AssetIdentifier("TEST")));

        Assert.Equal(FinancialDataProviderFailure.InvalidResponse, exception.Failure);
    }

    [Fact]
    public async Task GetCompanyFinancialsAsync_PreservesMissingStatementsAsNull()
    {
        var responses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/stable/income-statement"] =
                """[{"date":"2025-12-31","symbol":"TEST","reportedCurrency":"USD","fiscalYear":"2025","period":"FY","revenue":100}]""",
            ["/stable/balance-sheet-statement"] = "[]",
            ["/stable/cash-flow-statement"] = "[]"
        };
        var handler = new RoutingHandler(responses);
        using var httpClient = CreateHttpClient(handler);
        var provider = new FmpFinancialDataProvider(httpClient);

        var result = await provider.GetCompanyFinancialsAsync(new AssetIdentifier("TEST"));

        var period = Assert.Single(Assert.IsType<CompanyFinancials>(result).Periods);
        Assert.Equal(100m, period.Revenue);
        Assert.Null(period.TotalAssets);
        Assert.Null(period.OperatingCashFlow);
        Assert.Null(period.FreeCashFlow);
    }

    [Fact]
    public async Task GetCompanyMarketActivityAsync_MapsRecentInsidersAndDividends()
    {
        var responses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/stable/insider-trading/latest"] = """
                [{"symbol":"AAPL","filingDate":"2026-08-20","transactionDate":"2026-08-19","reportingName":"Test Officer","typeOfOwner":"officer","transactionType":"P-Purchase","acquisitionOrDisposition":"A","securitiesTransacted":1000,"price":230.5,"securitiesOwned":5000,"securityName":"Common Stock","url":"https://www.sec.gov/test"}]
                """,
            ["/stable/dividends"] = """
                [{"symbol":"AAPL","date":"2026-08-10","declarationDate":"2026-07-31","recordDate":"2026-08-11","paymentDate":"2026-08-14","dividend":0.26,"adjDividend":0.26,"yield":0.0042,"frequency":"Quarterly"}]
                """
        };
        var handler = new RoutingHandler(responses);
        using var httpClient = CreateHttpClient(handler);
        var provider = new FmpFinancialDataProvider(httpClient);

        var result = await provider.GetCompanyMarketActivityAsync(new AssetIdentifier("AAPL"));

        Assert.NotNull(result);
        Assert.True(result.InsiderDataAvailable);
        Assert.True(result.DividendDataAvailable);
        var insider = Assert.Single(result.InsiderTransactions);
        Assert.Equal(InsiderTransactionCategory.Purchase, insider.Category);
        Assert.Equal(1_000m, insider.SecuritiesTransacted);
        Assert.Equal(230_500m, insider.TransactionValue);
        Assert.Equal("https://www.sec.gov/test", insider.FilingUrl);
        var dividend = Assert.Single(result.Dividends);
        Assert.Equal(new DateOnly(2026, 8, 10), dividend.ExDividendDate);
        Assert.Equal(0.26m, dividend.AdjustedDividend);
        Assert.Equal("Quarterly", dividend.Frequency);
        Assert.Equal(2, handler.RequestUris.Count);
        Assert.All(handler.RequestUris, uri => Assert.Null(GetQueryValue(uri, "apikey")));
        var insiderRequest = Assert.Single(handler.RequestUris, uri => uri.AbsolutePath.EndsWith("/insider-trading/latest", StringComparison.Ordinal));
        Assert.Null(GetQueryValue(insiderRequest, "symbol"));
        Assert.Equal("100", GetQueryValue(insiderRequest, "limit"));
    }

    [Fact]
    public async Task GetCompanyMarketActivityAsync_DoesNotTreatAcquisitionDirectionAsPurchase()
    {
        var responses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/stable/insider-trading/latest"] = """
                [{"symbol":"AAPL","filingDate":"2026-08-20","reportingName":"Test Officer","transactionType":"F-Tax withholding","acquisitionOrDisposition":"A"}]
                """,
            ["/stable/dividends"] = "[]"
        };
        var handler = new RoutingHandler(responses);
        using var httpClient = CreateHttpClient(handler);
        var provider = new FmpFinancialDataProvider(httpClient);

        var result = await provider.GetCompanyMarketActivityAsync(new AssetIdentifier("AAPL"));

        var insider = Assert.Single(Assert.IsType<CompanyMarketActivity>(result).InsiderTransactions);
        Assert.Equal(InsiderTransactionCategory.Other, insider.Category);
    }

    [Fact]
    public async Task GetCompanyMarketActivityAsync_PreservesAvailableDatasetWhenOtherIsUnavailable()
    {
        var handler = new MixedActivityHandler();
        using var httpClient = CreateHttpClient(handler);
        var provider = new FmpFinancialDataProvider(httpClient);

        var result = await provider.GetCompanyMarketActivityAsync(new AssetIdentifier("GDRX"));

        Assert.NotNull(result);
        Assert.True(result.InsiderDataAvailable);
        Assert.False(result.DividendDataAvailable);
        Assert.Single(result.InsiderTransactions);
        Assert.Empty(result.Dividends);
    }

    [Fact]
    public async Task GetCompanyMarketActivityAsync_TreatsSuccessfulEmptyDatasetsAsAvailable()
    {
        var responses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/stable/insider-trading/latest"] = "[]",
            ["/stable/dividends"] = "[]"
        };
        var handler = new RoutingHandler(responses);
        using var httpClient = CreateHttpClient(handler);
        var provider = new FmpFinancialDataProvider(httpClient);

        var result = await provider.GetCompanyMarketActivityAsync(new AssetIdentifier("MELI"));

        Assert.NotNull(result);
        Assert.True(result.InsiderDataAvailable);
        Assert.True(result.DividendDataAvailable);
        Assert.Empty(result.InsiderTransactions);
        Assert.Empty(result.Dividends);
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/stable/")
        };
        client.DefaultRequestHeaders.Add("apikey", "test-key");
        return client;
    }

    private static string? GetQueryValue(Uri uri, string key)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (string.Equals(parts[0], key, StringComparison.Ordinal))
            {
                return parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            }
        }

        return null;
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? ApiKeyHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            ApiKeyHeader = request.Headers.TryGetValues("apikey", out var values)
                ? values.Single()
                : null;
            return Task.FromResult(responder(request));
        }
    }

    private sealed class RoutingHandler(IReadOnlyDictionary<string, string> responses)
        : HttpMessageHandler
    {
        public ConcurrentBag<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            RequestUris.Add(uri);
            var response = responses.TryGetValue(uri.AbsolutePath, out var json)
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
            return Task.FromResult(response);
        }
    }

    private sealed class MixedActivityHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = request.RequestUri!.AbsolutePath.EndsWith("/insider-trading/latest", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """[{"symbol":"GDRX","filingDate":"2026-08-21","reportingName":"Test Owner","transactionType":"C-Conversion","securitiesTransacted":10,"price":0}]""",
                        Encoding.UTF8,
                        "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.PaymentRequired);
            return Task.FromResult(response);
        }
    }
}
