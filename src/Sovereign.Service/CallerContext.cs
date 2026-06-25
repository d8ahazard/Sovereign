namespace Sovereign.Service;

/// <summary>
/// The authenticated context of an IPC caller, captured from the OS, used for auditing and
/// authorization. Never derived from the client process id (ADR 0002).
/// </summary>
/// <param name="UserName">The caller's Windows account name, or null if it could not be determined.</param>
public sealed record CallerContext(string? UserName)
{
    /// <summary>A display value safe for logs when the user name is unknown.</summary>
    public string UserNameOrUnknown => string.IsNullOrEmpty(this.UserName) ? "<unknown>" : this.UserName;
}
