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
```

Official documentation:

- https://site.financialmodelingprep.com/developer/docs
- https://site.financialmodelingprep.com/datasets/company-profile

Authentication uses the `apikey` HTTP request header. The key is not appended to URLs. Redirects are disabled so the authorization header cannot be forwarded to another host.

## Current normalization

The profile mapper preserves:

- ticker and company name;
- sector and industry;
- exchange, currency, and country;
- market capitalization and beta;
- active-trading state and website;
- provider logo URL when it uses HTTP or HTTPS;
- provider and fetch timestamp;
- preliminary asset classification.

ETF/funds, REITs, and financial institutions are classified as incompatible with the standard-company scoring model. This classification is a guardrail, not a full asset taxonomy.

The annual financial snapshot merges income, balance-sheet, and cash-flow rows by fiscal period. It currently preserves revenue, profits, EBITDA, diluted EPS and shares, cash, debt, assets, equity, current assets and liabilities, operating cash flow, capital expenditure, and free cash flow. Missing statements or values remain `null`; they are never coerced to zero.

The market-activity snapshot combines the most recent free insider feed with company dividend history. The FMP plan currently available to the project allows the global latest-insider feed (100 rows) but returns HTTP 402 for company-specific insider search. Spread therefore filters those 100 latest declarations by ticker and honestly returns an empty insider list when the company is absent. It does not simulate or backfill transactions. Purchases are classified only from explicit transaction types; acquisition/disposition direction alone is not treated as an open-market buy or sale.

Dividend rows preserve ex-dividend, declaration, record, and payment dates together with raw and adjusted dividend, yield, and frequency. Insider events and dividends are evidence-only datasets and never modify Company Score.

## Resilience implemented

- 15-second timeout;
- one-megabyte maximum buffered response;
- no automatic redirects;
- explicit 429, timeout, unavailable, and invalid-response categories;
- 24-hour in-memory profile cache;
- 12-hour in-memory annual-financials cache;
- one-hour in-memory market-activity cache;
- per-ticker single-flight locking to prevent duplicate concurrent calls.

No automatic retry is enabled yet. A future resilience policy must respect `Retry-After` and retry only demonstrably transient failures.

## Next datasets

The next provider slice will add TTM observations, ratios/key metrics, peer data, and historical prices. Each dataset will have an independent DTO, normalization rules, provenance, freshness, and cache policy. Company-specific insider history can replace the bounded latest-feed fallback if the FMP plan is upgraded.
