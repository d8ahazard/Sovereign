using Sovereign.Contracts.Ipc;

namespace Sovereign.Ipc;

/// <summary>
/// Raised when an IPC operation fails, either at the transport level or because the service
/// returned a non-success <see cref="IpcErrorCode"/>.
/// </summary>
public sealed class IpcException : Exception
{
    /// <summary>The error code returned by the service, when applicable.</summary>
    public IpcErrorCode ErrorCode { get; }

    /// <summary>Initializes a new instance with a message and optional error code.</summary>
    /// <param name="message">The error description.</param>
    /// <param name="errorCode">The service error code, if any.</param>
    public IpcException(string message, IpcErrorCode errorCode = IpcErrorCode.InternalError)
        : base(message)
    {
        this.ErrorCode = errorCode;
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error description.</param>
    /// <param name="innerException">The underlying cause.</param>
    public IpcException(string message, Exception innerException)
        : base(message, innerException)
    {
        this.ErrorCode = IpcErrorCode.InternalError;
    }
}
