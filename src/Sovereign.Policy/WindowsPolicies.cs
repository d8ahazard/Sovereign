using Sovereign.Contracts;

namespace Sovereign.Policy;

/// <summary>
/// The catalog of real, machine-wide Windows policies Sovereign manages.
/// </summary>
/// <remarks>
/// <para>
/// Every policy here writes documented machine-scope Group Policy values under
/// <c>HKLM\SOFTWARE\Policies\Microsoft\Windows\...</c>. Applying sets the value; rolling back
/// restores the captured original (usually deleting the value, which returns Windows to its default
/// behavior). All changes are therefore reversible via the engine's capture-before-change model
/// (ADR 0004) and apply only after the user explicitly chooses them.
/// </para>
/// <para>
/// Because the values live under <c>HKLM</c>, they take effect for every user on the machine, which
/// is exactly what "keep my PC fast for all users" needs. Per-user (<c>HKCU</c>) UX tweaks that must
/// be projected onto every profile are tracked separately (see docs/windows11-ux-restorations.md).
/// </para>
/// </remarks>
public static class WindowsPolicies
{
    private const string DataCollection = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection";
    private const string CloudContent = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent";
    private const string AdvertisingInfo = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo";
    private const string ExplorerPolicy = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Explorer";
    private const string WindowsCopilot = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot";
    private const string SystemPolicy = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\System";
    private const string WindowsSearch = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search";
    private const string LocationAndSensors = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors";
    private const string GameDvr = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR";
    private const string DeliveryOptimization = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
    private const string OneDrive = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\OneDrive";
    private const string WindowsAI = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsAI";
    private const string Edge = @"HKLM\SOFTWARE\Policies\Microsoft\Edge";
    private const string SqmClient = @"HKLM\SOFTWARE\Policies\Microsoft\SQMClient\Windows";

    /// <summary>
    /// Creates the built-in catalog of real machine-wide policies.
    /// </summary>
    public static IReadOnlyList<IPolicy> CreateDefault() =>
    [
        // ---- Lite: the most obvious, lowest-risk wins ----
        Declarative(
            id: "win.consumer-features-off",
            title: "Stop auto-installed apps & suggestions",
            description: "Disables Windows 'consumer features' so Windows stops silently installing promoted games and apps, and stops suggesting apps in Start and the lock screen.",
            risk: PolicyRiskLevel.Low,
            level: PolicyLevel.Lite,
            category: "Bloat",
            settings:
            [
                new DesiredSetting(RegistrySettingKey.Dword(CloudContent, "DisableWindowsConsumerFeatures"), SettingValue.Present("1"), "Stops auto-installed promoted apps/games."),
                new DesiredSetting(RegistrySettingKey.Dword(CloudContent, "DisableConsumerAccountStateContent"), SettingValue.Present("1"), "Stops account-driven promotional content."),
                new DesiredSetting(RegistrySettingKey.Dword(CloudContent, "DisableSoftLanding"), SettingValue.Present("1"), "Stops 'tips and tricks' suggestion content."),
            ]),

        Declarative(
            id: "win.advertising-id-off",
            title: "Turn off the advertising ID",
            description: "Disables the per-machine advertising ID that apps use to profile you across sessions.",
            risk: PolicyRiskLevel.Low,
            level: PolicyLevel.Lite,
            category: "Privacy",
            settings:
            [
                new DesiredSetting(RegistrySettingKey.Dword(AdvertisingInfo, "DisabledByGroupPolicy"), SettingValue.Present("1"), "Disables the advertising ID for all users."),
            ]),

        Declarative(
            id: "win.start-web-search-off",
            title: "Remove web/Bing results from search",
            description: "Stops the Start menu and search box from sending your typing to Bing and showing web results, so search stays local and fast.",
            risk: PolicyRiskLevel.Low,
            level: PolicyLevel.Lite,
            category: "Privacy",
            settings:
            [
                new DesiredSetting(RegistrySettingKey.Dword(ExplorerPolicy, "DisableSearchBoxSuggestions"), SettingValue.Present("1"), "Disables web suggestions in the search box."),
            ]),

        // ---- Normal: the recommended privacy/performance set ----
        Declarative(
            id: "win.telemetry-min",
            title: "Minimize diagnostic data & telemetry",
            description: "Sets Windows diagnostic data to the lowest level the edition allows, turns off feedback prompts, keeps the device name out of telemetry, and opts out of the Customer Experience Improvement Program. Reversible at any time.",
            risk: PolicyRiskLevel.Medium,
            level: PolicyLevel.Normal,
            category: "Privacy",
            settings:
            [
                new DesiredSetting(RegistrySettingKey.Dword(DataCollection, "AllowTelemetry"), SettingValue.Present("0"), "Requests the minimum diagnostic-data level."),
                new DesiredSetting(RegistrySettingKey.Dword(DataCollection, "DoNotShowFeedbackNotifications"), SettingValue.Present("1"), "Stops Windows feedback prompts."),
                new DesiredSetting(RegistrySettingKey.Dword(DataCollection, "AllowDeviceNameInTelemetry"), SettingValue.Present("0"), "Keeps the device name out of telemetry."),
                new DesiredSetting(RegistrySettingKey.Dword(SqmClient, "CEIPEnable"), SettingValue.Present("0"), "Disables the Customer Experience Improvement Program."),
            ]),

        Declarative(
            id: "win.copilot-off",
            title: "Turn off Copilot, Recall & AI",
            description: "Turns off Windows Copilot, the Recall snapshot/AI data analysis feature, and the Copilot/AI sidebar in Microsoft Edge \u2014 machine-wide.",
            risk: PolicyRiskLevel.Low,
            level: PolicyLevel.Normal,
            category: "AI",
            settings:
            [
                new DesiredSetting(RegistrySettingKey.Dword(WindowsCopilot, "TurnOffWindowsCopilot"), SettingValue.Present("1"), "Disables Windows Copilot for all users."),
                new DesiredSetting(RegistrySettingKey.Dword(WindowsAI, "DisableAIDataAnalysis"), SettingValue.Present("1"), "Disables Recall AI data analysis / snapshots."),
                new DesiredSetting(RegistrySettingKey.Dword(WindowsAI, "TurnOffSavingSnapshots"), SettingValue.Present("1"), "Stops Recall from saving snapshots."),
                new DesiredSetting(RegistrySettingKey.Dword(Edge, "HubsSidebarEnabled"), SettingValue.Present("0"), "Disables the Edge sidebar that hosts Copilot."),
            ]),

        Declarative(
            id: "win.cortana-off",
            title: "Turn off Cortana",
            description: "Disables Cortana machine-wide. Local and Windows search keep working.",
            risk: PolicyRiskLevel.Low,
            level: PolicyLevel.Normal,
            category: "Privacy",
            settings:
            [
                new DesiredSetting(RegistrySettingKey.Dword(WindowsSearch, "AllowCortana"), SettingValue.Present("0"), "Disables Cortana."),
            ]),

        Declarative(
            id: "win.activity-history-off",
            title: "Turn off activity history",
            description: "Stops Windows from collecting your activity timeline and uploading it to Microsoft.",
            risk: PolicyRiskLevel.Low,
            level: PolicyLevel.Normal,
            category: "Privacy",
            settings:
            [
                new DesiredSetting(RegistrySettingKey.Dword(SystemPolicy, "EnableActivityFeed"), SettingValue.Present("0"), "Disables the activity feed."),
                new DesiredSetting(RegistrySettingKey.Dword(SystemPolicy, "PublishUserActivities"), SettingValue.Present("0"), "Stops publishing user activities."),
                new DesiredSetting(RegistrySettingKey.Dword(SystemPolicy, "UploadUserActivities"), SettingValue.Present("0"), "Stops uploading user activities."),
            ]),

        Declarative(
            id: "win.delivery-optimization-off",
            title: "Stop sharing updates with the internet",
            description: "Sets Delivery Optimization to download updates without peer-to-peer sharing to other PCs on the internet, saving upload bandwidth.",
            risk: PolicyRiskLevel.Low,
            level: PolicyLevel.Normal,
            category: "Network",
            settings:
            [
                new DesiredSetting(RegistrySettingKey.Dword(DeliveryOptimization, "DODownloadMode"), SettingValue.Present("0"), "Disables peer-to-peer update sharing."),
            ]),

        // ---- Pro: "I just want a fucking Windows computer" ----
        Declarative(
            id: "win.spotlight-off",
            title: "Turn off Windows Spotlight ads",
            description: "Disables Windows Spotlight promotional content on the lock screen and in suggestions.",
            risk: PolicyRiskLevel.Low,
            level: PolicyLevel.Pro,
            category: "Bloat",
            settings:
            [
                new DesiredSetting(RegistrySettingKey.Dword(CloudContent, "DisableWindowsSpotlightFeatures"), SettingValue.Present("1"), "Disables Windows Spotlight features."),
            ]),

        Declarative(
            id: "win.gamedvr-off",
            title: "Turn off Game DVR background capture",
            description: "Turns off the Game DVR recording feature, which can quietly use CPU/GPU in the background. Note: this also disables Game Bar's manual recording (Win+Alt+R), so skip it if you use that. Reversible at any time.",
            risk: PolicyRiskLevel.Low,
            level: PolicyLevel.Pro,
            category: "Performance",
            settings:
            [
                new DesiredSetting(RegistrySettingKey.Dword(GameDvr, "AllowGameDVR"), SettingValue.Present("0"), "Disables background game recording."),
            ]),

        Declarative(
            id: "win.location-off",
            title: "Turn off location services",
            description: "Disables the system location service for all users. Apps that rely on location (weather, Find my device) will stop getting it.",
            risk: PolicyRiskLevel.Medium,
            level: PolicyLevel.Pro,
            category: "Privacy",
            settings:
            [
                new DesiredSetting(RegistrySettingKey.Dword(LocationAndSensors, "DisableLocation"), SettingValue.Present("1"), "Disables the location service machine-wide."),
            ]),

        Declarative(
            id: "win.onedrive-off",
            title: "Turn off OneDrive sync",
            description: "Stops the OneDrive file-sync engine from running and from pre-loading before sign-in. Choose this only if you do not use OneDrive; it turns off cloud file sync for everyone on the PC. (To also uninstall the OneDrive app, use the Apps tab.)",
            risk: PolicyRiskLevel.High,
            level: PolicyLevel.Pro,
            category: "Cloud",
            settings:
            [
                new DesiredSetting(RegistrySettingKey.Dword(OneDrive, "DisableFileSyncNGSC"), SettingValue.Present("1"), "Disables the OneDrive sync engine."),
                new DesiredSetting(RegistrySettingKey.Dword(OneDrive, "DisableFileSync"), SettingValue.Present("1"), "Disables legacy OneDrive file sync."),
                new DesiredSetting(RegistrySettingKey.Dword(OneDrive, "PreventNetworkTrafficPreUserSignIn"), SettingValue.Present("1"), "Stops OneDrive network traffic before sign-in."),
            ]),

        Declarative(
            id: "win.edge-tame",
            title: "Tame Microsoft Edge",
            description: "Stops Edge from running in the background, disables startup boost, and removes the Copilot/Discover sidebar and shopping/coupon nags. Edge keeps working as a browser; this just stops it pre-loading and pestering. (Microsoft does not support fully uninstalling Edge.)",
            risk: PolicyRiskLevel.Medium,
            level: PolicyLevel.Pro,
            category: "Cloud",
            settings:
            [
                new DesiredSetting(RegistrySettingKey.Dword(Edge, "BackgroundModeEnabled"), SettingValue.Present("0"), "Stops Edge running in the background."),
                new DesiredSetting(RegistrySettingKey.Dword(Edge, "StartupBoostEnabled"), SettingValue.Present("0"), "Disables Edge startup boost pre-loading."),
                new DesiredSetting(RegistrySettingKey.Dword(Edge, "HubsSidebarEnabled"), SettingValue.Present("0"), "Removes the Edge sidebar."),
                new DesiredSetting(RegistrySettingKey.Dword(Edge, "EdgeShoppingAssistantEnabled"), SettingValue.Present("0"), "Disables shopping/coupon nags."),
            ]),
    ];

    private static DeclarativeSettingPolicy Declarative(
        string id,
        string title,
        string description,
        PolicyRiskLevel risk,
        PolicyLevel level,
        string category,
        IReadOnlyList<DesiredSetting> settings) =>
        new(
            new PolicyMetadata(
                Id: id,
                Version: 1,
                Title: title,
                Description: description,
                RiskLevel: risk,
                Scope: PolicyScope.Machine,
                RequiresReboot: false,
                RequiresLogoff: false,
                Level: level,
                Category: category),
            settings);
}
