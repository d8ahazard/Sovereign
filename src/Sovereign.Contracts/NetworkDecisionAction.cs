namespace Sovereign.Contracts;

/// <summary>
/// The action taken (or to be taken) for an outbound connection attempt.
/// </summary>
/// <remarks>
/// The default-deny posture (agent_start.md section 2.1) requires that the absence of an
/// explicit allow resolves to <see cref="Block"/>. The distinct allow lifetimes mirror the
/// UI requirement in section 2.4 to differentiate temporary and permanent grants.
/// </remarks>
public enum NetworkDecisionAction
{
    /// <summary>Deny the connection. This is the safe default for unknown traffic.</summary>
    Block = 0,

    /// <summary>Allow a single connection attempt only.</summary>
    AllowOnce,

    /// <summary>Allow until the requesting process exits.</summary>
    AllowUntilProcessExit,

    /// <summary>Allow for an explicit, bounded duration.</summary>
    AllowForDuration,

    /// <summary>Allow while a specific user profile is active.</summary>
    AllowForProfile,

    /// <summary>Allow via a persistent rule until explicitly removed.</summary>
    AllowPermanent,
}
