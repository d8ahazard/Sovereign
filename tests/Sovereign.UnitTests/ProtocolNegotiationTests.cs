using Sovereign.Ipc;
using Xunit;

namespace Sovereign.UnitTests;

/// <summary>
/// Unit tests for IPC protocol-version negotiation (ADR 0002). Negotiation must fail closed when
/// there is no common version.
/// </summary>
public sealed class ProtocolNegotiationTests
{
    [Fact]
    public void TryNegotiate_IdenticalSingleVersion_AgreesOnIt()
    {
        bool ok = ProtocolNegotiation.TryNegotiate(1, 1, 1, 1, out int agreed);

        Assert.True(ok);
        Assert.Equal(1, agreed);
    }

    [Fact]
    public void TryNegotiate_OverlappingRanges_PicksHighestCommon()
    {
        bool ok = ProtocolNegotiation.TryNegotiate(1, 3, 2, 5, out int agreed);

        Assert.True(ok);
        Assert.Equal(3, agreed);
    }

    [Fact]
    public void TryNegotiate_DisjointRanges_FailsClosed()
    {
        bool ok = ProtocolNegotiation.TryNegotiate(2, 3, 4, 5, out int agreed);

        Assert.False(ok);
        Assert.Equal(0, agreed);
    }

    [Theory]
    [InlineData(0, 1, 1, 1)]
    [InlineData(1, 1, 0, 1)]
    [InlineData(2, 1, 1, 1)]
    [InlineData(1, 1, 2, 1)]
    public void TryNegotiate_InvalidRanges_FailClosed(int clientMin, int clientMax, int serviceMin, int serviceMax)
    {
        bool ok = ProtocolNegotiation.TryNegotiate(clientMin, clientMax, serviceMin, serviceMax, out int agreed);

        Assert.False(ok);
        Assert.Equal(0, agreed);
    }
}
