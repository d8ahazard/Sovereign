using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Sovereign.Service;
using Xunit;

namespace Sovereign.UnitTests;

/// <summary>
/// Unit tests for the named-pipe ACL builder (ADR 0002). The ACL is the primary trust boundary:
/// privileged principals get full control, the interactive user gets read/write only (never the
/// server-impersonating CreateNewInstance right), and no rule grants Everyone/Anonymous.
/// </summary>
public sealed class PipeSecurityFactoryTests
{
    private static List<PipeAccessRule> GetRules()
    {
        PipeSecurity security = PipeSecurityFactory.Create();
        AuthorizationRuleCollection rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
        return rules.Cast<PipeAccessRule>().ToList();
    }

    [Fact]
    public void Create_GrantsLocalSystemFullControl()
    {
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        PipeAccessRule rule = Assert.Single(GetRules(), r => r.IdentityReference.Equals(system));

        Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        Assert.True(rule.PipeAccessRights.HasFlag(PipeAccessRights.FullControl));
    }

    [Fact]
    public void Create_GrantsInteractiveUserReadWriteOnly_WithoutCreateNewInstance()
    {
        var interactive = new SecurityIdentifier(WellKnownSidType.InteractiveSid, null);
        PipeAccessRule rule = Assert.Single(GetRules(), r => r.IdentityReference.Equals(interactive));

        Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        Assert.False(
            rule.PipeAccessRights.HasFlag(PipeAccessRights.CreateNewInstance),
            "The interactive user must not be able to create new server instances.");
    }

    [Fact]
    public void Create_DoesNotGrantEveryone()
    {
        var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

        Assert.DoesNotContain(GetRules(), r => r.IdentityReference.Equals(everyone));
    }
}
