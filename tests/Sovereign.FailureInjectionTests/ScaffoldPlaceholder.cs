using Xunit;

namespace Sovereign.FailureInjectionTests;

/// <summary>
/// Placeholder for the failure-injection test tier (agent_start.md section 12.4): database
/// locked/corrupted, disk full, service/UI terminated, IPC unavailable, access denied,
/// registry/WFP write failures, partial policy apply, reboot between steps, clock change,
/// scheduler failure, notification flood, invalid config, and unsupported builds. Each must
/// fail closed for network enforcement. These arrive with the enforcement engine.
/// </summary>
public sealed class ScaffoldPlaceholder
{
    [Fact(Skip = "Failure-injection tests are introduced with the storage and enforcement engine (Milestone 2+).")]
    public void Placeholder()
    {
    }
}
