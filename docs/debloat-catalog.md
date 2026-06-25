# Debloat Catalog (candidate list)

A friendly, comprehensive catalog of preinstalled Windows apps, AI/cloud components, and
optional features that Sovereign can manage. It is deliberately broader than typical debloat
scripts (Win11Debloat, WinUtil, ThisIsWin11, O&O ShutUp10) but follows Sovereign's rules:
**nothing is removed without live verification and a recorded restore path**, and every entry is
classified by risk so the UI can be friendly without being reckless.

> Status: candidate catalog for the de-cloud/debloat policy pack (Milestone 5). Package
> identities drift across Windows builds and **must be resolved live** on the target machine
> (`Get-AppxProvisionedPackage -Online`, `Get-AppxPackage`), never hard-coded. The package family
> patterns below are candidates to match against, flagged for VM verification. Mechanism and
> reversibility are documented in [`reversibility.md`](reversibility.md) and the research records
> dated 2026-06-24 under [`research/`](research/).

## How to read this

- **Default**: Sovereign's suggested action — `Remove`, `Optional` (off by default, easy to
  enable), `Disable` (turn off, do not remove), or `Keep` (recommended to leave alone).
- **Risk**: `Safe` (no functional loss for most users), `Caution` (you lose a feature you might
  want), `Care` (can affect system behavior; removal guarded), `System` (do not remove).
- **Restore**: how Sovereign brings it back — `Store` (Microsoft Store reinstall), `Reprovision`
  (`Add-AppxProvisionedPackage` from retained files), `Feature` (Windows optional feature /
  capability), `Re-enable` (reverse a policy/registry change), or `Flagged` (no guaranteed restore
  — Sovereign warns before removal).

Every action is reversible unless explicitly `Flagged`. See `reversibility.md`.

## Hardening levels (Lite / Normal / Pro)

The Setup wizard and Features view select entries by **hardening level**
([ADR 0005](decisions/0005-hardening-presets-and-guided-setup.md)). Each entry maps to a level using
its `Default` and `Risk`:

- **Lite** — only `Safe` items with high nuisance value (promoted games, News/Tips, Bing/web in
  search, ad/suggestion content). No app a typical user might use; never `Care`/`System`.
- **Normal** *(recommended, default)* — Lite plus the broadly-agreed `Remove`/`Disable` entries at
  `Safe`/`Caution` risk (telemetry, consumer/cloud experiences, most promoted apps, AI consumer
  features). All reversible.
- **Pro** — "I just want a fucking windows computer": Normal plus aggressive removal/disable of
  cloud, cross-device, and most `Optional` inbox apps. Still never auto-selects `System` or
  `Flagged` items, and still fully reversible; `Flagged` always needs an explicit confirm.

`Care`/`System` items are always opt-in only. `Unknown`/`Unsupported` items are shown but never
auto-selected. The level only seeds the initial selection; the user edits item-by-item before apply
(which relabels the level "customized").

---

## 1. Games and casual/promo apps

Friendly: "the stuff that shows up wanting your attention."

| Friendly name | Candidate package family (verify live) | Default | Risk | Restore |
|---------------|----------------------------------------|---------|------|---------|
| Solitaire Collection | `Microsoft.MicrosoftSolitaireCollection` | Remove | Safe | Store |
| Xbox Game Bar | `Microsoft.XboxGamingOverlay` | Optional | Caution | Store |
| Xbox app / Game services | `Microsoft.GamingApp`, `Microsoft.XboxGameOverlay`, `Microsoft.Xbox.TCUI`, `Microsoft.XboxIdentityProvider`, `Microsoft.XboxSpeechToTextOverlay` | Optional | Caution | Store |
| Candy Crush / promoted games | (varies; promoted installs, not always present) | Remove | Safe | Store |

Note: the Gaming profile (see `ui-design.md`) re-enables Xbox/Game Bar pieces on demand, so
gamers are not punished for a clean default.

## 2. Bing / web / news / weather

| Friendly name | Candidate package family (verify live) | Default | Risk | Restore |
|---------------|----------------------------------------|---------|------|---------|
| Bing Search (web results in Start/Search) | `Microsoft.BingSearch` | Optional | Caution | Store |
| News | `Microsoft.BingNews` | Remove | Safe | Store |
| Weather | `Microsoft.BingWeather` | Optional | Safe | Store |
| Search highlights / web search | policy (CloudContent / Search) | Disable | Safe | Re-enable |

## 3. AI features

| Friendly name | Mechanism | Default | Risk | Restore |
|---------------|-----------|---------|------|---------|
| Copilot (consumer app) | AppLocker packaged-app deny (`MICROSOFT.COPILOT`) + optional package removal | Remove/Disable | Caution | Re-enable / Store |
| Recall | feature/policy (verify per build/SKU) | Disable | Care | Re-enable |
| Click to Do | feature/policy (verify) | Disable | Caution | Re-enable |
| Paint generative fill | app setting/policy | Disable | Safe | Re-enable |
| Photos generative features | app setting/policy | Disable | Safe | Re-enable |
| Notepad Rewrite/AI | app setting/policy | Disable | Safe | Re-enable |

Copilot's recommended control is an AppLocker deny rule (requires the Application Identity
service). The legacy `TurnOffWindowsCopilot` policy only hides the taskbar button and is
deprecated. See the privacy/Copilot research record.

## 4. Cloud, sync, and cross-device

| Friendly name | Candidate package / mechanism | Default | Risk | Restore |
|---------------|-------------------------------|---------|------|---------|
| OneDrive | per-user installer + policy (KFM) | Optional | Care | Reinstall / Re-enable |
| Phone Link | `Microsoft.YourPhone` | Optional | Safe | Store |
| Cross Device Experience Host | `MicrosoftWindows.CrossDevice` (verify) | Disable | Care | Store/Re-enable |
| Settings sync | policy | Disable | Safe | Re-enable |
| Cloud clipboard | policy | Disable | Safe | Re-enable |
| Activity history | policy | Disable | Safe | Re-enable |
| Windows Backup | inbox app / policy | Optional | Safe | Store/Re-enable |

## 5. Productivity / inbox apps (taste-dependent)

| Friendly name | Candidate package family (verify live) | Default | Risk | Restore |
|---------------|----------------------------------------|---------|------|---------|
| Microsoft 365 (Office) hub | `Microsoft.MicrosoftOfficeHub` | Optional | Safe | Store |
| Outlook (new) | `Microsoft.OutlookForWindows` | Optional | Caution | Store |
| Mail and Calendar (legacy) | `microsoft.windowscommunicationsapps` | Optional | Caution | Store |
| To Do | `Microsoft.Todos` | Optional | Safe | Store |
| Power Automate | `Microsoft.PowerAutomateDesktop` | Optional | Safe | Store |
| Clipchamp | `Clipchamp.Clipchamp` | Optional | Safe | Store |
| Teams (consumer/free) | `MicrosoftTeams` / `MSTeams` (verify) | Optional | Safe | Store |
| Solitaire (see Games) | - | - | - | - |
| Sticky Notes | `Microsoft.MicrosoftStickyNotes` | Keep | Safe | Store |
| Snipping Tool | `Microsoft.ScreenSketch` | Keep | Caution | Store |
| Calculator | `Microsoft.WindowsCalculator` | Keep | Safe | Store |
| Notepad | `Microsoft.WindowsNotepad` | Keep | Safe | Store |
| Paint | `Microsoft.Paint` | Keep | Safe | Store |
| Photos | `Microsoft.Windows.Photos` | Keep | Caution | Store |
| Media Player | `Microsoft.ZuneMusic` (Media Player), `Microsoft.ZuneVideo` | Keep | Safe | Store |
| Camera | `Microsoft.WindowsCamera` | Keep | Safe | Store |
| Sound Recorder | `Microsoft.WindowsSoundRecorder` | Optional | Safe | Store |
| Alarms & Clock | `Microsoft.WindowsAlarms` | Optional | Safe | Store |

## 6. Support / help / store plumbing

| Friendly name | Candidate package family (verify live) | Default | Risk | Restore |
|---------------|----------------------------------------|---------|------|---------|
| Get Help | `Microsoft.GetHelp` | Optional | Safe | Store |
| Quick Assist | `MicrosoftCorporationII.QuickAssist` | Optional | Caution | Store |
| Tips | `Microsoft.Getstarted` | Remove | Safe | Store |
| Microsoft Store | `Microsoft.WindowsStore` | Keep | System | Flagged |
| App Installer (winget) | `Microsoft.DesktopAppInstaller` | Keep | Care | Store |
| Store Purchase App | `Microsoft.StorePurchaseApp` | Keep | Care | Store |
| Windows Web Experience Pack (widgets) | `MicrosoftWindows.Client.WebExperience` | Disable | Caution | Store/Re-enable |

## 7. Media extensions (codecs)

Friendly: "boring but load-bearing — removing these breaks Photos/Media Player."

| Friendly name | Candidate package family (verify live) | Default | Risk |
|---------------|----------------------------------------|---------|------|
| HEIF / HEVC / AV1 / VP9 / WebP / Raw image / Web Media / AVC Encoder extensions | `Microsoft.*VideoExtension`, `Microsoft.*ImageExtension`, `Microsoft.WebMediaExtensions`, `Microsoft.AV1VideoExtension`, etc. | Keep | Care |

Default is `Keep`. Sovereign exposes them but warns clearly; they are dependencies for built-in
media apps.

## 8. Do-not-remove / system

These are never default-removed; Sovereign may still expose status.

- Windows Security (`Microsoft.SecHealthUI` / Defender UI) — `System`.
- Microsoft Store, App Installer, .NET/VC runtime framework packages,
  `MicrosoftWindows.Client.*` shell components, `Microsoft.UI.Xaml.*`,
  `Microsoft.VCLibs.*`, `Microsoft.NET.Native.*` — `System` / `Care`.
- Cortana (`Microsoft.549981C3F5F10`) — usually safe to remove where present, but verify it is
  not load-bearing for search on the target build.

## 9. Optional Windows features / capabilities (not Appx)

Managed via `Get-WindowsOptionalFeature` / `Enable-WindowsOptionalFeature` /
`Disable-WindowsOptionalFeature` and `Get-WindowsCapability` / `Add-/Remove-WindowsCapability`
(or DISM). These are reversible by re-enabling the feature/capability.

Examples (verify per build): Internet Explorer mode components, Windows Media Player legacy,
WordPad (deprecated), Math Recognizer, Handwriting, "Windows Hello Face" components, optional
telemetry components. Sovereign exposes these in the Features view with the same
detect/desired/enforcement triad as policies.

---

## Cross-cutting rules for every entry

1. Resolve identity live; never act on a hard-coded name.
2. Before removal, capture the restore path; if it is `Flagged`, require explicit confirmation and
   say plainly "this may not be restorable without reinstalling Windows components."
3. Apply at both scopes where relevant (provisioned + per-user); record both for rollback.
4. Verify the result independently (re-enumerate / launch attempt), not by exit code.
5. After Windows updates, drift detection re-checks these and reports anything Microsoft restored.
