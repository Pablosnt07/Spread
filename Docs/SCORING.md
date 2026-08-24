# Scoring contract

`SCORING.md` supplied on 2026-08-23 is the canonical methodology. Scores are calculated internally on `0..100`; a UI may display `score / 10` as presentation only.

## Standard company model v0.1.0

| Dimension | Weight |
|---|---:|
| Quality | 22% |
| Growth | 18% |
| Profitability | 18% |
| Valuation | 18% |
| Financial Health | 14% |
| Risk (higher means safer) | 10% |

```text
SpreadScore = sum(available dimension weight * score)
              / sum(available dimension weights)
```

Metric normalization uses `0.45 * AbsoluteScore + 0.55 * PeerScore`. When comparable peers are unavailable, the absolute score is used and peer confidence falls. Absolute anchors and internal metric weights are experimental until calibrated; they must be added as versioned configuration, never hidden constants.

## Publication and confidence

Missing dimensions and metrics are excluded, not converted to zero. Remaining weights are renormalized. Current provisional minimum weighted coverage is 70%.

```text
Confidence = 100 * (
  0.45 * Coverage +
  0.20 * Freshness +
  0.20 * PeerQuality +
  0.15 * Consistency)
```

Confidence below 40 or coverage below the configured minimum returns `InsufficientData` with no definitive score. Calculations preserve decimal precision and round only for presentation.

## Non-negotiable exclusions

- Negative or meaningless P/E, PEG, and EV/EBITDA are unavailable, not attractive.
- CAGR is unavailable across negative, near-zero, or sign-changing bases unless a separately documented rule applies.
- Market momentum, RSI, news, sentiment, analyst targets, and isolated insider events do not alter the fundamental score.
- ETFs, REITs, financial institutions, unusual holdings, and pre-revenue assets do not use this standard model.

Every score run records model/anchor version, exact input snapshot, excluded metrics and reasons, data dates, provider, peer definition, and unrounded outputs.
