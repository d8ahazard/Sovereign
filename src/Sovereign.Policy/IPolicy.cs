using Sovereign.Contracts;

namespace Sovereign.Policy;

/// <summary>
/// The contract every managed desired-state policy must implement.
/// </summary>
/// <remarks>
/// This is a Milestone 0 placeholder that fixes the shape described in agent_start.md
/// section 8 without performing any system change. Concrete detection, planning, apply,
/// verify, repair, and rollback logic is introduced in Milestone 2. No method here touches
/// privileged machine state in Milestone 0.
/// </remarks>
public interface IPolicy
{
    /// <summary>Gets the stable unique identifier of the policy.</summary>
    string Id { get; }

    /// <summary>Gets the policy schema/content version.</summary>
    int Version { get; }

    /// <summary>Gets a short human-readable title.</summary>
    string Title { get; }

    /// <summary>
    /// Determines the current state of the system relative to this policy.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The detected <see cref="PolicyResultState"/>.</returns>
    ValueTask<PolicyResultState> DetectAsync(CancellationToken cancellationToken);
}
