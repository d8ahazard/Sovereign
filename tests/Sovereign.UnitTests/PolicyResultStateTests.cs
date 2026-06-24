using Sovereign.Contracts;
using Xunit;

namespace Sovereign.UnitTests;

/// <summary>
/// Unit tests guarding the policy-result-state invariants from agent_start.md section 8.
/// </summary>
public sealed class PolicyResultStateTests
{
    [Fact]
    public void Unknown_IsTheDefaultValue()
    {
        // Fail-closed: an uninitialized result must be Unknown, never Compliant.
        Assert.Equal(PolicyResultState.Unknown, default);
        Assert.Equal(0, (int)PolicyResultState.Unknown);
    }

    [Fact]
    public void Unknown_IsNotCompliant()
    {
        Assert.NotEqual(PolicyResultState.Compliant, PolicyResultState.Unknown);
    }

    [Fact]
    public void AllRequiredStates_AreDistinct()
    {
        PolicyResultState[] states =
        [
            PolicyResultState.Unknown,
            PolicyResultState.Compliant,
            PolicyResultState.NonCompliant,
            PolicyResultState.Applied,
            PolicyResultState.PartiallyApplied,
            PolicyResultState.Unsupported,
            PolicyResultState.VerificationFailed,
            PolicyResultState.RollbackFailed,
            PolicyResultState.RequiresReboot,
            PolicyResultState.RequiresUserAction,
        ];

        Assert.Equal(states.Length, states.Distinct().Count());
    }
}
