# Backend architecture

## Decision

Spread starts as a modular monolith with two .NET projects: an API and tests. This keeps the learning surface small while retaining explicit boundaries for market data, scoring, persistence, and later user features.

```text
Browser / frontend
       |
       | HTTPS + JSON
       v
ASP.NET Core API
  |-- Features (thin HTTP seams)
  |-- Providers (external DTO -> normalized domain)
  |-- Scoring (pure deterministic calculations)
  |-- Persistence (snapshots and immutable score runs)
  `-- Future user modules (identity/profile/watchlist/portfolio)
       |
       +-- financial data provider
       `-- PostgreSQL (future implementation)
```

The backend is deployable independently from the Vercel frontend. A .NET-compatible host such as Azure, Render, or Railway is the expected backend target.

## Stable boundaries

- `IFinancialDataProvider` is the only route to vendor data. FMP DTOs must never become API contracts.
- Scoring accepts normalized inputs and has no HTTP, EF Core, provider, or user-profile dependency.
- `IScoreRunRepository` persists immutable, reproducible results.
- Public asset analysis remains anonymous and rate limited.
- Profile Match is a later calculation over published dimensions; it cannot modify Company Score.

## Delivery sequence

1. Foundation and scoring primitives (complete).
2. FMP company-profile proof, normalized contract, cache, and fixtures (complete).
3. FMP annual income, balance-sheet, and cash-flow snapshot (complete).
4. PostgreSQL snapshots, EF Core mappings, and migrations.
5. Six analyzers, anchors, peers, and deterministic signals.
6. Company analysis endpoints and caching/resilience.
7. Identity, profiles, watchlists, and portfolios only when their user-facing feature begins.

Redis, microservices, CQRS, event buses, and messaging are intentionally excluded until measured scale requires them.
