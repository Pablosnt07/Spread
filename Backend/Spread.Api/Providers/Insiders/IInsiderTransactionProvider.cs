using Spread.Api.Domain.Activity;
using Spread.Api.Domain.Assets;

namespace Spread.Api.Providers.Insiders;

public interface IInsiderTransactionProvider
{
    Task<InsiderTransactionSnapshot?> GetInsiderTransactionsAsync(
        AssetIdentifier asset,
        CancellationToken cancellationToken = default);
}

public sealed record InsiderTransactionSnapshot(
    IReadOnlyList<InsiderTransaction> Transactions,
    DateTimeOffset FetchedAt,
    string Provider);
