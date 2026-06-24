using Xunit;

namespace Sovereign.SecurityTests;

/// <summary>
/// Placeholder for the security test tier. These verify the privilege boundary and abuse
/// cases from the threat model (agent_start.md sections 7 and 15.2): IPC authentication and
/// authorization, rejection of unprivileged policy changes, replay protection, and that no
/// enforcement error becomes an allow decision. They arrive with the service/IPC milestone.
/// </summary>
public sealed class ScaffoldPlaceholder
{
    [Fact(Skip = "Security tests are introduced with the authenticated IPC boundary (Milestone 1+).")]
    public void Placeholder()
    {
    }
}
