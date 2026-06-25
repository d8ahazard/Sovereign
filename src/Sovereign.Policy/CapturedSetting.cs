using System.Text.Json.Serialization;

namespace Sovereign.Policy;

/// <summary>
/// The captured original value of a single setting, recorded before a policy is applied so the
/// change can be reversed (capture-before-change; see docs/reversibility.md).
/// </summary>
/// <param name="Key">The setting key.</param>
/// <param name="Existed">Whether the setting existed before the change.</param>
/// <param name="Value">The original value when <paramref name="Existed"/> is true; otherwise null.</param>
public sealed record CapturedSetting(string Key, bool Existed, string? Value)
{
    /// <summary>Converts this capture back to a <see cref="SettingValue"/> for restoration.</summary>
    public SettingValue ToSettingValue() => this.Existed ? SettingValue.Present(this.Value!) : SettingValue.Absent;
}

/// <summary>
/// Source-generated JSON context for restore-point payloads, keeping serialization trim- and
/// self-contained-safe (no reflection-based serialization).
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(IReadOnlyList<CapturedSetting>))]
[JsonSerializable(typeof(List<CapturedSetting>))]
[JsonSerializable(typeof(CapturedSetting))]
public sealed partial class PolicyJsonContext : JsonSerializerContext
{
}
