# Security model

## Assets and trust boundaries

Protected assets include provider and database credentials, provider quota/cost, immutable scoring inputs and versions, future account data, watchlists, and private portfolio positions. Trust boundaries exist at browser/API, API/provider, API/database, identity-provider/API, CI/hosting, and external-data/scoring transitions.

All request data and all provider payloads are untrusted. Tickers, ranges, filters, JSON, JWTs, headers, financial values, periods, units, currencies, and external error bodies require validation and bounds. Provider credentials belong only in user-secrets or the hosting secret store; Alpha Vantage query authentication is added inside a redacting handler and provider HTTP informational logs are disabled.

## Invariants

- Secrets, tokens, connection strings, provider URLs containing keys, and private bodies never enter logs or API errors.
- The Company Score depends only on normalized snapshot data and immutable versioned configuration.
- Missing/invalid data is not zero; unsupported assets never receive a standard score.
- Future private rows are authorized from a cryptographically validated token subject, never a body-supplied user ID.
- One request cannot trigger unbounded provider calls, response sizes, retries, or date ranges.
- Historical score runs are immutable and traceable.

## Baseline controls

- Local secrets: `dotnet user-secrets`; production secrets: hosting secret store. No real value belongs in `.env.example`.
- Public reads are rate limited; analysis refresh will receive a stricter partition plus provider concurrency limit.
- Outbound HTTP will use `IHttpClientFactory`, fixed provider base URLs, cancellation, timeouts, bounded responses, and limited transient retries respecting `Retry-After`.
- Errors use Problem Details without stack traces or upstream payloads in production.
- CORS will use exact environment-specific origins before frontend integration; no wildcard with credentials.
- Database runtime credentials use least privilege and encrypted connections.
- CI will restore, build, test, audit dependencies, and scan committed content for secrets before deployment.

## Company-search controls

- Queries are normalized and limited to 2–64 ASCII characters from a strict allowlist. Markup, slashes, control characters, and oversized limits are rejected before contacting FMP.
- The public contract returns at most 8 normalized results and never exposes provider DTOs, request URLs, credentials, or upstream error bodies.
- The Next route allows 12 searches per observed client per minute; the backend applies a second fixed-window limit, while the search service globally caps concurrent FMP lookups at 2.
- A 320 ms browser debounce, per-query single-flight, a bounded 500-entry cache, 15-minute positive caching, and 2-minute empty-result caching reduce spam and provider cost.
- Profiles not found are negatively cached for 5 minutes so random ticker scans cannot repeatedly consume provider quota.
- Internal `System.Diagnostics.Metrics` instruments requests, validation/rate rejections, cache hits, provider calls, and provider latency. These metrics are intentionally not published by a public endpoint.
- Provider logo URLs are accepted only over HTTPS from `images.financialmodelingprep.com`; arbitrary tracking/image hosts are discarded.
- The web app sends `nosniff`, frame-denial, strict referrer, and restrictive permissions headers. Kestrel's identifying `Server` header is disabled.

The in-process Next limiter is a defense-in-depth control for the MVP. Before horizontal production scaling, it must be backed by a durable edge/distributed rate limiter because separate serverless instances do not share memory.

OpenAI is not part of the scoring pipeline or this backend slice. If a future feature genuinely needs the OpenAI API, its credential setup requires a separate explicit decision and server-side secret storage.

The FMP client sends its credential only through the `apikey` request header. Redirects are disabled, response bodies are bounded, provider DTOs are never returned directly, and search logs contain only query length/result count—not the raw input or credential.

Repository: /home/pablo/Documents/ChatGPT/Spread
Version: initial-uncommitted-foundation
