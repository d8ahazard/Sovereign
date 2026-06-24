using Sovereign.Contracts;
using Xunit;

namespace Sovereign.UnitTests;

/// <summary>
/// Unit tests for the fail-closed network decision defaults. These prove the most important
/// Milestone 0 invariant available at the contract layer: the absence of an explicit allow
/// resolves to a block, and a blocked decision asserts no hostname as fact.
/// </summary>
public sealed class NetworkDecisionTests
{
    [Fact]
    public void DefaultDeny_IsABlockDecision()
    {
        NetworkDecision decision = NetworkDecision.DefaultDeny();

        Assert.Equal(NetworkDecisionAction.Block, decision.Action);
        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public void DefaultDeny_AssertsNoHostname()
    {
        NetworkDecision decision = NetworkDecision.DefaultDeny(@"C:\Windows\System32\svchost.exe");

        Assert.Null(decision.RemoteHost);
        Assert.Equal(@"C:\Windows\System32\svchost.exe", decision.ExecutablePath);
        Assert.False(string.IsNullOrWhiteSpace(decision.Explanation));
    }

    [Fact]
    public void BlockAction_IsTheZeroValue()
    {
        // Fail-closed relies on the default (uninitialized) enum value being Block.
        Assert.Equal(0, (int)default(NetworkDecisionAction));
        Assert.Equal(NetworkDecisionAction.Block, default);
    }

    [Theory]
    [InlineData(NetworkDecisionAction.AllowOnce)]
    [InlineData(NetworkDecisionAction.AllowUntilProcessExit)]
    [InlineData(NetworkDecisionAction.AllowForDuration)]
    [InlineData(NetworkDecisionAction.AllowForProfile)]
    [InlineData(NetworkDecisionAction.AllowPermanent)]
    public void NonBlockActions_AreAllowed(NetworkDecisionAction action)
    {
        NetworkDecision decision = new(action, ExecutablePath: null, RemoteHost: null, Explanation: "test");

        Assert.True(decision.IsAllowed);
    }
}
