using Sovereign.Contracts;

namespace Sovereign.Policy;

/// <summary>
/// Static metadata describing a managed policy (agent_start.md section 8).
/// </summary>
/// <param name="Id">Stable unique identifier.</param>
/// <param name="Version">Policy content/schema version.</param>
/// <param name="Title">Short human-readable title.</param>
/// <param name="Description">Longer description of what the policy does and why.</param>
/// <param name="RiskLevel">The policy's risk level.</param>
/// <param name="Scope">Machine- or user-scoped.</param>
/// <param name="RequiresReboot">Whether applying may require a reboot to take full effect.</param>
/// <param name="RequiresLogoff">Whether applying may require a logoff to take full effect.</param>
public sealed record PolicyMetadata(
    string Id,
    int Version,
    string Title,
    string Description,
    PolicyRiskLevel RiskLevel,
    PolicyScope Scope,
    bool RequiresReboot = false,
    bool RequiresLogoff = false);

/// <summary>
/// A declared desired state for a single setting.
/// </summary>
/// <param name="Key">The setting key.</param>
/// <param name="Desired">The desired value (use <see cref="SettingValue.Absent"/> to require removal).</param>
/// <param name="Explanation">Why this setting is desired.</param>
public sealed record DesiredSetting(string Key, SettingValue Desired, string Explanation);

/// <summary>
/// A single concrete change between current and desired state.
/// </summary>
/// <param name="Key">The setting key.</param>
/// <param name="From">The current value.</param>
/// <param name="To">The desired value.</param>
/// <param name="Explanation">Why this change is made.</param>
public sealed record PolicyChange(string Key, SettingValue From, SettingValue To, string Explanation);

/// <summary>
/// A plan: the concrete set of changes required to reach the desired state.
/// </summary>
/// <param name="PolicyId">The policy this plan is for.</param>
/// <param name="Changes">The changes required (empty means already compliant).</param>
public sealed record PolicyPlan(string PolicyId, IReadOnlyList<PolicyChange> Changes)
{
    /// <summary>Gets a value indicating whether the plan would make no changes.</summary>
    public bool IsNoOp => this.Changes.Count == 0;
}

/// <summary>
/// The outcome of a policy engine operation, including the plan it acted on and an audit
/// correlation id.
/// </summary>
/// <param name="PolicyId">The policy operated on.</param>
/// <param name="CorrelationId">Correlation id tying together the operation's audit events.</param>
/// <param name="State">The resulting state. Never <see cref="PolicyResultState.Compliant"/> on failure.</param>
/// <param name="Plan">The plan that was evaluated/applied.</param>
/// <param name="FailureDetail">Human-readable detail when the operation did not fully succeed.</param>
public sealed record PolicyExecutionReport(
    string PolicyId,
    string CorrelationId,
    PolicyResultState State,
    PolicyPlan Plan,
    string? FailureDetail);
