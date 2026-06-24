namespace Sovereign.Storage;

/// <summary>
/// The append-only local event history contract.
/// </summary>
/// <remarks>
/// Milestone 0 placeholder only. The SQLite-backed implementation, versioned migrations,
/// and corruption handling described in agent_start.md section 3 (Sovereign.Storage) arrive
/// in later milestones. Per section 2.2, stored data must remain local and must never relax
/// enforcement on corruption. No storage backend is wired up in Milestone 0.
/// </remarks>
public interface IEventStore
{
    /// <summary>
    /// Appends an audit event record to the local, append-only history.
    /// </summary>
    /// <param name="category">A stable category identifier for the event.</param>
    /// <param name="message">A human-readable description that must not contain secrets.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the event has been durably recorded.</returns>
    ValueTask AppendAsync(string category, string message, CancellationToken cancellationToken);
}
