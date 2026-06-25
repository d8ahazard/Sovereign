namespace Sovereign.Storage;

/// <summary>
/// A persisted snapshot of the original state captured before a policy was applied, enabling
/// user-initiated rollback (the capture-before-change model; see docs/reversibility.md).
/// </summary>
/// <param name="Id">Monotonic local identifier.</param>
/// <param name="PolicyId">The policy this restore point belongs to.</param>
/// <param name="CorrelationId">Correlation id of the apply operation that created it.</param>
/// <param name="CreatedUtc">When the restore point was captured.</param>
/// <param name="PayloadJson">Opaque JSON describing the captured original state (owned by the engine).</param>
public sealed record RestorePoint(long Id, string PolicyId, string CorrelationId, DateTimeOffset CreatedUtc, string PayloadJson);

/// <summary>
/// Local persistence for policy restore points.
/// </summary>
/// <remarks>
/// The payload is an opaque JSON string produced and consumed by the policy engine; storage treats
/// it as data and does not interpret it. Like the event store, this must fail safe rather than
/// silently lose data.
/// </remarks>
public interface IRestorePointStore
{
    /// <summary>Opens the store and ensures its schema exists. Call once before use.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    ValueTask InitializeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Persists a restore point and returns its assigned id.
    /// </summary>
    /// <param name="policyId">The policy the restore point belongs to.</param>
    /// <param name="correlationId">Correlation id of the apply operation.</param>
    /// <param name="payloadJson">Opaque captured-state JSON.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    ValueTask<long> SaveAsync(string policyId, string correlationId, string payloadJson, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the most recent restore point for a policy, or null if none exists.
    /// </summary>
    /// <param name="policyId">The policy to look up.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    ValueTask<RestorePoint?> GetLatestAsync(string policyId, CancellationToken cancellationToken);
}
