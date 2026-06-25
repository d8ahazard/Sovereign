using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Sovereign.Contracts.Ipc;

namespace Sovereign.Service;

/// <summary>
/// Enumerates classic (Win32/MSI) installed programs from the machine-wide uninstall registry and
/// uninstalls them through their own registered uninstaller. This is what covers the things the
/// Appx inventory cannot: preinstalled antivirus trials (McAfee/Norton/...), OEM "assistant" bloat
/// (SupportAssist, Vantage, JumpStart, ...), partner promos, and the desktop Office/Microsoft 365
/// install.
/// </summary>
/// <remarks>
/// <para>
/// Enumeration is read-only and reads only the standard uninstall roots under <c>HKLM</c> (64-bit and
/// 32-bit views). Windows updates, hotfixes, hidden system components, and patch sub-entries are
/// filtered out so the list stays meaningful.
/// </para>
/// <para>
/// Removal never trusts a command supplied by the client. The client sends back an opaque id that
/// identifies a registry subkey; the service re-opens that key under a known root, reads the
/// program's own <c>QuietUninstallString</c>/<c>UninstallString</c> (or builds a silent
/// <c>msiexec /x</c> for MSI products), and runs it. Uninstalling a classic program is <b>not</b>
/// reversible the way a registry policy is, which is surfaced honestly in the UI and audited.
/// </para>
/// </remarks>
public sealed partial class Win32ProgramManager(ILogger<Win32ProgramManager> logger)
{
    private const string UninstallRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private static readonly TimeSpan RemoveTimeout = TimeSpan.FromMinutes(5);

    private readonly ILogger<Win32ProgramManager> _logger = logger;

    /// <summary>Vendor/name fragments that mark a program as preinstalled bloat, with a friendly category and blurb.</summary>
    private static readonly BloatRule[] BloatRules =
    [
        // Antivirus / security trials
        new("McAfee", "Security trials", "Preinstalled McAfee security trial. Nags for a subscription and overlaps with the built-in Windows Defender."),
        new("Norton", "Security trials", "Preinstalled Norton security trial that overlaps with the built-in Windows Defender."),
        new("Symantec", "Security trials", "Symantec/Norton security software, usually a preinstalled trial."),
        new("Avast", "Security trials", "Preinstalled Avast antivirus, usually a trial with upsells."),
        new("AVG", "Security trials", "Preinstalled AVG antivirus, usually a trial with upsells."),
        new("Webroot", "Security trials", "Preinstalled Webroot security trial."),
        new("Avira", "Security trials", "Preinstalled Avira antivirus trial."),
        // OEM manufacturer bloat
        new("SupportAssist", "Manufacturer bloat", "Dell SupportAssist: background OEM 'support' agent most people never use."),
        new("Dell Customer Connect", "Manufacturer bloat", "Dell marketing/notification app."),
        new("Dell Digital Delivery", "Manufacturer bloat", "Dell app for delivering preinstalled OEM software."),
        new("Dell Optimizer", "Manufacturer bloat", "Dell background tuning agent."),
        new("MyDell", "Manufacturer bloat", "Dell companion/marketing app."),
        new("Dell Update", "Manufacturer bloat", "Dell's own updater (Windows Update covers most drivers)."),
        new("Lenovo Vantage", "Manufacturer bloat", "Lenovo's companion app with ads and a background agent."),
        new("Lenovo Now", "Manufacturer bloat", "Lenovo welcome/marketing app."),
        new("Lenovo Welcome", "Manufacturer bloat", "Lenovo first-run marketing app."),
        new("Lenovo Utility", "Manufacturer bloat", "Lenovo hotkey/utility helper."),
        new("Lenovo Smart", "Manufacturer bloat", "Lenovo companion app."),
        new("HP JumpStart", "Manufacturer bloat", "HP first-run marketing app."),
        new("HP Support Assistant", "Manufacturer bloat", "HP background 'support' agent."),
        new("HP Connection Optimizer", "Manufacturer bloat", "HP background network agent."),
        new("HP Documentation", "Manufacturer bloat", "HP marketing/documentation stub."),
        new("HP Sure", "Manufacturer bloat", "HP background security/telemetry agent."),
        new("MyASUS", "Manufacturer bloat", "ASUS companion app with a background agent."),
        new("ASUS GiftBox", "Manufacturer bloat", "ASUS preinstalled promotions."),
        new("ASUS Product Register", "Manufacturer bloat", "ASUS registration/marketing app."),
        new("Acer Care Center", "Manufacturer bloat", "Acer companion app."),
        new("Acer Jumpstart", "Manufacturer bloat", "Acer first-run marketing app."),
        // Partner promos / preinstalled games
        new("WildTangent", "Preinstalled games", "WildTangent ad-supported game launcher/promotion."),
        new("Booking.com", "Partner promos", "Preinstalled Booking.com promotion."),
        new("ExpressVPN", "Partner promos", "Preinstalled ExpressVPN promotion."),
        new("Amazon", "Partner promos", "Preinstalled Amazon shopping promotion."),
    ];

    /// <summary>Programs we surface but never recommend (the user may rely on them); category only.</summary>
    private static readonly (string Fragment, string Category)[] CategoryRules =
    [
        ("Microsoft 365", "Office"),
        ("Microsoft Office", "Office"),
        ("OneNote", "Office"),
        ("Microsoft Edge", "Browser"),
        ("Google Chrome", "Browser"),
        ("Mozilla Firefox", "Browser"),
        ("Dropbox", "Cloud storage"),
        ("Spotify", "Media"),
        ("Zoom", "Communication"),
    ];

    [GeneratedRegex(@"^(64|32)\|(.+)$")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"^\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\}$")]
    private static partial Regex MsiGuidPattern();

    [GeneratedRegex(@"^(KB\d{6,}|Security Update|Update for|Hotfix)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex UpdatePattern();

    /// <summary>Enumerates installed classic programs, recommended-for-removal first.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task<IReadOnlyList<AppInfo>> ListAsync(CancellationToken cancellationToken)
    {
        LogEnumerating(this._logger);
        return await Task.Run(Enumerate, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Uninstalls a classic program identified by an opaque enumeration id.</summary>
    /// <param name="programId">The id handed out by <see cref="ListAsync"/> (form <c>view|subkey</c>).</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task<AppActionResult> RemoveAsync(string programId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(programId) || IdPattern().Match(programId) is not { Success: true } match)
        {
            return new AppActionResult(programId, false, "Invalid program id.");
        }

        RegistryView view = match.Groups[1].Value == "32" ? RegistryView.Registry32 : RegistryView.Registry64;
        string subKeyName = match.Groups[2].Value;
        if (subKeyName.Contains('\\', StringComparison.Ordinal) || subKeyName.Length > 256)
        {
            return new AppActionResult(programId, false, "Invalid program id.");
        }

        using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        using RegistryKey? key = baseKey.OpenSubKey($@"{UninstallRoot}\{subKeyName}", writable: false);
        if (key is null)
        {
            return new AppActionResult(programId, false, "This program is no longer installed.");
        }

        string displayName = (key.GetValue("DisplayName") as string) ?? subKeyName;
        if (!TryBuildUninstall(subKeyName, key, out string fileName, out string arguments))
        {
            return new AppActionResult(programId, false, $"{displayName} does not expose a way to uninstall it automatically.");
        }

        LogRemoving(this._logger, displayName);
        try
        {
            int exitCode = await RunProcessAsync(fileName, arguments, cancellationToken).ConfigureAwait(false);

            // 0 = success, 3010 = success but a reboot is needed (common for MSI/AV).
            return exitCode is 0 or 3010
                ? new AppActionResult(programId, true, exitCode == 3010 ? "Removed. A reboot is recommended to finish." : null)
                : new AppActionResult(programId, false, $"The uninstaller exited with code {exitCode}.");
        }
        catch (TimeoutException)
        {
            return new AppActionResult(programId, false, "The uninstaller did not finish in time (it may need to be run interactively).");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new AppActionResult(programId, false, $"Could not start the uninstaller: {ex.Message}");
        }
    }

    private static List<AppInfo> Enumerate()
    {
        var apps = new List<AppInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using RegistryKey? root = baseKey.OpenSubKey(UninstallRoot, writable: false);
            if (root is null)
            {
                continue;
            }

            foreach (string subKeyName in root.GetSubKeyNames())
            {
                using RegistryKey? key = root.OpenSubKey(subKeyName, writable: false);
                if (key is null)
                {
                    continue;
                }

                AppInfo? info = BuildEntry(view, subKeyName, key, seen);
                if (info is not null)
                {
                    apps.Add(info);
                }
            }
        }

        return apps
            .OrderByDescending(a => a.Recommended)
            .ThenBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AppInfo? BuildEntry(RegistryView view, string subKeyName, RegistryKey key, HashSet<string> seen)
    {
        string displayName = (key.GetValue("DisplayName") as string)?.Trim() ?? string.Empty;
        if (displayName.Length == 0)
        {
            return null;
        }

        if (GetDword(key, "SystemComponent") == 1)
        {
            return null;
        }

        if (key.GetValue("ParentKeyName") is not null || key.GetValue("ParentDisplayName") is not null)
        {
            return null;
        }

        string releaseType = (key.GetValue("ReleaseType") as string) ?? string.Empty;
        if (releaseType is "Security Update" or "Update Rollup" or "Hotfix" || UpdatePattern().IsMatch(displayName))
        {
            return null;
        }

        bool hasUninstall = key.GetValue("UninstallString") is string u && !string.IsNullOrWhiteSpace(u)
            || key.GetValue("QuietUninstallString") is string q && !string.IsNullOrWhiteSpace(q);
        if (!hasUninstall)
        {
            return null;
        }

        string version = (key.GetValue("DisplayVersion") as string) ?? string.Empty;
        if (!seen.Add(displayName + "\u0001" + version))
        {
            return null;
        }

        string publisher = ((key.GetValue("Publisher") as string) ?? string.Empty).Trim();
        Classification classification = Classify(displayName, publisher);
        string id = $"{(view == RegistryView.Registry32 ? "32" : "64")}|{subKeyName}";

        return new AppInfo(
            PackageFullName: id,
            PackageFamilyName: string.Empty,
            Name: displayName,
            DisplayName: displayName,
            Description: classification.Description,
            Publisher: publisher.Length == 0 ? "Unknown publisher" : publisher,
            Category: classification.Category,
            Recommended: classification.Recommended,
            Removable: true,
            IsSystem: false,
            HasStartEntry: true,
            IconBase64: null,
            Kind: "win32",
            Reversible: false);
    }

    private static Classification Classify(string displayName, string publisher)
    {
        foreach (BloatRule rule in BloatRules)
        {
            if (displayName.Contains(rule.Fragment, StringComparison.OrdinalIgnoreCase)
                || publisher.Contains(rule.Fragment, StringComparison.OrdinalIgnoreCase))
            {
                return new Classification(rule.Category, rule.Description, Recommended: true);
            }
        }

        foreach ((string fragment, string category) in CategoryRules)
        {
            if (displayName.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return new Classification(category, "An installed desktop program.", Recommended: false);
            }
        }

        return new Classification("Installed program", "An installed desktop program.", Recommended: false);
    }

    private static bool TryBuildUninstall(string subKeyName, RegistryKey key, out string fileName, out string arguments)
    {
        fileName = string.Empty;
        arguments = string.Empty;

        // Prefer a clean silent MSI removal when the product is an MSI (subkey is the product GUID).
        if (MsiGuidPattern().IsMatch(subKeyName))
        {
            fileName = "msiexec.exe";
            arguments = $"/x {subKeyName} /qn /norestart";
            return true;
        }

        if (key.GetValue("QuietUninstallString") is string quiet && !string.IsNullOrWhiteSpace(quiet))
        {
            return TrySplitCommand(quiet, out fileName, out arguments);
        }

        if (key.GetValue("UninstallString") is string uninstall && !string.IsNullOrWhiteSpace(uninstall))
        {
            // Rewrite a bare MSI uninstall to a silent one; otherwise run as the vendor specified.
            string trimmed = uninstall.Trim();
            if (trimmed.StartsWith("MsiExec", StringComparison.OrdinalIgnoreCase))
            {
                string rewritten = trimmed
                    .Replace("/I", "/X", StringComparison.OrdinalIgnoreCase)
                    .Replace("/i", "/X", StringComparison.Ordinal);
                if (!rewritten.Contains("/qn", StringComparison.OrdinalIgnoreCase) && !rewritten.Contains("/quiet", StringComparison.OrdinalIgnoreCase))
                {
                    rewritten += " /qn /norestart";
                }

                return TrySplitCommand(rewritten, out fileName, out arguments);
            }

            return TrySplitCommand(trimmed, out fileName, out arguments);
        }

        return false;
    }

    private static bool TrySplitCommand(string command, out string fileName, out string arguments)
    {
        fileName = string.Empty;
        arguments = string.Empty;
        string trimmed = command.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed[0] == '"')
        {
            int end = trimmed.IndexOf('"', 1);
            if (end < 0)
            {
                return false;
            }

            fileName = trimmed[1..end];
            arguments = trimmed[(end + 1)..].Trim();
        }
        else
        {
            int space = trimmed.IndexOf(' ', StringComparison.Ordinal);
            if (space < 0)
            {
                fileName = trimmed;
            }
            else
            {
                fileName = trimmed[..space];
                arguments = trimmed[(space + 1)..].Trim();
            }
        }

        return fileName.Length > 0;
    }

    private static async Task<int> RunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RemoveTimeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            throw new TimeoutException("The uninstaller timed out.");
        }

        return process.ExitCode;
    }

    private static int GetDword(RegistryKey key, string name) =>
        key.GetValue(name) is int value ? value : 0;

    private readonly record struct BloatRule(string Fragment, string Category, string Description);

    private readonly record struct Classification(string Category, string Description, bool Recommended);

    [LoggerMessage(EventId = 410, Level = LogLevel.Debug, Message = "Enumerating installed Win32 programs.")]
    private static partial void LogEnumerating(ILogger logger);

    [LoggerMessage(EventId = 411, Level = LogLevel.Information, Message = "Uninstalling Win32 program {Program}.")]
    private static partial void LogRemoving(ILogger logger, string program);
}
