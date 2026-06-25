using System.Collections.Generic;

namespace Sovereign.Contracts.Ipc;

/// <summary>
/// A single installed app (Appx/MSIX package) the user can review and optionally remove.
/// </summary>
/// <param name="PackageFullName">The unique package full name (used to target removal).</param>
/// <param name="PackageFamilyName">The package family name (stable across versions).</param>
/// <param name="Name">The raw package name (e.g. <c>Microsoft.BingWeather</c>).</param>
/// <param name="DisplayName">A friendly display name (Start menu name or curated title).</param>
/// <param name="Description">A short, human description of what the app is / why it's bloat.</param>
/// <param name="Publisher">A short publisher label.</param>
/// <param name="Category">A grouping label (e.g. "Game", "Microsoft 365", "Media").</param>
/// <param name="Recommended">Whether Sovereign recommends removing this as bloat.</param>
/// <param name="Removable">Whether this package is safe to remove (false for protected system apps).</param>
/// <param name="IsSystem">Whether this is a system / background package (hidden by default).</param>
/// <param name="HasStartEntry">Whether the app shows in the Start menu (i.e. user-facing).</param>
/// <param name="IconBase64">A base64-encoded PNG of the app logo, if one could be resolved.</param>
/// <param name="Kind">The entry kind: <c>appx</c> for a Store/MSIX package, <c>win32</c> for a classic installed program.</param>
/// <param name="Reversible">Whether removal can be undone (Store apps can be reinstalled; classic uninstalls cannot).</param>
public sealed record AppInfo(
    string PackageFullName,
    string PackageFamilyName,
    string Name,
    string DisplayName,
    string Description,
    string Publisher,
    string Category,
    bool Recommended,
    bool Removable,
    bool IsSystem,
    bool HasStartEntry,
    string? IconBase64,
    string Kind = "appx",
    bool Reversible = true);

/// <summary>
/// The list of installed apps the service enumerated.
/// </summary>
/// <param name="Apps">The installed packages, recommended-for-removal first.</param>
public sealed record AppListResult(IReadOnlyList<AppInfo> Apps);

/// <summary>
/// The result of an app removal.
/// </summary>
/// <param name="PackageFullName">The package that was targeted.</param>
/// <param name="Success">Whether removal succeeded.</param>
/// <param name="Detail">Human-readable detail (especially on failure).</param>
public sealed record AppActionResult(string PackageFullName, bool Success, string? Detail);

/// <summary>
/// Identifies the app a removal request targets.
/// </summary>
/// <param name="PackageFullName">The target package full name.</param>
public sealed record AppTargetRequest(string PackageFullName);
