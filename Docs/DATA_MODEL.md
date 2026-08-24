# Persistence and future modules

PostgreSQL is the intended durable store. EF Core/Npgsql and the first migration will be added with the provider integration so the schema is proven against real normalized data instead of guessed payloads.

## Core immutable analysis data

```text
assets
  id UUID PK
  ticker, exchange, asset_type, currency
  name, sector, industry, stable_identifier

market_data_snapshots
  id UUID PK, asset_id FK
  provider, as_of, fetched_at, coverage
  normalization_version, raw_payload_reference nullable

metric_observations
  id UUID PK, snapshot_id FK
  metric_name, value, unit, currency nullable
  fiscal_period, period_end, published_at
  status, source

algorithm_versions
  id UUID PK, version UNIQUE
  anchor_version, definition_hash, created_at

company_score_runs
  id UUID PK, asset_id FK, snapshot_id FK, algorithm_version_id FK
  final_score nullable, coverage, confidence, status, calculated_at

company_score_components
  id UUID PK, score_run_id FK
  dimension, score, configured_weight, effective_weight, contribution
```

Snapshots, algorithm versions, runs, and components are append-only. A uniqueness constraint over snapshot + algorithm version prevents accidental duplicate runs.

## Future private data

```text
app_users(id UUID PK, auth_subject UNIQUE, created_at)
investor_profiles(id, user_id UNIQUE FK, preferences..., timestamps)
watchlists(id, user_id FK, name, timestamps)
watchlist_items(id, watchlist_id FK, asset_id FK, created_at, UNIQUE(watchlist_id, asset_id))
portfolios(id, user_id FK, name, timestamps)
portfolio_positions(id, portfolio_id FK, asset_id FK, quantity, average_cost, timestamps)
```

Spread will not store passwords. A future auth provider owns credentials; the validated JWT `sub` maps to `app_users.auth_subject`. Every private query is scoped by the authenticated owner, with database constraints and cross-user authorization tests. Portfolio values use bounded `decimal`, never floating point.

The runtime database role will have least privilege; the migration role is separate. Supabase Data API remains disabled for business tables unless a future feature explicitly needs it and adds RLS plus ownership policies.
