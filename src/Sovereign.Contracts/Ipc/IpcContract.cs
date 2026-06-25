namespace Sovereign.Contracts.Ipc;

/// <summary>
/// Shared, stable constants for the local IPC contract between the UI/CLI and the service.
/// </summary>
/// <remarks>
/// See ADR 0002. The protocol is versioned; clients and the service negotiate a common version
/// via <see cref="HelloRequest"/>/<see cref="HelloResponse"/> before any operation is issued.
/// </remarks>
public static class IpcContract
{
    /// <summary>The named-pipe name (without the <c>\\.\pipe\</c> prefix).</summary>
    public const string PipeName = "Sovereign.Ipc";

    /// <summary>The lowest protocol version this build understands.</summary>
    public const int ProtocolVersionMin = 1;

    /// <summary>The highest protocol version this build understands.</summary>
    public const int ProtocolVersionMax = 1;

    /// <summary>The protocol version this build prefers to speak.</summary>
    public const int CurrentProtocolVersion = 1;
}

/// <summary>
/// The set of operations a caller may request. Milestone 1 exposes read-only operations only.
/// </summary>
public enum IpcOperation
{
    /// <summary>Liveness check; returns success with no payload.</summary>
    Ping = 0,

    /// <summary>Returns service <see cref="HealthStatus"/>.</summary>
    GetHealth,

    /// <summary>Returns the service version string.</summary>
    GetVersion,

    /// <summary>Returns recent audit events (see <see cref="QueryEventsRequest"/>).</summary>
    QueryEvents,
}

/// <summary>
/// Result code for an IPC response. <see cref="None"/> (zero) indicates success; all non-zero
/// values indicate failure and must never be treated as success.
/// </summary>
public enum IpcErrorCode
{
    /// <summary>Success.</summary>
    None = 0,

    /// <summary>The requested operation is not recognized.</summary>
    UnknownOperation,

    /// <summary>The caller is not authorized to perform the operation.</summary>
    Unauthorized,

    /// <summary>The request was malformed or failed validation.</summary>
    BadRequest,

    /// <summary>No protocol version is common to client and service.</summary>
    ProtocolVersionUnsupported,

    /// <summary>The message exceeded the maximum allowed size.</summary>
    MessageTooLarge,

    /// <summary>An unexpected server-side error occurred.</summary>
    InternalError,
}
