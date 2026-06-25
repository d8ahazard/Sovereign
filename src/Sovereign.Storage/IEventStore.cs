using System.Collections.Generic;
using Sovereign.Contracts.Ipc;

namespace Sovereign.Storage;

/// <summary>
/// The local, append-only audit event store.
/// </summary>
/// <remarks>
/// Backed by SQLite (agent_start.md section 3). Data stays local (section 2.2). The store must
/// fail safe: an open/migration/read failure surfaces as an error rather than silently returning
/// empty success, so callers never mistake a broken store for "nothing happened".
/// </remarks>
public interface IEventStore
{
    /// <summary>
    /// Opens the store and applies any pending schema migrations. Must be called once before use.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    ValueTask InitializeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Appends an audit event and returns its assigned monotonic id.
    /// </summary>
    /// <param name="category">A stable category identifier for the event.</param>
    /// <param name="message">A human-readable description that must not contain secrets.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The new event's id.</returns>
    ValueTask<long> AppendAsync(string category, string message, CancellationToken cancellationToken);

    /// <summary>
    /// Returns recent events ordered by id ascending.
    /// </summary>
    /// <param name="limit">Maximum number of events to return (the store clamps to a safe bound).</param>
    /// <param name="afterId">When set, only events with a greater id are returned.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The matching events.</returns>
    ValueTask<IReadOnlyList<EventRecord>> QueryAsync(int limit, long? afterId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the total number of events stored.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    ValueTask<long> CountAsync(CancellationToken cancellationToken);
}
