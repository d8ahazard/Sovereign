using System.Globalization;
using Microsoft.Win32;
using Sovereign.Policy;

namespace Sovereign.Service;

/// <summary>
/// An <see cref="ISettingProvider"/> backed by the real Windows registry.
/// </summary>
/// <remarks>
/// <para>
/// Decodes the opaque registry keys produced by <see cref="RegistrySettingKey"/> and performs real
/// reads and writes under the local machine (or current user) hive. The service runs as LocalSystem,
/// so it has the rights to write the machine-wide policy keys these policies target.
/// </para>
/// <para>
/// Reversibility is structural: the engine captures the current value (present or absent) before any
/// write, so restoring "absent" deletes the value and returns Windows to its default behavior. This
/// provider therefore performs no privileged action on its own; it only does what an already-planned,
/// user-approved change asks for.
/// </para>
/// </remarks>
public sealed class RegistrySettingProvider : ISettingProvider
{
    /// <inheritdoc />
    public ValueTask<SettingValue> GetAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        RegistryTarget target = RegistryTarget.Parse(key);
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(target.Hive, RegistryView.Registry64);
        using RegistryKey? subKey = baseKey.OpenSubKey(target.SubKey, writable: false);
        if (subKey is null)
        {
            return ValueTask.FromResult(SettingValue.Absent);
        }

        object? raw = subKey.GetValue(target.ValueName, defaultValue: null);
        if (raw is null)
        {
            return ValueTask.FromResult(SettingValue.Absent);
        }

        string text = target.Kind == RegistryValueKind.DWord
            ? Convert.ToInt64(raw, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
            : Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;

        return ValueTask.FromResult(SettingValue.Present(text));
    }

    /// <inheritdoc />
    public ValueTask SetAsync(string key, SettingValue value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        RegistryTarget target = RegistryTarget.Parse(key);
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(target.Hive, RegistryView.Registry64);

        if (!value.Exists)
        {
            // Restoring "absent" means deleting the value, which returns Windows to its default.
            using RegistryKey? subKey = baseKey.OpenSubKey(target.SubKey, writable: true);
            subKey?.DeleteValue(target.ValueName, throwOnMissingValue: false);
            return ValueTask.CompletedTask;
        }

        using RegistryKey subKeyToWrite = baseKey.CreateSubKey(target.SubKey, writable: true);
        if (target.Kind == RegistryValueKind.DWord)
        {
            int dword = int.Parse(value.Value!, NumberStyles.Integer, CultureInfo.InvariantCulture);
            subKeyToWrite.SetValue(target.ValueName, dword, RegistryValueKind.DWord);
        }
        else
        {
            subKeyToWrite.SetValue(target.ValueName, value.Value!, RegistryValueKind.String);
        }

        return ValueTask.CompletedTask;
    }

    private readonly record struct RegistryTarget(RegistryHive Hive, string SubKey, string ValueName, RegistryValueKind Kind)
    {
        public static RegistryTarget Parse(string key)
        {
            string[] parts = key.Split(RegistrySettingKey.Separator);
            if (parts.Length != 3)
            {
                throw new ArgumentException($"Malformed registry setting key '{key}'.", nameof(key));
            }

            string path = parts[0];
            string valueName = parts[1];
            RegistryValueKind kind = parts[2] switch
            {
                "dword" => RegistryValueKind.DWord,
                "string" => RegistryValueKind.String,
                _ => throw new ArgumentException($"Unsupported registry value kind '{parts[2]}'.", nameof(key)),
            };

            int slash = path.IndexOf('\\', StringComparison.Ordinal);
            if (slash <= 0)
            {
                throw new ArgumentException($"Registry path '{path}' is missing a hive or subkey.", nameof(key));
            }

            string hiveText = path[..slash];
            string subKey = path[(slash + 1)..];
            RegistryHive hive = hiveText switch
            {
                "HKLM" => RegistryHive.LocalMachine,
                "HKCU" => RegistryHive.CurrentUser,
                _ => throw new ArgumentException($"Unsupported registry hive '{hiveText}'.", nameof(key)),
            };

            return new RegistryTarget(hive, subKey, valueName, kind);
        }
    }
}
