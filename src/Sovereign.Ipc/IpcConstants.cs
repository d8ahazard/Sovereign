namespace Sovereign.Ipc;

/// <summary>
/// Transport-level constants for the named-pipe IPC framing (ADR 0002).
/// </summary>
public static class IpcConstants
{
    /// <summary>
    /// Maximum allowed size, in bytes, of a single framed message body. Frames declaring a
    /// larger size are rejected and the connection is closed (a local denial-of-service guard).
    /// </summary>
    public const int MaxMessageBytes = 1 * 1024 * 1024;

    /// <summary>Number of bytes in the length prefix that precedes each message body.</summary>
    public const int LengthPrefixBytes = 4;
}
