using System.Globalization;
using System.Text.Json;
using Sovereign.Contracts;
using Sovereign.Storage;

namespace Sovereign.Policy;

/// <summary>
/// The generic, transactional orchestrator for declarative policies (ADR 0004).
/// </summary>
/// <remarks>
/// Derives Detect/Plan/Apply/Verify/Repair/Rollback from a policy's desired settings against an
/// <see cref="ISettingProvider"/>. Apply is idempotent (a no-op plan reports
/// <see cref="PolicyResultState.Compliant"/>) and transactional: it captures originals, persists a
/// restore point, applies each change, verifies each change and the whole policy, and on any failure
/// restores every captured value and verifies the restore. It never reports
/// <see cref="PolicyResultState.Compliant"/> for a failed apply, and treats
/// <see cref="PolicyResultState.Unknown"/>/<see cref="PolicyResultState.Unsupported"/> as not
/// compliant.
/// </remarks>
public sealed class PolicyEngine
{
    private readonly ISettingProvider _provider;
    private readonly IRestorePointStore _restorePoints;
    private readonly IEventStore _events;

    /// <summary>
    /// Creates an engine over the given provider, restore-point store, and audit event store.
    /// </summary>
    /// <param name="provider">The system-state seam the engine reads and mutates.</param>
    /// <param name="restorePoints">Where capture-before-change snapshots are persisted.</param>
    /// <param name="events">The local audit event store.</param>
    public PolicyEngine(ISettingProvider provider, IRestorePointStore restorePoints, IEventStore events)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(restorePoints);
        ArgumentNullException.ThrowIfNull(events);
        this._provider = provider;
        this._restorePoints = restorePoints;
        this._events = events;
    }

    /// <summary>
    /// Determines the current state of a policy without changing anything.
    /// </summary>
    /// <param name="policy">The policy to evaluate.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async ValueTask<PolicyResultState> DetectAsync(IPolicy policy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (!await policy.IsSupportedAsync(cancellationToken).ConfigureAwait(false))
        {
            return PolicyResultState.Unsupported;
        }

        PolicyPlan? plan = await this.TryBuildPlanAsync(policy, cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            // A provider failure means we cannot determine the state. Unknown is never compliant.
            return PolicyResultState.Unknown;
        }

        return plan.IsNoOp ? PolicyResultState.Compliant : PolicyResultState.NonCompliant;
    }

    /// <summary>
    /// Produces a plan preview for a policy. Throws if the provider cannot be read.
    /// </summary>
    /// <param name="policy">The policy to plan.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async ValueTask<PolicyPlan> PlanAsync(IPolicy policy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return await this.BuildPlanAsync(policy, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies a policy transactionally with capture-before-change and verification.
    /// </summary>
    /// <param name="policy">The policy to apply.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async ValueTask<PolicyExecutionReport> ApplyAsync(IPolicy policy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);
        string correlationId = NewCorrelationId();
        string policyId = policy.Metadata.Id;

        if (!await policy.IsSupportedAsync(cancellationToken).ConfigureAwait(false))
        {
            return await this.ReportAsync(policyId, correlationId, PolicyResultState.Unsupported, EmptyPlan(policyId), "Policy is not supported on this system.", cancellationToken).ConfigureAwait(false);
        }

        PolicyPlan? plan = await this.TryBuildPlanAsync(policy, cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            return await this.ReportAsync(policyId, correlationId, PolicyResultState.Unknown, EmptyPlan(policyId), "Could not read current state; the system state is unknown.", cancellationToken).ConfigureAwait(false);
        }

        if (plan.IsNoOp)
        {
            // Idempotent: already in the desired state, so apply is a no-op.
            return await this.ReportAsync(policyId, correlationId, PolicyResultState.Compliant, plan, null, cancellationToken).ConfigureAwait(false);
        }

        // Capture-before-change: snapshot every target's original value before mutating anything.
        var captured = new List<CapturedSetting>(plan.Changes.Count);
        foreach (PolicyChange change in plan.Changes)
        {
            SettingValue current = await this._provider.GetAsync(change.Key, cancellationToken).ConfigureAwait(false);
            captured.Add(new CapturedSetting(change.Key, current.Exists, current.Value));
        }

        string payload = JsonSerializer.Serialize(captured, PolicyJsonContext.Default.ListCapturedSetting);
        await this._restorePoints.SaveAsync(policyId, correlationId, payload, cancellationToken).ConfigureAwait(false);

        try
        {
            foreach (PolicyChange change in plan.Changes)
            {
                await this._provider.SetAsync(change.Key, change.To, cancellationToken).ConfigureAwait(false);

                SettingValue after = await this._provider.GetAsync(change.Key, cancellationToken).ConfigureAwait(false);
                if (after != change.To)
                {
                    throw new PolicyApplyException($"Verification of '{change.Key}' failed after applying.");
                }
            }

            // Independently verify the whole policy rather than trusting the per-step writes.
            PolicyResultState verified = await this.DetectAsync(policy, cancellationToken).ConfigureAwait(false);
            if (verified != PolicyResultState.Compliant)
            {
                throw new PolicyApplyException($"Post-apply verification reported '{verified}', not Compliant.");
            }

            return await this.ReportAsync(policyId, correlationId, PolicyResultState.Applied, plan, null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            bool restored = await this.TryRestoreAsync(captured, cancellationToken).ConfigureAwait(false);
            PolicyResultState state = restored ? PolicyResultState.VerificationFailed : PolicyResultState.RollbackFailed;
            string detail = restored
                ? $"Apply failed and was rolled back: {ex.Message}"
                : $"Apply failed and rollback also failed: {ex.Message}";
            return await this.ReportAsync(policyId, correlationId, state, plan, detail, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Rolls a policy back to its most recent restore point.
    /// </summary>
    /// <param name="policyId">The policy to roll back.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async ValueTask<PolicyExecutionReport> RollbackAsync(string policyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId);
        string correlationId = NewCorrelationId();

        RestorePoint? point = await this._restorePoints.GetLatestAsync(policyId, cancellationToken).ConfigureAwait(false);
        if (point is null)
        {
            return await this.ReportAsync(policyId, correlationId, PolicyResultState.Unknown, EmptyPlan(policyId), "No restore point exists for this policy.", cancellationToken).ConfigureAwait(false);
        }

        List<CapturedSetting>? captured = JsonSerializer.Deserialize(point.PayloadJson, PolicyJsonContext.Default.ListCapturedSetting);
        if (captured is null)
        {
            return await this.ReportAsync(policyId, correlationId, PolicyResultState.RollbackFailed, EmptyPlan(policyId), "The restore point payload could not be read.", cancellationToken).ConfigureAwait(false);
        }

        bool restored = await this.TryRestoreAsync(captured, cancellationToken).ConfigureAwait(false);
        PolicyResultState state = restored ? PolicyResultState.Applied : PolicyResultState.RollbackFailed;
        string? detail = restored ? null : "One or more settings could not be restored.";
        return await this.ReportAsync(policyId, correlationId, state, EmptyPlan(policyId), detail, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<bool> TryRestoreAsync(List<CapturedSetting> captured, CancellationToken cancellationToken)
    {
        bool allRestored = true;
        for (int i = captured.Count - 1; i >= 0; i--)
        {
            CapturedSetting item = captured[i];
            SettingValue original = item.ToSettingValue();
            try
            {
                await this._provider.SetAsync(item.Key, original, cancellationToken).ConfigureAwait(false);
                SettingValue after = await this._provider.GetAsync(item.Key, cancellationToken).ConfigureAwait(false);
                if (after != original)
                {
                    allRestored = false;
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
            {
                allRestored = false;
            }
        }

        return allRestored;
    }

    private async ValueTask<PolicyPlan?> TryBuildPlanAsync(IPolicy policy, CancellationToken cancellationToken)
    {
        try
        {
            return await this.BuildPlanAsync(policy, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
        {
            return null;
        }
    }

    private async ValueTask<PolicyPlan> BuildPlanAsync(IPolicy policy, CancellationToken cancellationToken)
    {
        IReadOnlyList<DesiredSetting> desired = await policy.GetDesiredSettingsAsync(cancellationToken).ConfigureAwait(false);
        var changes = new List<PolicyChange>();
        foreach (DesiredSetting setting in desired)
        {
            SettingValue current = await this._provider.GetAsync(setting.Key, cancellationToken).ConfigureAwait(false);
            if (current != setting.Desired)
            {
                changes.Add(new PolicyChange(setting.Key, current, setting.Desired, setting.Explanation));
            }
        }

        return new PolicyPlan(policy.Metadata.Id, changes);
    }

    private async ValueTask<PolicyExecutionReport> ReportAsync(
        string policyId,
        string correlationId,
        PolicyResultState state,
        PolicyPlan plan,
        string? failureDetail,
        CancellationToken cancellationToken)
    {
        string message = string.Create(
            CultureInfo.InvariantCulture,
            $"policy={policyId} correlation={correlationId} state={state} changes={plan.Changes.Count}{(failureDetail is null ? string.Empty : $" detail={failureDetail}")}");
        await this._events.AppendAsync("policy", message, cancellationToken).ConfigureAwait(false);
        return new PolicyExecutionReport(policyId, correlationId, state, plan, failureDetail);
    }

    private static PolicyPlan EmptyPlan(string policyId) => new(policyId, Array.Empty<PolicyChange>());

    private static string NewCorrelationId() => Guid.NewGuid().ToString("N");
}

/// <summary>
/// Internal signal that a policy apply step failed and must trigger rollback.
/// </summary>
public sealed class PolicyApplyException : Exception
{
    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The failure message.</param>
    public PolicyApplyException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and inner exception.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying cause.</param>
    public PolicyApplyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception with no message.</summary>
    public PolicyApplyException()
    {
    }
}
