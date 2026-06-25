using System.Collections.Concurrent;
using Sovereign.Storage;

namespace Sovereign.SecurityTests;

/// <summary>
/// A trivial in-memory <see cref="IRestorePointStore"/> for tests that do not exercise persistence.
/// </summary>
internal sealed class InMemoryRestorePointStore : IRestorePointStore
{
    private readonly ConcurrentDictionary<string, RestorePoint> _latest = new(StringComparer.Ordinal);
    private long _id;

    public ValueTask InitializeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask<long> SaveAsync(string policyId, string correlationId, string payloadJson, CancellationToken cancellationToken)
    {
        long id = Interlocked.Increment(ref this._id);
        this._latest[policyId] = new RestorePoint(id, policyId, correlationId, DateTimeOffset.UtcNow, payloadJson);
        return ValueTask.FromResult(id);
    }

    public ValueTask<RestorePoint?> GetLatestAsync(string policyId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(this._latest.TryGetValue(policyId, out RestorePoint? point) ? point : null);
}
