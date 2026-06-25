using System.Collections.Generic;
using Sovereign.Contracts.Ipc;

namespace Sovereign.Service;

/// <summary>
/// The explicit allow-list of IPC operations the service will perform (ADR 0002).
/// </summary>
/// <remarks>
/// Authorization is allow-list based and fails closed: an operation is denied unless it is
/// explicitly present here. Milestone 1 exposes only read-only operations. Privileged, mutating
/// operations are deliberately absent and must be added one at a time, each with its own
/// authorization review. Decisions never depend on the (spoofable) client process id.
/// </remarks>
public sealed class AuthorizationPolicy
{
    private readonly HashSet<IpcOperation> _allowedOperations =
    [
        IpcOperation.Ping,
        IpcOperation.GetHealth,
        IpcOperation.GetVersion,
        IpcOperation.QueryEvents,
    ];

    /// <summary>
    /// Returns whether the operation is on the allow-list.
    /// </summary>
    /// <param name="operation">The requested operation.</param>
    public bool IsAllowed(IpcOperation operation) => this._allowedOperations.Contains(operation);
}
