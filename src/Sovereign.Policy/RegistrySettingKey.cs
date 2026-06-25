namespace Sovereign.Policy;

/// <summary>
/// Encodes a Windows registry value location as an opaque <see cref="ISettingProvider"/> key.
/// </summary>
/// <remarks>
/// The engine and policies stay infrastructure-independent (settings are just string keys). A
/// registry-backed provider decodes these keys and performs the real reads/writes. The encoded form
/// is <c>{hive}\{subKey}|{valueName}|{kind}</c>, for example
/// <c>HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent|DisableWindowsConsumerFeatures|dword</c>.
/// Registry value names used by these policies never contain the <c>|</c> separator.
/// </remarks>
public static class RegistrySettingKey
{
    /// <summary>The separator between the registry path, value name, and value kind.</summary>
    public const char Separator = '|';

    /// <summary>Encodes a REG_DWORD value location.</summary>
    /// <param name="hiveAndSubKey">The hive-qualified key path, for example <c>HKLM\SOFTWARE\...</c>.</param>
    /// <param name="valueName">The registry value name.</param>
    public static string Dword(string hiveAndSubKey, string valueName) =>
        Encode(hiveAndSubKey, valueName, "dword");

    /// <summary>Encodes a REG_SZ value location.</summary>
    /// <param name="hiveAndSubKey">The hive-qualified key path, for example <c>HKLM\SOFTWARE\...</c>.</param>
    /// <param name="valueName">The registry value name.</param>
    public static string Sz(string hiveAndSubKey, string valueName) =>
        Encode(hiveAndSubKey, valueName, "string");

    /// <summary>Returns whether a setting key is a registry key this scheme owns.</summary>
    /// <param name="key">The setting key.</param>
    public static bool IsRegistryKey(string key) =>
        key is not null && (key.StartsWith(@"HKLM\", StringComparison.Ordinal) || key.StartsWith(@"HKCU\", StringComparison.Ordinal));

    private static string Encode(string hiveAndSubKey, string valueName, string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hiveAndSubKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        if (hiveAndSubKey.Contains(Separator, StringComparison.Ordinal) || valueName.Contains(Separator, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Registry key parts must not contain '{Separator}'.", nameof(hiveAndSubKey));
        }

        return $"{hiveAndSubKey}{Separator}{valueName}{Separator}{kind}";
    }
}
