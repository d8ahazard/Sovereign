namespace Sovereign.Policy;

/// <summary>
/// The value of a managed setting, which may be present (with a string value) or absent.
/// </summary>
/// <remarks>
/// Settings are modeled as string-valued keys so the engine stays infrastructure-independent. A
/// concrete <see cref="ISettingProvider"/> maps keys to real backing state (an in-memory sandbox in
/// Milestone 2; registry/Appx in Milestone 5).
/// </remarks>
public sealed record SettingValue
{
    private SettingValue(bool exists, string? value)
    {
        this.Exists = exists;
        this.Value = value;
    }

    /// <summary>Gets a value indicating whether the setting is present.</summary>
    public bool Exists { get; }

    /// <summary>Gets the string value when <see cref="Exists"/> is true; otherwise null.</summary>
    public string? Value { get; }

    /// <summary>A setting that is absent.</summary>
    public static SettingValue Absent { get; } = new(false, null);

    /// <summary>
    /// Creates a present value.
    /// </summary>
    /// <param name="value">The non-null string value.</param>
    public static SettingValue Present(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new SettingValue(true, value);
    }
}
