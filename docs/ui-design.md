# UI/UX Design Spec

Sovereign's control panel must be **friendly but powerful**: approachable status at a glance,
with depth one click away. This spec guides the `Sovereign.UI` (WinUI 3, .NET 10) implementation
in Milestone 1+ and is consistent with the requirements in
[`agent_start.md`](../agent_start.md) sections 2.4 and 10 and the product brief
[`instructions.txt`](../instructions.txt).

> The UI runs **unelevated** and never mutates privileged state directly. Every action is a
> request to `Sovereign.Service` over authenticated local IPC. The UI must degrade safely: if the
> service or a prompt is unavailable, enforcement is unaffected and connections stay blocked.

## Design principles

1. **Status first, detail on demand.** Big, legible badges; drill-downs for power users.
2. **Plain language, real consequences.** Labels describe what happens, not registry keys.
   Dangerous actions explain the consequence and confirm; routine reversible actions don't nag.
3. **Always show the truth triad.** For every managed item show **Current state**, **Desired
   state**, and **Enforcement status** so a quiet Windows "fix" during an update is visible.
4. **Reversible by default.** "Undo" and "Restore point" are first-class, always reachable.
5. **Fast and calm.** No blocking I/O on the UI thread; no flashing admin PowerShell windows.
6. **Modern Fluent look.** WinUI 3 with Mica, light/dark, rounded cards, accent-colored badges,
   keyboard accessible, high-contrast safe.

## Visual language: badges and states

A small, consistent badge vocabulary used everywhere:

| Badge | Meaning |
|-------|---------|
| Green "Locked / Protected / Enforced" | Desired state is applied and verified. |
| Blue "Allowed / On" | Explicitly permitted by the user or active profile. |
| Amber "Drift / Attention" | Current state diverged from desired (e.g. Windows restored something). |
| Red "Blocked / Failed" | Blocked connection or a failed/rollback-failed operation. |
| Grey "Unsupported / Unknown" | Not applicable to this build/SKU, or state undetermined (never shown as compliant). |

Badges are clickable; clicking opens the relevant detail or the explanation view.

## Navigation

Left nav rail: **Dashboard**, **Connections**, **Firewall**, **Features**, **Apps** (debloat),
**Network profiles**, **Updates**, **Events**, **Restore points**, **Settings**. A **Setup wizard**
entry point (also auto-launched on first run) is pinned at the top; see
[`setup-wizard-design.md`](setup-wizard-design.md).

> **Two distinct axes — do not conflate (see [ADR 0005](decisions/0005-hardening-presets-and-guided-setup.md)):**
> **Network profiles** (Locked / Normal / Development / Gaming / Update Window / Offline) choose
> which outbound allow rule sets are active over default-deny. **Hardening level** (Lite / Normal /
> Pro) chooses how much cloud/AI/bloat/UX cleanup is selected by default. Both default to Normal and
> are independent. The nav item is named **Network profiles** to keep this clear.

## Screens

### Setup wizard (first run + re-runnable)

A guided, Windows 11-style flow that reviews the machine and applies a clearly-shown, reversible
set of changes: choose a **hardening level** (Lite / Normal / Pro), review existing firewall rules,
disable Microsoft cloud services via preset **square tiles**, debloat preinstalled apps from a live
inventory, restore Windows 11 UX regressions, then review an exact diff and apply (one restore
point). It is the guided front-end over the same engine and toggles as Features/Apps/Firewall, with
strict no-dark-patterns rules. Full spec: [`setup-wizard-design.md`](setup-wizard-design.md).

### Dashboard

A grid of clickable status cards, each a badge + one-line summary + "manage" affordance:

- Network: `Locked` · Windows telemetry: `Disabled` · Cloud features: `Removed` · AI features:
  `Removed` · Automatic updates: `Blocked` · Microsoft account: `Disabled`.
- Last blocked connection (e.g. `svchost.exe -> settings-win.data.microsoft.com`).
- Pending decisions: count (click -> Connections queue).
- System drift: `None` / count (click -> the drifted items).

A large **network-profile selector** (Locked / Normal / Development / Gaming / Update Window /
Offline). "Normal" is still default-deny; network profiles only activate different explicit rule
sets. Separately, a **hardening-level** chip (Lite / Normal / Pro, or "Normal (customized)") shows
the current cleanup posture and links to the Setup wizard / Features.

### Connections (live timeline)

Little Snitch / Portmaster-style. Each entry is a card:

- App friendly name, executable path, SHA-256, publisher, parent process, Windows service (for
  `svchost.exe`, the hosted service such as `UsoSvc`, `DoSvc`, `BITS` — never just "svchost").
- Destination hostname + IP, port/protocol, first-seen + attempt count, owner hint
  (Microsoft/Google/etc. as evidence-based, not guessed), and why the rule matched or failed.
- Decision buttons: `Keep blocked` · `Allow once` · `Until app closes` · `Allow this host` ·
  `Allow app anywhere` · `Allow for profile` · `Create permanent rule` · `Inspect`.

Timeout defaults to block. Repeated identical events are rate-limited without losing the audit
record. A guessed hostname is never shown as fact.

### Firewall (existing-rule review)

A read-first review of the machine's existing Windows Defender Firewall rules (enumerated via
`Get-NetFirewallRule` + filter cmdlets). Rules are grouped (User-created / Third-party app / Windows
built-in / Group Policy) and tagged (Allow/Block, Inbound/Outbound, scope). Likely-risky or stale
rules are **flagged with a plain-language reason** (e.g. enabled inbound allow to Any address on a
sensitive port). The default recommended action is **Disable** (reversible), never delete; Group
Policy rules are read-only with guidance; every change captures the full rule definition first. This
is the in-app surface of the wizard's firewall step (see
[`setup-wizard-design.md`](setup-wizard-design.md)).

### Features (human-readable toggles)

Grouped clickable cards (Cloud storage, Connected experiences, Advertising, AI, Location, ...)
mirroring [`debloat-catalog.md`](debloat-catalog.md) and
[`windows11-ux-restorations.md`](windows11-ux-restorations.md) categories. Each toggle shows the
truth triad and a "what this does / what you lose" tooltip. Risky items show a clear confirm. A
**hardening-level** control (Lite / Normal / Pro) re-seeds the suggested toggles; individual changes
relabel it "(customized)". Nothing applies until confirmed, and a batch apply makes one restore
point.

### Apps (debloat)

The catalog as a friendly checklist with category filters and risk labels (Safe / Caution /
Care / System). Bulk actions create a single restore point. `System` items are visible but
guarded. Each app shows its restore method (Store / Reprovision / Re-enable / Flagged).

### Network profiles

Create/edit **network profiles** as named explicit rule sets layered over default-deny. Gaming
re-enables Xbox/Game Bar; Development adds scoped dev access (Git, package registries, etc.) with
interpreter rules (a global `python.exe`/`node.exe` allow shows a clear warning that arbitrary
code inherits access). (Distinct from the **hardening level** cleanup axis — see ADR 0005.)

### Updates (gated)

Shows "Updates are currently blocked," last scan, installed build, and `Open Update Window`.
The update-window flow (scan -> select -> install -> reboot approval -> close -> reapply ->
drift report -> traffic verification) is a guided wizard with no automatic reboot.

### Events

Searchable, append-only local history with filters and export (JSON/YAML). Every allow/block,
policy apply, drift, and update action is here with correlation IDs.

### Restore points

List of restore points (timestamp, actor, reason, item count) with "Revert this change" and
"Revert batch." Surfaces the reversibility model in [`reversibility.md`](reversibility.md).

## Notifications

Toast for blocked connections identifies *what was blocked* before offering permission, with the
same actions as the Connections card. If the UI is closed, the event is queued locally and the
connection stays blocked. (Windows App SDK notifications do not work from elevated processes,
reinforcing the unelevated-UI design.)

## Accessibility and quality bar

- Full keyboard navigation, screen-reader labels, high-contrast theme support.
- No action depends on color alone (badges pair color with text/icon).
- All long-running work is async with progress and cancellation.

## Out of scope for this spec

Exact XAML, control templates, and theming tokens are implementation details for Milestone 1.
This document defines structure, behavior, and the friendly-but-powerful intent.
