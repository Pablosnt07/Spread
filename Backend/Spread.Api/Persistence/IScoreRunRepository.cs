using Spread.Api.Domain.Scoring;

namespace Spread.Api.Persistence;

public interface IScoreRunRepository
{
    Task SaveAsync(ScoreRunSnapshot scoreRun, CancellationToken cancellationToken = default);
}

public sealed record ScoreRunSnapshot(
    Guid Id,
    Guid AssetId,
    Guid MarketDataSnapshotId,
    string ModelVersion,
    string AnchorVersion,
    SpreadScoreResult Result,
    DateTimeOffset CalculatedAt);
