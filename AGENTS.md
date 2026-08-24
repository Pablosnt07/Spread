# Spread development rules

## Backend

- Use .NET 10, ASP.NET Core, C#, and a simple modular monolith.
- Keep HTTP endpoints thin. Business logic belongs in services and scoring.
- Keep external provider DTOs separate from Spread domain models.
- Use `CancellationToken` for all I/O and never expose provider keys.

## Scoring

- `Docs/SCORING.md` is the canonical scoring contract.
- Company Score never depends on investor profile or an LLM.
- Missing, invalid, and not-applicable data are not zero.
- Stocks and incompatible asset types use different engines.
- Every calculation is deterministic, versioned, explainable, and tested.
- Never silently change weights, anchors, or publication thresholds.

## Future user data

- Profile Match remains separate from Company Score.
- Authentication owns identity only; never store passwords in Spread.
- Every profile, watchlist, and portfolio operation must enforce ownership from the validated token subject, never a request body user ID.

## Workflow and security

- Read repository docs and inspect Git status before structural changes.
- Preserve user changes and keep each change reviewable.
- Never commit secrets, tokens, connection strings, or raw credentials.
- Run build and tests before handoff.
