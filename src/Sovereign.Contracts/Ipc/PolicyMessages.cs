using System.Collections.Generic;

namespace Sovereign.Contracts.Ipc;

/// <summary>
/// Summary metadata describing a managed policy, for listing in the UI/CLI.
/// </summary>
/// <param name="Id">Stable policy identifier.</param>
/// <param name="Version">Policy content/schema version.</param>
/// <param name="Title">Short human-readable title.</param>
/// <param name="Description">Longer description of what the policy does.</param>
/// <param name="RiskLevel">The policy's risk level.</param>
/// <param name="Scope">Whether the policy is machine- or user-scoped.</param>
/// <param name="RequiresReboot">Whether applying may require a reboot.</param>
/// <param name="Level">The hardening preset this policy belongs to.</param>
/// <param name="Category">A short grouping label for the UI (for example, "Privacy").</param>
public sealed record PolicyInfo(
    string Id,
    int Version,
    string Title,
    string Description,
    PolicyRiskLevel RiskLevel,
    PolicyScope Scope,
    bool RequiresReboot,
    PolicyLevel Level = PolicyLevel.Normal,
    string Category = "General");

/// <summary>
/// The list of available policies.
/// </summary>
/// <param name="Policies">All managed policies the service exposes.</param>
public sealed record PolicyListResult(IReadOnlyList<PolicyInfo> Policies);

/// <summary>
/// A single planned change to a setting. A null value means the setting is absent (to be created
/// or to be deleted, depending on whether it is the "from" or "to").
/// </summary>
/// <param name="Key">The setting key.</param>
/// <param name="From">Current value, or null if absent.</param>
/// <param name="To">Desired value, or null if it should be absent.</param>
/// <param name="Explanation">Why this change is made.</param>
public sealed record PolicyChangeInfo(string Key, string? From, string? To, string Explanation);

/// <summary>
/// A plan preview: the concrete set of changes a policy would make. An empty change list means the
/// system already matches the desired state.
/// </summary>
/// <param name="PolicyId">The policy this plan is for.</param>
/// <param name="Changes">The changes that would be applied.</param>
public sealed record PolicyPlanInfo(string PolicyId, IReadOnlyList<PolicyChangeInfo> Changes);

/// <summary>
/// The result of a detect operation.
/// </summary>
/// <param name="PolicyId">The policy that was evaluated.</param>
/// <param name="State">The detected state (never <see cref="PolicyResultState.Compliant"/> when unknown).</param>
public sealed record PolicyDetectResult(string PolicyId, PolicyResultState State);

/// <summary>
/// The result of a mutating policy operation (apply or rollback).
/// </summary>
/// <param name="PolicyId">The policy that was operated on.</param>
/// <param name="CorrelationId">Correlation id tying together the audit events for this operation.</param>
/// <param name="State">The resulting state. Never <see cref="PolicyResultState.Compliant"/> on failure.</param>
/// <param name="Changes">The changes that were attempted/applied.</param>
/// <param name="FailureDetail">Human-readable detail when the operation did not fully succeed.</param>
public sealed record PolicyRunResult(
    string PolicyId,
    string CorrelationId,
    PolicyResultState State,
    IReadOnlyList<PolicyChangeInfo> Changes,
    string? FailureDetail);

/// <summary>
/// Identifies the policy a detect/plan/apply/rollback request targets.
/// </summary>
/// <param name="PolicyId">The target policy id.</param>
public sealed record PolicyTargetRequest(string PolicyId);

/// <summary>
/// A persisted restore point that a policy can be rolled back to.
/// </summary>
/// <param name="Id">Monotonic local identifier.</param>
/// <param name="PolicyId">The policy this restore point belongs to.</param>
/// <param name="CorrelationId">Correlation id of the apply that created it.</param>
/// <param name="CreatedUtc">When the restore point was captured.</param>
public sealed record RestorePointInfo(long Id, string PolicyId, string CorrelationId, DateTimeOffset CreatedUtc);

/// <summary>
/// The list of recent restore points the service has captured.
/// </summary>
/// <param name="RestorePoints">The restore points, most recent first.</param>
public sealed record RestorePointListResult(IReadOnlyList<RestorePointInfo> RestorePoints);
