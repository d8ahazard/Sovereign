namespace Sovereign.Contracts;

/// <summary>
/// Whether a policy targets machine-wide state or per-user state.
/// </summary>
public enum PolicyScope
{
    /// <summary>Affects the whole machine (all users).</summary>
    Machine = 0,

    /// <summary>Affects the current user's state only.</summary>
    User,
}
