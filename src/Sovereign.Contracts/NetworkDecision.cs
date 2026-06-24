namespace Sovereign.Contracts;

/// <summary>
/// A skeleton representation of an outbound network decision and the evidence behind it.
/// </summary>
/// <remarks>
/// This is a Milestone 0 placeholder establishing the contract boundary only. The full
/// field set required by agent_start.md section 9 (executable hash, publisher identity,
/// service identity, app container, resolved hostname with evidence, rule source/priority,
/// lifetime, profile, actor, timestamps, expiration, explanation) will be added in later
/// milestones alongside the enforcement engine. No system access is performed here.
/// </remarks>
/// <param name="Action">The decision applied to the connection attempt.</param>
/// <param name="ExecutablePath">The full path of the executable that initiated the attempt, if known.</param>
/// <param name="RemoteHost">The destination hostname when evidence exists; otherwise <see langword="null"/>.</param>
/// <param name="Explanation">A human-readable reason for the decision.</param>
public sealed record NetworkDecision(
    NetworkDecisionAction Action,
    string? ExecutablePath,
    string? RemoteHost,
    string Explanation)
{
    /// <summary>
    /// Creates the safe default decision for an unattributed or unknown connection attempt:
    /// blocked, with no asserted hostname. Used to guarantee fail-closed behavior when no
    /// explicit allow rule matches.
    /// </summary>
    /// <param name="executablePath">The initiating executable path, if known.</param>
    /// <returns>A blocking <see cref="NetworkDecision"/>.</returns>
    public static NetworkDecision DefaultDeny(string? executablePath = null) =>
        new(NetworkDecisionAction.Block, executablePath, RemoteHost: null,
            Explanation: "No explicit allow rule matched; blocked by default-deny policy.");

    /// <summary>
    /// Gets a value indicating whether this decision permits the connection in any form.
    /// </summary>
    public bool IsAllowed => this.Action != NetworkDecisionAction.Block;
}
