using Sovereign.Contracts.Ipc;
using Sovereign.Service;
using Xunit;

namespace Sovereign.UnitTests;

/// <summary>
/// Unit tests for the IPC authorization allow-list (ADR 0002). Authorization fails closed: only
/// explicitly allow-listed, read-only operations are permitted in Milestone 1.
/// </summary>
public sealed class AuthorizationPolicyTests
{
    [Theory]
    [InlineData(IpcOperation.Ping)]
    [InlineData(IpcOperation.GetHealth)]
    [InlineData(IpcOperation.GetVersion)]
    [InlineData(IpcOperation.QueryEvents)]
    public void IsAllowed_ReadOnlyOperations_AreAllowed(IpcOperation operation)
    {
        var policy = new AuthorizationPolicy();

        Assert.True(policy.IsAllowed(operation));
    }

    [Fact]
    public void IsAllowed_UnknownOperation_IsDenied()
    {
        var policy = new AuthorizationPolicy();

        // A value outside the defined enum stands in for any future/unknown operation.
        Assert.False(policy.IsAllowed((IpcOperation)9999));
    }
}
