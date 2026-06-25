namespace Sovereign.Policy;

/// <summary>
/// A concrete <see cref="IPolicy"/> defined entirely by static metadata and a fixed set of desired
/// settings, optionally gated by a supportability check.
/// </summary>
/// <remarks>
/// This is the common shape for declarative policies: no per-policy detect/apply/rollback code is
/// needed because the <see cref="PolicyEngine"/> derives all of that from the desired settings.
/// </remarks>
public sealed class DeclarativeSettingPolicy : IPolicy
{
    private readonly IReadOnlyList<DesiredSetting> _desired;
    private readonly Func<CancellationToken, ValueTask<bool>> _isSupported;

    /// <summary>
    /// Creates a declarative policy.
    /// </summary>
    /// <param name="metadata">The policy metadata.</param>
    /// <param name="desiredSettings">The desired settings (keys must be unique).</param>
    /// <param name="isSupported">Optional supportability check; defaults to always supported.</param>
    public DeclarativeSettingPolicy(
        PolicyMetadata metadata,
        IReadOnlyList<DesiredSetting> desiredSettings,
        Func<CancellationToken, ValueTask<bool>>? isSupported = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(desiredSettings);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (DesiredSetting setting in desiredSettings)
        {
            if (!seen.Add(setting.Key))
            {
                throw new ArgumentException($"Duplicate setting key '{setting.Key}' in policy '{metadata.Id}'.", nameof(desiredSettings));
            }
        }

        this.Metadata = metadata;
        this._desired = desiredSettings;
        this._isSupported = isSupported ?? (static _ => ValueTask.FromResult(true));
    }

    /// <inheritdoc />
    public PolicyMetadata Metadata { get; }

    /// <inheritdoc />
    public ValueTask<bool> IsSupportedAsync(CancellationToken cancellationToken) => this._isSupported(cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<DesiredSetting>> GetDesiredSettingsAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(this._desired);
}
