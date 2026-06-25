using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Sovereign.Contracts.Ipc;

namespace Sovereign.Service;

/// <summary>
/// Enumerates installed Appx/MSIX packages and removes them for all users.
/// </summary>
/// <remarks>
/// <para>
/// Enumeration uses the in-box <c>Appx</c> PowerShell module for the package inventory and then
/// enriches each entry in-process: friendly display names (manifest + indirect string resolution),
/// curated titles/descriptions for known bloat, a resolved logo, and recommended/protected/system
/// classification. Background/system packages that are not user-facing and not recommended bloat are
/// filtered out so the list stays meaningful. The service runs as LocalSystem, which is required both
/// for the all-users scope and to read package assets under <c>WindowsApps</c>.
/// </para>
/// <para>
/// Removing an app is <b>not</b> auto-reversible the way a registry policy is; the original bits are
/// gone. Removal is therefore gated on an explicit user choice, refuses protected packages, validates
/// the package name against a strict allow-list before it reaches a shell, and is audited.
/// </para>
/// </remarks>
public sealed partial class AppxManager(ILogger<AppxManager> logger)
{
    private static readonly TimeSpan ListTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan RemoveTimeout = TimeSpan.FromSeconds(120);

    // Total icon payload budget so a ListApps response stays comfortably under the 1 MB IPC frame cap.
    private const int IconByteBudget = 650 * 1024;
    private const int MaxSingleIconBytes = 48 * 1024;

    private readonly ILogger<AppxManager> _logger = logger;

    /// <summary>Apps Sovereign preselects as bloat. Matched case-insensitively against the package name.</summary>
    private static readonly string[] BloatFragments =
    [
        "BingWeather", "BingNews", "BingFinance", "BingSports", "BingSearch", "BingTranslator",
        "GamingApp", "ZuneMusic", "ZuneVideo", "YourPhone", "Windows.Phone", "Microsoft.People",
        "windowscommunicationsapps", "SolitaireCollection", "MicrosoftOfficeHub", "SkypeApp",
        "GetHelp", "Getstarted", "Todos", "PowerAutomateDesktop", "WindowsFeedbackHub",
        "WindowsMaps", "Teams", "MSTeams", "Clipchamp", "OutlookForWindows", "Copilot", "549981C3F5F10",
        "3DBuilder", "3DViewer", "Print3D", "MixedReality", "MicrosoftJournal", "OneConnect", "Wallet",
        "Microsoft.News", "DevHome", "PowerBI", "NetworkSpeedTest", "Office.OneNote", "Microsoft.Messaging",
        "Spotify", "Disney", "Facebook", "Instagram", "TikTok", "CandyCrush", "king.com", "WhatsApp",
        "Netflix", "AmazonVideo", "PrimeVideo", "Twitter", "LinkedIn", "Dolby", "Family", "Hidden",
    ];

    /// <summary>Packages that must never be offered for removal (would break the OS or core experiences).</summary>
    private static readonly string[] ProtectedFragments =
    [
        "WindowsStore", "StorePurchaseApp", "DesktopAppInstaller", "SecHealthUI", "Defender",
        "ShellExperienceHost", "StartMenuExperienceHost", "AccountsControl", "LockApp",
        "ContentDeliveryManager", "BioEnrollment", "CredDialogHost", "Win32WebViewHost",
        "CapturePicker", "AsyncTextService", "PeopleExperienceHost", "PinningConfirmationDialog",
        "SearchHost", "Windows.Search", "Windows.CBSPreview", "XboxGameCallableUI",
        "Microsoft.UI.Xaml", "Microsoft.NET", "Microsoft.Services.Store", "Microsoft.AAD",
        "Windows.Photos", "WindowsCalculator", "WindowsNotepad", "Microsoft.Paint",
        "ScreenSketch", "WindowsTerminal", "ImmersiveControlPanel", "WebpImageExtension",
        "HEIFImageExtension", "VP9VideoExtensions", "WebMediaExtensions", "RawImageExtension",
        "AVCEncoderVideoExtension", "HEVCVideoExtension", "MPEG2VideoExtension", "VCLibs",
        "LanguageExperiencePack", "WindowsAppRuntime", "NotepadPlusPlus",
    ];

    /// <summary>Frameworks and runtimes that are not user-facing apps; excluded from the list entirely.</summary>
    private static readonly string[] FrameworkFragments =
    [
        "VCLibs", "NET.Native", "UI.Xaml.2", "UI.Xaml.CBS", "WindowsAppRuntime", "DXProvider",
        ".Runtime.", "WebView2", "Microsoft.Advertising", "Microsoft.Services.Store.Engagement",
    ];

    /// <summary>Friendly titles, descriptions, and categories for well-known packages (substring match on name).</summary>
    private static readonly CuratedApp[] Catalog =
    [
        new("BingWeather", "Weather", "MSN Weather app with ads.", "News & weather"),
        new("BingNews", "News", "The MSN News feed, with ads and sponsored stories.", "News & weather"),
        new("BingFinance", "Money", "MSN Money / finance app.", "News & weather"),
        new("BingSports", "Sports", "MSN Sports app.", "News & weather"),
        new("BingSearch", "Bing Search", "Bing web-search integration for the search box.", "Search"),
        new("BingTranslator", "Translator", "Bing-powered translator app.", "Utility"),
        new("WindowsMaps", "Maps", "Windows Maps. Most people just use a browser map.", "Maps"),
        new("ZuneMusic", "Media Player (Groove)", "The Groove / Media Player music app.", "Media"),
        new("ZuneVideo", "Movies & TV", "Microsoft's video-store and player app.", "Media"),
        new("YourPhone", "Phone Link", "Links your Android/iPhone to Windows.", "Connectivity"),
        new("Windows.Phone", "Phone Link", "Links your Android/iPhone to Windows.", "Connectivity"),
        new("Microsoft.People", "People", "Legacy contacts app.", "Productivity"),
        new("windowscommunicationsapps", "Mail & Calendar", "The old Mail and Calendar apps (replaced by Outlook).", "Productivity"),
        new("SolitaireCollection", "Solitaire Collection", "Ad-supported Microsoft card games.", "Game"),
        new("MicrosoftOfficeHub", "Microsoft 365 (Office hub)", "An upsell hub for Microsoft 365.", "Microsoft 365"),
        new("Office.OneNote", "OneNote (Store)", "The Store version of OneNote.", "Microsoft 365"),
        new("SkypeApp", "Skype", "Preinstalled Skype.", "Communication"),
        new("Getstarted", "Tips", "Windows tips-and-tricks app.", "System"),
        new("GetHelp", "Get Help", "Microsoft support / help app.", "System"),
        new("Todos", "Microsoft To Do", "To-do list app.", "Productivity"),
        new("PowerAutomateDesktop", "Power Automate", "Desktop automation tool.", "Productivity"),
        new("WindowsFeedbackHub", "Feedback Hub", "Sends feedback and diagnostics to Microsoft.", "System"),
        new("MSTeams", "Teams (personal)", "The consumer Teams chat app.", "Communication"),
        new("MicrosoftTeams", "Teams (personal)", "The consumer Teams chat app.", "Communication"),
        new("Clipchamp", "Clipchamp", "Microsoft's online video editor.", "Media"),
        new("OutlookForWindows", "New Outlook", "The new web-based Outlook app.", "Productivity"),
        new("DevHome", "Dev Home", "Developer dashboard and environment manager.", "Developer"),
        new("GamingApp", "Xbox", "The Xbox app and Game Pass storefront.", "Game"),
        new("XboxGamingOverlay", "Xbox Game Bar", "The Game Bar overlay (Win+G). Also provides screen recording (Win+Alt+R) \u2014 keep it if you use that.", "Game"),
        new("XboxGameOverlay", "Xbox Game Bar plugin", "A Game Bar component. Keep it if you use Game Bar recording.", "Game"),
        new("XboxSpeechToTextOverlay", "Xbox captions", "Game Bar speech-to-text overlay.", "Game"),
        new("XboxIdentityProvider", "Xbox sign-in", "Sign-in for Xbox and Game Pass games (keep if you game).", "Game"),
        new("Xbox.TCUI", "Xbox UI", "Xbox UI component used by some games (keep if you game).", "Game"),
        new("549981C3F5F10", "Cortana", "The Cortana voice-assistant app.", "AI"),
        new("Copilot", "Copilot", "The Windows Copilot app.", "AI"),
        new("MixedReality.Portal", "Mixed Reality Portal", "Windows Mixed Reality portal.", "System"),
        new("Microsoft3DViewer", "3D Viewer", "3D model viewer.", "Media"),
        new("Print3D", "Print 3D", "3D printing app.", "Utility"),
        new("3DBuilder", "3D Builder", "3D model creation app.", "Utility"),
        new("MicrosoftJournal", "Journal", "Handwriting / pen note app.", "Productivity"),
        new("Wallet", "Wallet", "Legacy Microsoft Pay wallet.", "Utility"),
        new("MicrosoftStickyNotes", "Sticky Notes", "Desktop sticky notes.", "Productivity"),
        new("WindowsAlarms", "Clock", "Alarms, timers, and clock.", "Utility"),
        new("WindowsCamera", "Camera", "Windows Camera app.", "Utility"),
        new("WindowsSoundRecorder", "Sound Recorder", "Voice recorder.", "Utility"),
        new("QuickAssist", "Quick Assist", "Remote-assistance tool.", "Utility"),
        new("SpotifyMusic", "Spotify", "A preinstalled Spotify promotion.", "Partner app"),
        new("Spotify", "Spotify", "A preinstalled Spotify promotion.", "Partner app"),
        new("Disney", "Disney+", "A preinstalled Disney+ promotion.", "Partner app"),
        new("WhatsApp", "WhatsApp", "WhatsApp desktop.", "Partner app"),
        new("Facebook", "Facebook", "A preinstalled Facebook promotion.", "Partner app"),
        new("Instagram", "Instagram", "A preinstalled Instagram promotion.", "Partner app"),
        new("TikTok", "TikTok", "A preinstalled TikTok promotion.", "Partner app"),
        new("CandyCrushSaga", "Candy Crush Saga", "A preinstalled game promotion.", "Game"),
        new("CandyCrushSoda", "Candy Crush Soda", "A preinstalled game promotion.", "Game"),
        new("king.com", "King games", "A preinstalled King.com game promotion.", "Game"),
        new("PrimeVideo", "Prime Video", "A preinstalled Amazon Prime Video promotion.", "Partner app"),
        new("AmazonVideo", "Prime Video", "A preinstalled Amazon Prime Video promotion.", "Partner app"),
        new("Netflix", "Netflix", "A preinstalled Netflix promotion.", "Partner app"),
        new("LinkedIn", "LinkedIn", "Preinstalled LinkedIn app.", "Partner app"),
        new("Dolby", "Dolby Access", "Dolby Atmos companion app.", "Media"),
    ];

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._+-]+$")]
    private static partial Regex FullNamePattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9.+-]*$")]
    private static partial Regex NamePattern();

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
    private static partial Regex CamelBoundary();

    [LibraryImport("shlwapi.dll", EntryPoint = "SHLoadIndirectString", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHLoadIndirectString(string pszSource, [Out] char[] pszOutBuf, int cchOutBuf, nint ppvReserved);

    /// <summary>Enumerates installed, user-relevant packages for all users.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task<IReadOnlyList<AppInfo>> ListAsync(CancellationToken cancellationToken)
    {
        LogEnumerating(this._logger);
        const string script =
            """
            $ErrorActionPreference = 'Stop'
            $list = Get-AppxPackage -AllUsers |
                Where-Object { -not $_.IsFramework -and -not $_.IsResourcePackage } |
                ForEach-Object {
                    [pscustomobject]@{
                        Name      = $_.Name
                        Full      = $_.PackageFullName
                        Family    = $_.PackageFamilyName
                        Publisher = $_.Publisher
                        Install   = $_.InstallLocation
                        Kind      = [string]$_.SignatureKind
                    }
                }
            ConvertTo-Json -Depth 3 -InputObject @($list)
            """;

        ShellResult result = await RunEncodedAsync(script, ListTimeout, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Enumerating apps failed: {Trim(result.StdErr)}");
        }

        return BuildApps(result.StdOut);
    }

    /// <summary>Removes a package for all users and deprovisions it so it does not return for new users.</summary>
    /// <param name="packageFullName">The validated package full name.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task<AppActionResult> RemoveAsync(string packageFullName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageFullName) || !FullNamePattern().IsMatch(packageFullName))
        {
            return new AppActionResult(packageFullName, false, "Invalid package name.");
        }

        string name = packageFullName.Split('_', 2)[0];
        if (!NamePattern().IsMatch(name))
        {
            return new AppActionResult(packageFullName, false, "Invalid package name.");
        }

        if (IsProtected(name) || IsProtected(packageFullName))
        {
            LogRefusedProtected(this._logger, packageFullName);
            return new AppActionResult(packageFullName, false, "This package is protected and cannot be removed.");
        }

        LogRemoving(this._logger, packageFullName);
        string script =
            $$"""
            $ErrorActionPreference = 'Stop'
            $full = '{{packageFullName}}'
            $name = '{{name}}'
            Remove-AppxPackage -AllUsers -Package $full -ErrorAction Stop
            try {
                Get-AppxProvisionedPackage -Online |
                    Where-Object { $_.DisplayName -eq $name } |
                    ForEach-Object { Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction Stop | Out-Null }
            } catch { }
            'OK'
            """;

        ShellResult result = await RunEncodedAsync(script, RemoveTimeout, cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0
            ? new AppActionResult(packageFullName, true, null)
            : new AppActionResult(packageFullName, false, Trim(result.StdErr));
    }

    private static AppInfo[] BuildApps(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement[] elements = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().ToArray()
            : [doc.RootElement];

        int iconBudget = IconByteBudget;
        var byFamily = new Dictionary<string, AppInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (JsonElement el in elements)
        {
            if (el.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string name = GetString(el, "Name");
            string full = GetString(el, "Full");
            string family = GetString(el, "Family");
            if (string.IsNullOrEmpty(full) || string.IsNullOrEmpty(name) || IsFrameworkName(name))
            {
                continue;
            }

            string kind = GetString(el, "Kind");
            string install = GetString(el, "Install");

            ManifestInfo manifest = ReadManifest(install, full);
            bool userFacing = manifest.HasAppEntry;

            bool protectedApp = IsProtected(name) || IsProtected(full) || string.Equals(kind, "System", StringComparison.OrdinalIgnoreCase);
            bool recommended = !protectedApp && IsBloat(name) && IsBloat(family);
            // Show user-facing apps and recommended bloat; drop background/system noise.
            if (!userFacing && !recommended)
            {
                continue;
            }

            bool isSystem = protectedApp || (!userFacing && !recommended);
            if (byFamily.ContainsKey(family))
            {
                continue;
            }

            CuratedApp? curated = LookupCatalog(name);
            string display = curated?.Title
                ?? (string.IsNullOrWhiteSpace(manifest.DisplayName) ? Prettify(name) : manifest.DisplayName!);
            string description = curated?.Description ?? DefaultDescription(kind, isSystem, recommended);
            string category = curated?.Category ?? DefaultCategory(isSystem, recommended);
            string publisher = ShortPublisher(GetString(el, "Publisher"));
            string? icon = ResolveIcon(install, manifest.LogoRelative, ref iconBudget);

            byFamily[family] = new AppInfo(
                full, family, name, display, description, publisher, category,
                recommended, !protectedApp, isSystem, userFacing, icon);
        }

        return byFamily.Values
            .OrderByDescending(a => a.Recommended)
            .ThenBy(a => a.IsSystem)
            .ThenBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsBloat(string value) => MatchesAny(value, BloatFragments);

    private static bool IsProtected(string value) => MatchesAny(value, ProtectedFragments);

    private static bool IsFrameworkName(string name) => MatchesAny(name, FrameworkFragments);

    private static bool MatchesAny(string value, string[] fragments)
    {
        foreach (string fragment in fragments)
        {
            if (value.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static CuratedApp? LookupCatalog(string name)
    {
        foreach (CuratedApp entry in Catalog)
        {
            if (name.Contains(entry.Key, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    private static string DefaultDescription(string kind, bool isSystem, bool recommended)
    {
        if (recommended)
        {
            return "Preinstalled app Sovereign suggests removing.";
        }

        if (isSystem)
        {
            return "A Windows system component. Removing it can break part of the OS.";
        }

        return string.Equals(kind, "Store", StringComparison.OrdinalIgnoreCase)
            ? "An app installed from the Microsoft Store."
            : "An installed app.";
    }

    private static string DefaultCategory(bool isSystem, bool recommended) =>
        recommended ? "Suggested removal" : isSystem ? "System" : "App";

    private static ManifestInfo ReadManifest(string installLocation, string packageFullName)
    {
        if (string.IsNullOrWhiteSpace(installLocation) || !Directory.Exists(installLocation))
        {
            return ManifestInfo.Empty;
        }

        string manifestPath = Path.Combine(installLocation, "AppxManifest.xml");
        if (!File.Exists(manifestPath))
        {
            return ManifestInfo.Empty;
        }

        try
        {
            XDocument doc = XDocument.Load(manifestPath);
            XElement? visual = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "VisualElements");

            bool hasAppEntry = doc.Descendants()
                .Where(e => e.Name.LocalName == "VisualElements")
                .Any(v => !string.Equals((string?)v.Attribute("AppListEntry"), "none", StringComparison.OrdinalIgnoreCase));

            string? rawDisplay = (string?)visual?.Attribute("DisplayName");
            if (string.IsNullOrWhiteSpace(rawDisplay))
            {
                rawDisplay = doc.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "DisplayName" && e.Parent?.Name.LocalName == "Properties")?.Value;
            }

            string? logo = (string?)visual?.Attribute("Square44x44Logo")
                ?? (string?)visual?.Attribute("Square150x150Logo")
                ?? doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Logo" && e.Parent?.Name.LocalName == "Properties")?.Value;

            return new ManifestInfo(hasAppEntry, ResolveResourceString(rawDisplay, packageFullName, installLocation), logo);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            return ManifestInfo.Empty;
        }
    }

    private static string? ResolveResourceString(string? value, string packageFullName, string installLocation)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!value.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        // Build an indirect string Windows can resolve against the package's resources.pri.
        string source = $"@{{{packageFullName}? {value}}}";
        if (TryLoadIndirect(source, out string? resolved))
        {
            return resolved;
        }

        string priSource = $"@{{{Path.Combine(installLocation, "resources.pri")}? {value}}}";
        return TryLoadIndirect(priSource, out resolved) ? resolved : null;
    }

    private static bool TryLoadIndirect(string source, out string? resolved)
    {
        resolved = null;
        try
        {
            char[] buffer = new char[1024];
            int hr = SHLoadIndirectString(source, buffer, buffer.Length, nint.Zero);
            if (hr != 0)
            {
                return false;
            }

            int len = Array.IndexOf(buffer, '\0');
            string text = new string(buffer, 0, len < 0 ? buffer.Length : len).Trim();
            if (text.Length == 0 || text.Contains("ms-resource:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            resolved = text;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    private static string? ResolveIcon(string installLocation, string? logoRelative, ref int iconBudget)
    {
        if (iconBudget <= 0 || string.IsNullOrWhiteSpace(installLocation) || string.IsNullOrWhiteSpace(logoRelative) || !Directory.Exists(installLocation))
        {
            return null;
        }

        try
        {
            string normalized = logoRelative.Replace('/', '\\');
            string relativeDir = Path.GetDirectoryName(normalized) ?? string.Empty;
            string dir = Path.Combine(installLocation, relativeDir);
            if (!Directory.Exists(dir))
            {
                return null;
            }

            string baseName = Path.GetFileNameWithoutExtension(normalized);
            string ext = Path.GetExtension(normalized);
            ext = string.IsNullOrEmpty(ext) ? ".png" : ext;

            string[] candidates = Directory.GetFiles(dir, baseName + "*" + ext);
            if (candidates.Length == 0)
            {
                return null;
            }

            string best = candidates
                .OrderBy(IconPreference)
                .ThenBy(f => new FileInfo(f).Length)
                .First();

            var info = new FileInfo(best);
            if (info.Length == 0 || info.Length > MaxSingleIconBytes)
            {
                return null;
            }

            byte[] bytes = File.ReadAllBytes(best);
            iconBudget -= bytes.Length;
            return Convert.ToBase64String(bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static int IconPreference(string path)
    {
        string f = Path.GetFileName(path).ToLowerInvariant();
        if (f.Contains("targetsize-32")) { return 0; }
        if (f.Contains("targetsize-44")) { return 1; }
        if (f.Contains("targetsize-48")) { return 2; }
        if (f.Contains("scale-100")) { return 3; }
        if (f.Contains("scale-125")) { return 4; }
        if (f.Contains("scale-150")) { return 5; }
        if (f.Contains("scale-200")) { return 6; }
        return 7;
    }

    private static string GetString(JsonElement el, string property) =>
        el.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string ShortPublisher(string publisher)
    {
        foreach (string part in publisher.Split(','))
        {
            string trimmed = part.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[3..];
            }
        }

        return publisher;
    }

    private static string Prettify(string name)
    {
        int dot = name.LastIndexOf('.');
        string tail = dot >= 0 && dot < name.Length - 1 ? name[(dot + 1)..] : name;
        return CamelBoundary().Replace(tail, " ");
    }

    private static async Task<ShellResult> RunEncodedAsync(string script, TimeSpan timeout, CancellationToken cancellationToken)
    {
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-EncodedCommand");
        psi.ArgumentList.Add(encoded);

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { stdout.AppendLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { stderr.AppendLine(e.Data); } };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException("The PowerShell operation timed out.");
        }

        return new ShellResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string Trim(string value)
    {
        string trimmed = value.Trim();
        return trimmed.Length > 400 ? trimmed[..400] : trimmed;
    }

    private readonly record struct ShellResult(int ExitCode, string StdOut, string StdErr);

    private sealed record CuratedApp(string Key, string Title, string Description, string Category);

    private readonly record struct ManifestInfo(bool HasAppEntry, string? DisplayName, string? LogoRelative)
    {
        public static ManifestInfo Empty => new(false, null, null);
    }

    [LoggerMessage(EventId = 400, Level = LogLevel.Debug, Message = "Enumerating installed Appx packages for all users.")]
    private static partial void LogEnumerating(ILogger logger);

    [LoggerMessage(EventId = 401, Level = LogLevel.Information, Message = "Removing Appx package {Package} for all users.")]
    private static partial void LogRemoving(ILogger logger, string package);

    [LoggerMessage(EventId = 402, Level = LogLevel.Warning, Message = "Refused to remove protected package {Package}.")]
    private static partial void LogRefusedProtected(ILogger logger, string package);
}
