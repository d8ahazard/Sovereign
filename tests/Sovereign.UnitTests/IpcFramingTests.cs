using System.Buffers.Binary;
using Sovereign.Contracts.Ipc;
using Sovereign.Ipc;
using Xunit;

namespace Sovereign.UnitTests;

/// <summary>
/// Unit tests for length-prefixed IPC framing (ADR 0002), including the size bound that guards
/// against local denial-of-service.
/// </summary>
public sealed class IpcFramingTests
{
    [Fact]
    public async Task WriteThenRead_RoundTripsBody()
    {
        byte[] payload = [1, 2, 3, 4, 250, 0, 99];
        using var stream = new MemoryStream();

        await IpcFraming.WriteFrameAsync(stream, payload, CancellationToken.None);
        stream.Position = 0;
        byte[]? read = await IpcFraming.ReadFrameAsync(stream, CancellationToken.None);

        Assert.NotNull(read);
        Assert.Equal(payload, read);
    }

    [Fact]
    public async Task ReadFrame_OnEmptyStream_ReturnsNull()
    {
        using var stream = new MemoryStream();

        byte[]? read = await IpcFraming.ReadFrameAsync(stream, CancellationToken.None);

        Assert.Null(read);
    }

    [Fact]
    public async Task WriteFrame_OversizedBody_ThrowsMessageTooLarge()
    {
        using var stream = new MemoryStream();
        byte[] tooBig = new byte[IpcConstants.MaxMessageBytes + 1];

        IpcException ex = await Assert.ThrowsAsync<IpcException>(
            async () => await IpcFraming.WriteFrameAsync(stream, tooBig, CancellationToken.None));

        Assert.Equal(IpcErrorCode.MessageTooLarge, ex.ErrorCode);
    }

    [Fact]
    public async Task ReadFrame_DeclaredLengthOverBound_ThrowsBeforeAllocating()
    {
        using var stream = new MemoryStream();
        byte[] prefix = new byte[IpcConstants.LengthPrefixBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(prefix, (uint)IpcConstants.MaxMessageBytes + 1);
        stream.Write(prefix, 0, prefix.Length);
        stream.Position = 0;

        IpcException ex = await Assert.ThrowsAsync<IpcException>(
            async () => await IpcFraming.ReadFrameAsync(stream, CancellationToken.None));

        Assert.Equal(IpcErrorCode.MessageTooLarge, ex.ErrorCode);
    }
}
