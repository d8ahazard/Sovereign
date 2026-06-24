namespace Sovereign.Contracts;

/// <summary>
/// The outcome of evaluating, applying, verifying, or rolling back a managed policy.
/// </summary>
/// <remarks>
/// Per agent_start.md section 8, policy results must distinguish these states explicitly,
/// and <see cref="Unknown"/> must never be treated as compliant. The fail-closed posture
/// of the product depends on these states being kept distinct; do not collapse them.
/// </remarks>
public enum PolicyResultState
{
    /// <summary>The result could not be determined. Must never be treated as compliant.</summary>
    Unknown = 0,

    /// <summary>The system already matches the desired state.</summary>
    Compliant,

    /// <summary>The system does not match the desired state.</summary>
    NonCompliant,

    /// <summary>The desired state was applied successfully.</summary>
    Applied,

    /// <summary>Only some steps of a multi-step policy were applied.</summary>
    PartiallyApplied,

    /// <summary>The policy is not supported on this Windows edition or build.</summary>
    Unsupported,

    /// <summary>Application appeared to succeed but post-apply verification failed.</summary>
    VerificationFailed,

    /// <summary>An attempt to roll back a change failed; residual drift may remain.</summary>
    RollbackFailed,

    /// <summary>The change requires a reboot to take full effect.</summary>
    RequiresReboot,

    /// <summary>The change requires an explicit user action to proceed.</summary>
    RequiresUserAction,
}
