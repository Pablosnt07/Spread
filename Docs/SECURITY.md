# Security model

## Assets and trust boundaries

Protected assets include provider and database credentials, provider quota/cost, immutable scoring inputs and versions, future account data, watchlists, and private portfolio positions. Trust boundaries exist at browser/API, API/provider, API/database, identity-provider/API, CI/hosting, and external-data/scoring transitions.

All request data and all provider payloads are untrusted. Tickers, ranges, filters, JSON, JWTs, headers, financial values, periods, units, currencies, and external error bodies require validation and bounds.

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

OpenAI is not part of the scoring pipeline or this backend slice. If a future feature genuinely needs the OpenAI API, its credential setup requires a separate explicit decision and server-side secret storage.

The FMP client sends its credential only through the `apikey` request header. Redirects are disabled, response bodies are bounded, provider DTOs are never returned directly, and logs contain ticker/provider metadata without the credential.

Repository: /home/pablo/Documents/ChatGPT/Spread
Version: initial-uncommitted-foundation
