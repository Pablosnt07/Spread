# Financial data providers

## Financial Modeling Prep

FMP is the initial provider behind `IFinancialDataProvider`. Spread never exposes an FMP DTO directly; every response is validated and mapped into a domain model before it reaches an API contract.

Current stable endpoints:

```text
GET https://financialmodelingprep.com/stable/profile?symbol=AAPL
GET https://financialmodelingprep.com/stable/income-statement?symbol=AAPL&period=annual&limit=5
GET https://financialmodelingprep.com/stable/balance-sheet-statement?symbol=AAPL&period=annual&limit=5
GET https://financialmodelingprep.com/stable/cash-flow-statement?symbol=AAPL&period=annual&limit=5
GET https://financialmodelingprep.com/stable/insider-trading/latest?page=0&limit=100
GET https://financialmodelingprep.com/stable/dividends?symbol=AAPL
GET https://financialmodelingprep.com/stable/search-symbol?query=AAPL
GET https://financialmodelingprep.com/stable/search-name?query=Apple
GET https://financialmodelingprep.com/stable/historical-price-eod/light?symbol=AAPL&from=2025-08-24&to=2026-08-24
```

Official documentation:

- https://site.financialmodelingprep.com/developer/docs
- https://site.financialmodelingprep.com/datasets/company-profile

Authentication uses the `apikey` HTTP request header. The key is not appended to URLs. Redirects are disabled so the authorization header cannot be forwarded to another host.

## Alpha Vantage insiders

Alpha Vantage is an optional, specialized source for company-specific insider history. It does not replace FMP for profiles, statements, or dividends.

```text
GET https://www.alphavantage.co/query?function=INSIDER_TRANSACTIONS&symbol=AAPL&from=2021-08-24
```

Official documentation: https://www.alphavantage.co/documentation/

The provider requests a bounded five-year window and publishes at most 20 normalized movements. Alpha Vantage requires `apikey` in the query string, so Spread appends it inside an inner HTTP handler, restores the redacted URI after sending, disables redirects, and suppresses framework-level informational HTTP logs. Exceptions and application logs never include the upstream URL, response body, or credential.

Alpha Vantage is preferred for insider movements when enabled. FMP's bounded latest feed remains the fallback if Alpha Vantage is disabled, unavailable, or rate limited. Dividends continue to come from FMP. Each transaction carries its own normalized source identifier.

## Current normalization

The profile mapper preserves:

- ticker and company name;
- sector and industry;
- exchange, currency, and country;
- market capitalization and beta;
- active-trading state and website;
- provider logo URL only when it uses HTTPS on `images.financialmodelingprep.com`;
- provider and fetch timestamp;
- preliminary asset classification.

ETF/funds, REITs, and financial institutions are classified as incompatible with the standard-company scoring model. This classification is a guardrail, not a full asset taxonomy.

The annual financial snapshot merges income, balance-sheet, and cash-flow rows by fiscal period. It currently preserves revenue, profits, EBITDA, diluted EPS and shares, cash, debt, assets, equity, current assets and liabilities, operating cash flow, capital expenditure, and free cash flow. Missing statements or values remain `null`; they are never coerced to zero.

The market-activity snapshot combines company-specific Alpha Vantage insider history when configured with FMP dividend history. The FMP plan currently available to the project allows only the global latest-insider feed (100 rows), so that feed is retained as a fallback. Spread does not simulate or backfill transactions. Purchases are classified only from explicit transaction types; acquisition/disposition direction alone is not treated as an open-market buy or sale.

Dividend rows preserve ex-dividend, declaration, record, and payment dates together with raw and adjusted dividend, yield, and frequency. Insider events and dividends are evidence-only datasets and never modify Company Score.

## Resilience implemented

- 15-second timeout;
- one-megabyte maximum buffered response;
- no automatic redirects;
- explicit 429, timeout, unavailable, and invalid-response categories;
- 24-hour in-memory profile cache;
- 12-hour in-memory annual-financials cache;
- 24-hour in-memory market-activity cache to protect Alpha Vantage's daily quota;
- six-hour historical-price cache, maximum 25-year window and at most 320 normalized points;
- per-ticker single-flight locking to prevent duplicate concurrent calls.
- bounded company-search cache with 15-minute positive and 2-minute empty-result TTLs;
- five-minute negative profile caching and a maximum of two concurrent provider search calls.

Historical-price calls share a single outbound concurrency slot. A provider `429` receives one bounded retry after one second; no other failures are retried. This prevents a portfolio-plus-benchmark chart from creating a provider burst or retry storm.

## Next datasets

The next provider slice will add TTM observations, ratios/key metrics, and peer data. Each dataset will have an independent DTO, normalization rules, provenance, freshness, and cache policy. A persistent distributed cache is required before scaling provider traffic across multiple backend instances.
