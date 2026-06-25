using System.Reflection;
using Sovereign.Contracts.Ipc;

namespace Sovereign.Service;

/// <summary>
/// Process-wide runtime facts shared across IPC handlers: when this instance started, its version,
/// and the protocol version it speaks.
/// </summary>
public sealed class ServiceRuntime
{
    /// <summary>When the current service instance started.</summary>
    public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;

    /// <summary>The informational version of the running service assembly.</summary>
    public string Version { get; } =
        typeof(ServiceRuntime).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(ServiceRuntime).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    /// <summary>The protocol version this build prefers.</summary>
    public int ProtocolVersion { get; } = IpcContract.CurrentProtocolVersion;

    /// <summary>Seconds elapsed since <see cref="StartedUtc"/>.</summary>
    public long UptimeSeconds => (long)(DateTimeOffset.UtcNow - this.StartedUtc).TotalSeconds;
}
