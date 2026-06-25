namespace Sovereign.Policy;

/// <summary>
/// A managed desired-state policy.
/// </summary>
/// <remarks>
/// Policies are declarative: they expose metadata, a supportability check, and the set of desired
/// settings they want. The <see cref="PolicyEngine"/> derives Detect, Plan, Apply, Verify, Repair,
/// and Rollback generically from those settings against an <see cref="ISettingProvider"/>, so
/// idempotency and reversibility are structural rather than per-policy discipline (ADR 0004).
/// A policy itself performs no I/O and touches no privileged state.
/// </remarks>
public interface IPolicy
{
    /// <summary>Gets the policy's static metadata.</summary>
    PolicyMetadata Metadata { get; }

    /// <summary>
    /// Determines whether this policy is supported on the current system.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>True if supported; false to report <c>Unsupported</c>.</returns>
    ValueTask<bool> IsSupportedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns the settings this policy wants in their desired state.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The desired settings (keys must be unique within a policy).</returns>
    ValueTask<IReadOnlyList<DesiredSetting>> GetDesiredSettingsAsync(CancellationToken cancellationToken);
}
