# Windows 11 UX Restorations (candidate catalog)

Windows 11 changed or removed several behaviors that many users want back. Sovereign surfaces these
as **reversible, mostly per-user policies** with the same detect / desired / enforcement triad as
everything else, selectable by hardening level (Lite / Normal / Pro;
[ADR 0005](decisions/0005-hardening-presets-and-guided-setup.md)) and shown in the
[Setup Wizard](setup-wizard-design.md) "Windows 11 fixes" step.

> Status: candidate catalog for Milestone 5 (policies) surfaced by Milestone 7 (wizard). Mechanisms
> below are **candidates to verify in a VM per Windows build** (see the dated firewall/UX research
> record) and to resolve live; do not hard-code without verification. Most are `HKCU` changes that
> revert by restoring the captured value or deleting the added key, and many only need an Explorer
> restart (no reboot).

## How to read this

- **Mechanism**: candidate technical means (verify per build). "Settings-equiv" means there is an
  official Settings/Group Policy toggle we should prefer when present.
- **Scope**: `User` (HKCU / per-user) or `Machine`.
- **Risk**: `Safe` / `Caution` / `Care` (same scale as the debloat catalog).
- **Restore**: how Sovereign reverts it.
- **Level**: lowest hardening level that selects it by default (Lite ⊂ Normal ⊂ Pro). "—" means
  available but not auto-selected at any level (opt-in only).

## 1. Shell & context menu

| Friendly name | Mechanism (verify) | Scope | Risk | Restore | Level |
|---------------|--------------------|-------|------|---------|-------|
| Classic right-click menu (Win10 style by default) | Empty default value at `HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32` (verified mechanism); apply needs an Explorer restart | User | Caution | Delete the CLSID key + restart Explorer | Normal |
| "Show more options" is already classic | (same as above) | User | — | — | — |

The CLSID-empty-`InprocServer32` mechanism is well-documented and reversible (delete the key,
restart Explorer). `Caution` because some third-party shell tools (e.g. ExplorerPatcher) conflict;
Sovereign does **not** depend on or install any third-party shell replacement.

## 2. File Explorer

| Friendly name | Mechanism (verify) | Scope | Risk | Restore | Level |
|---------------|--------------------|-------|------|---------|-------|
| Open Explorer to "This PC" (not Home) | `HKCU\...\Explorer\Advanced\LaunchTo` | User | Safe | Re-set original | Lite |
| Show file name extensions | `...\Advanced\HideFileExt = 0` (Settings-equiv) | User | Safe | Re-set original | Normal |
| Show hidden files | `...\Advanced\Hidden` (Settings-equiv) | User | Caution | Re-set original | — |
| Full path in title bar | `...\CabinetState\FullPath` | User | Safe | Re-set original | — |
| Restore compact view spacing | `...\Advanced\UseCompactMode` | User | Safe | Re-set original | — |
| Remove "Home"/Gallery from nav pane | namespace/policy (verify) | User | Caution | Re-add | — |

## 3. Start menu

| Friendly name | Mechanism (verify) | Scope | Risk | Restore | Level |
|---------------|--------------------|-------|------|---------|-------|
| Hide "Recommended" (recent/most-used) | Settings-equiv toggles + CloudContent policy (verify per build) | User/Machine | Safe | Re-enable | Lite |
| No web/Bing results in Start search | Search policy (`BingSearchEnabled` / `DisableSearchBoxSuggestions`, verify) | User | Safe | Re-enable | Lite |
| Remove "recommended" app promotions / suggested content | CloudContent / `SystemPaneSuggestions` (verify) | User | Safe | Re-enable | Lite |

## 4. Taskbar

| Friendly name | Mechanism (verify) | Scope | Risk | Restore | Level |
|---------------|--------------------|-------|------|---------|-------|
| Hide Widgets button | `...\Advanced\TaskbarDa` / WebExperience (verify) | User | Safe | Re-enable | Lite |
| Hide Chat/Teams button | `...\Advanced\TaskbarMn` (verify) | User | Safe | Re-enable | Lite |
| Hide Task View / Search box (or set to icon) | `...\Advanced\ShowTaskViewButton`, `...\Search\SearchboxTaskbarMode` | User | Safe | Re-set original | — |
| Left-align taskbar | `...\Advanced\TaskbarAl` | User | Safe | Re-set original | — |
| Move taskbar / ungroup / smaller / Win10 layout | **Not officially supported** (requires third-party shell mods) | — | Care | — | — (excluded) |

Taskbar relocation, ungrouping, and size are **not** officially supported in Windows 11 without
third-party shell replacements. Sovereign **flags these as unsupported** and does not implement them
via unsupported hooks (it would be fragile and could break on updates — against our rules).

## 5. Lock screen, desktop & ads

| Friendly name | Mechanism (verify) | Scope | Risk | Restore | Level |
|---------------|--------------------|-------|------|---------|-------|
| Lock-screen "fun facts/tips" (Spotlight tips) off | `...\ContentDeliveryManager` / Spotlight policy | User | Safe | Re-enable | Lite |
| "Get the most out of Windows" / setup nags off | `...\ContentDeliveryManager\SubscribedContent-*` (verify) | User | Safe | Re-enable | Lite |
| Tips & suggestions notifications off | `...\ContentDeliveryManager` + notification policy | User | Safe | Re-enable | Normal |
| Ads in File Explorer (sync provider promos) off | `...\Advanced\ShowSyncProviderNotifications = 0` | User | Safe | Re-set original | Lite |
| Personalized ads (advertising ID) off | advertising-ID policy (Settings-equiv) | User/Machine | Safe | Re-enable | Lite |

## 6. Privacy-adjacent niceties (overlap with debloat catalog)

These overlap with [`debloat-catalog.md`](debloat-catalog.md) and the privacy research record; they
are listed there as the source of truth and surfaced together in the wizard:

- Search highlights / web search off; activity history off; cloud clipboard off; settings sync off;
  suggested content in Settings off.

## Cross-cutting rules

1. Prefer the **official Settings/Group Policy** mechanism when one exists; fall back to a
   documented per-user registry value only when verified.
2. **Capture before change**, verify by re-reading state (and, where relevant, restarting Explorer
   to confirm the effect), and revert by restoring the captured value or deleting the added key.
3. Never ship a tweak that depends on an **unsupported third-party shell hook**; flag those as
   unsupported instead.
4. Re-verify after Windows updates (drift detection) — Microsoft sometimes resets these.
5. `Care` and unsupported items are never auto-selected by a hardening level.
