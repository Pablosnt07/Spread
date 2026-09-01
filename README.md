# spread

Backend foundation for **spread**, an educational stock fundamental analyzer. It transforms provider data into normalized observations and deterministic, explainable scores. It is not an investment adviser and does not emit buy/sell recommendations.

## Current slice

- .NET 10 ASP.NET Core API and xUnit tests.
- Health and versioned methodology endpoints.
- Live FMP company-profile integration, including validated company logos, through a normalized Spread contract.
- `GET /api/companies/{ticker}` with validation, rate limiting, and 24-hour cache.
- `GET /api/companies/{ticker}/financials` with five annual periods, strict normalization, and 12-hour cache.
- `GET /api/companies/{ticker}/activity` with optional company-specific Alpha Vantage insider history, FMP fallback, and dividend history.
- Pure 0–100 scoring primitives for six dimensions.blic

- Missing-dimension weight redistribution and confidence gating.
- Provider and immutable score-run persistence boundaries.
- Architecture for future PostgreSQL, identity, profiles, watchlists, and portfolios.
- Next.js interface with Mercado, company analysis, Portfolio, Watchlist, and Comparator views, including real FMP annual income, dividend, insider, and company-profile sections.
- Stateless portfolio allocation preview with unique asset count, total invested capital, and deterministic percentages.

No database, authentication, or OpenAI integration is enabled yet. FMP supplies normalized company profiles, annual financial statements, dividends, and bounded insider fallback data. Alpha Vantage can optionally supply company-specific insider history.

## Run locally

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project Backend/Spread.Api
```

Then request `GET /health`, `GET /api/methodology`, `GET /api/companies/AAPL`, `GET /api/companies/AAPL/financials`, `GET /api/companies/AAPL/activity`, or `POST /api/portfolios/allocation-preview`.

The allocation preview accepts amounts already converted to one base currency. It does not persist private positions; durable portfolios will require authentication and owner-scoped storage.

Run the frontend separately:

```bash
cd Frontend
npm install
npm run dev
```

Then open https://spread-pablosnt07-4v56bl3as-pablosantamariaxd-8385s-projects.vercel.app/. Company identity, annual income, dividends, and insider evidence use normalized FMP data; price history and the remaining unfinished analysis slices still use demonstration data.

Real provider keys must never be committed. Development uses the user-secret keys `FinancialData:Fmp:ApiKey` and `FinancialData:AlphaVantage:ApiKey`; hosting uses their double-underscore environment-variable equivalents. `.env.example` contains names only.

## Repository map

- `Backend/Spread.Api`: API, domain, scoring, providers, and persistence boundaries.
- `Backend/Spread.Tests`: deterministic unit tests that do not call external APIs.
- `Docs`: product architecture, scoring contract, data model, and security model.
- `Frontend`: Next.js product interface and reusable financial visualization components.
