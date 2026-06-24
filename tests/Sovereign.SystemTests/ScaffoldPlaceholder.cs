using Xunit;

namespace Sovereign.SystemTests;

/// <summary>
/// Placeholder for the system test tier (agent_start.md section 12.3). These run in disposable
/// Windows VMs across supported editions/builds and include the external packet-capture
/// locked-mode acceptance test (section 12.5). They require lab infrastructure and are not run
/// as part of the Milestone 0 non-privileged unit test gate.
/// </summary>
public sealed class ScaffoldPlaceholder
{
    [Fact(Skip = "System tests require disposable Windows VMs and external packet capture (Milestone 3+).")]
    public void Placeholder()
    {
    }
}
