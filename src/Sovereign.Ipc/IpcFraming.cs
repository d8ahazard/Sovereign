using System.Buffers.Binary;

namespace Sovereign.Ipc;

/// <summary>
/// Length-prefixed message framing shared by the IPC client and server (ADR 0002): a 4-byte
/// little-endian unsigned length followed by that many UTF-8 JSON bytes. A hard size bound guards
/// against local denial-of-service via oversized declared lengths.
/// </summary>
public static class IpcFraming
{
    /// <summary>
    /// Writes a single framed message: the length prefix followed by <paramref name="body"/>.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="body">The message body bytes.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <exception cref="IpcException">The body exceeds <see cref="IpcConstants.MaxMessageBytes"/>.</exception>
    public static async ValueTask WriteFrameAsync(Stream stream, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (body.Length > IpcConstants.MaxMessageBytes)
        {
            throw new IpcException(
                $"Outgoing message of {body.Length} bytes exceeds the maximum of {IpcConstants.MaxMessageBytes}.",
                Contracts.Ipc.IpcErrorCode.MessageTooLarge);
        }

        byte[] prefix = new byte[IpcConstants.LengthPrefixBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(prefix, (uint)body.Length);

        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads a single framed message body, or returns <see langword="null"/> on a clean
    /// end-of-stream before any bytes of the next frame arrive.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The message body bytes, or <see langword="null"/> if the peer closed the stream.</returns>
    /// <exception cref="IpcException">The declared length exceeds <see cref="IpcConstants.MaxMessageBytes"/> or the stream ended mid-frame.</exception>
    public static async ValueTask<byte[]?> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        byte[] prefix = new byte[IpcConstants.LengthPrefixBytes];
        int read = await ReadUpToAsync(stream, prefix, cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            return null;
        }

        if (read < prefix.Length)
        {
            throw new IpcException("Stream ended while reading the length prefix.", Contracts.Ipc.IpcErrorCode.BadRequest);
        }

        uint length = BinaryPrimitives.ReadUInt32LittleEndian(prefix);
        if (length > IpcConstants.MaxMessageBytes)
        {
            throw new IpcException(
                $"Incoming message declares {length} bytes, exceeding the maximum of {IpcConstants.MaxMessageBytes}.",
                Contracts.Ipc.IpcErrorCode.MessageTooLarge);
        }

        byte[] body = new byte[length];
        if (length > 0)
        {
            await stream.ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);
        }

        return body;
    }

    private static async ValueTask<int> ReadUpToAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
            if (n == 0)
            {
                break;
            }

            total += n;
        }

        return total;
    }
}
