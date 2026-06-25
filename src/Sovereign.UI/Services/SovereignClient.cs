using System;
using System.Threading;
using System.Threading.Tasks;
using Sovereign.Ipc;

namespace Sovereign.UI.Services;

/// <summary>
/// A thin helper that opens a short-lived authenticated IPC connection, runs an operation, and
/// disposes it. The UI owns no privileged state; every call flows through the ACL'd named pipe
/// (ADR 0002/0003). Connections are intentionally short-lived so the UI never pins the service.
/// </summary>
public static class SovereignClient
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Runs an operation against a freshly connected client and returns its result.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The work to perform with the connected client.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public static async Task<T> RunAsync<T>(Func<IpcClient, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await using IpcClient client = await IpcClient.ConnectAsync("sovereign-ui", connectTimeout: ConnectTimeout, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await operation(client).ConfigureAwait(false);
    }

    /// <summary>Runs an operation against a freshly connected client.</summary>
    /// <param name="operation">The work to perform with the connected client.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public static async Task RunAsync(Func<IpcClient, Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await using IpcClient client = await IpcClient.ConnectAsync("sovereign-ui", connectTimeout: ConnectTimeout, cancellationToken: cancellationToken).ConfigureAwait(false);
        await operation(client).ConfigureAwait(false);
    }
}
