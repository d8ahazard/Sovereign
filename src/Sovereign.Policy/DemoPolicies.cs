using Sovereign.Contracts;

namespace Sovereign.Policy;

/// <summary>
/// The initial harmless test policies shipped in Milestone 2.
/// </summary>
/// <remarks>
/// These operate only on the in-memory sandbox <see cref="ISettingProvider"/> and make no real
/// machine changes. They exist to exercise the full detect/plan/apply/verify/rollback engine and to
/// give the UI/CLI something to act on. Real Windows policies (registry/Appx) arrive in Milestone 5
/// behind the same engine and provider seam.
/// </remarks>
public static class DemoPolicies
{
    /// <summary>The sandbox key prefix all demo policies write under.</summary>
    public const string KeyPrefix = "sandbox/demo/";

    /// <summary>
    /// Creates the built-in harmless demo policies.
    /// </summary>
    public static IReadOnlyList<IPolicy> CreateDefault() =>
    [
        new DeclarativeSettingPolicy(
            new PolicyMetadata(
                Id: "demo.telemetry-off",
                Version: 1,
                Title: "Demo: turn off sandbox telemetry",
                Description: "Sets a sandbox telemetry flag to off. Harmless demonstration policy; makes no real system change.",
                RiskLevel: PolicyRiskLevel.Low,
                Scope: PolicyScope.Machine),
            [
                new DesiredSetting($"{KeyPrefix}telemetry.enabled", SettingValue.Present("0"), "Sandbox telemetry should be off."),
            ]),

        new DeclarativeSettingPolicy(
            new PolicyMetadata(
                Id: "demo.consumer-features-off",
                Version: 1,
                Title: "Demo: disable sandbox consumer features",
                Description: "Disables two sandbox consumer-experience flags and removes a sandbox suggestions key. Harmless demonstration policy.",
                RiskLevel: PolicyRiskLevel.Medium,
                Scope: PolicyScope.Machine),
            [
                new DesiredSetting($"{KeyPrefix}consumer.experiences", SettingValue.Present("0"), "Sandbox consumer experiences should be off."),
                new DesiredSetting($"{KeyPrefix}consumer.tips", SettingValue.Present("0"), "Sandbox tips should be off."),
                new DesiredSetting($"{KeyPrefix}consumer.suggestions", SettingValue.Absent, "Sandbox suggestions key should be removed."),
            ]),
    ];
}
