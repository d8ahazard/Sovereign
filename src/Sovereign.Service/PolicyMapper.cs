using Sovereign.Contracts.Ipc;
using Sovereign.Policy;

namespace Sovereign.Service;

/// <summary>
/// Maps internal policy-engine types onto the stable IPC DTOs.
/// </summary>
internal static class PolicyMapper
{
    public static PolicyInfo ToInfo(IPolicy policy)
    {
        PolicyMetadata m = policy.Metadata;
        return new PolicyInfo(m.Id, m.Version, m.Title, m.Description, m.RiskLevel, m.Scope, m.RequiresReboot);
    }

    public static PolicyChangeInfo ToChange(PolicyChange change) =>
        new(change.Key, ToNullable(change.From), ToNullable(change.To), change.Explanation);

    public static PolicyPlanInfo ToPlan(PolicyPlan plan) =>
        new(plan.PolicyId, plan.Changes.Select(ToChange).ToArray());

    public static PolicyRunResult ToRun(PolicyExecutionReport report) =>
        new(
            report.PolicyId,
            report.CorrelationId,
            report.State,
            report.Plan.Changes.Select(ToChange).ToArray(),
            report.FailureDetail);

    private static string? ToNullable(SettingValue value) => value.Exists ? value.Value : null;
}
