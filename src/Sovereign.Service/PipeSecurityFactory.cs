using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Sovereign.Service;

/// <summary>
/// Builds the <see cref="PipeSecurity"/> applied to the server pipe at creation (ADR 0002).
/// </summary>
/// <remarks>
/// The ACL is the primary trust boundary. LocalSystem and Administrators get full control; the
/// interactive logged-on user gets <see cref="PipeAccessRights.ReadWrite"/> only (deliberately not
/// <see cref="PipeAccessRights.CreateNewInstance"/>, which would let a client impersonate the
/// server). No rule is added for Everyone or Anonymous.
/// <para>
/// The account the server itself runs under is also granted full control so it can create the
/// additional pipe instances needed to serve concurrent/sequential connections. In production that
/// account is LocalSystem (already covered); in console/dev or test runs it is the interactive
/// user, who must be able to manage the pipe it created. This does not widen rights for client
/// processes running under any other account.
/// </para>
/// </remarks>
public static class PipeSecurityFactory
{
    /// <summary>Creates the pipe security descriptor for the Sovereign IPC pipe.</summary>
    public static PipeSecurity Create()
    {
        var security = new PipeSecurity();

        var localSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var interactiveUsers = new SecurityIdentifier(WellKnownSidType.InteractiveSid, null);

        security.AddAccessRule(new PipeAccessRule(localSystem, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(administrators, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(interactiveUsers, PipeAccessRights.ReadWrite, AccessControlType.Allow));

        AddServerOwnerRule(security, localSystem, administrators);

        return security;
    }

    private static void AddServerOwnerRule(PipeSecurity security, SecurityIdentifier localSystem, SecurityIdentifier administrators)
    {
        using WindowsIdentity current = WindowsIdentity.GetCurrent();
        SecurityIdentifier? owner = current.User;

        // Skip when the owner is already covered by a full-control rule to keep the ACL minimal.
        if (owner is null || owner.Equals(localSystem) || owner.Equals(administrators))
        {
            return;
        }

        security.AddAccessRule(new PipeAccessRule(owner, PipeAccessRights.FullControl, AccessControlType.Allow));
    }
}
