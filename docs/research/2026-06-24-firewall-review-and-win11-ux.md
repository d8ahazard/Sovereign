# 2026-06-24: Firewall rule review and Windows 11 UX restorations

## Question

(1) How can Sovereign **read and classify** the user's existing Windows Defender Firewall rules to
flag risky/stale/unnecessary ones without breaking things the user needs? (2) What are the
mechanisms for the most-wanted **Windows 11 UX restorations** (starting with the classic right-click
context menu), and are they reversible?

## Target Windows editions and builds

Windows 11 (Home/Pro), 23H2/24H2 and later. Exact behavior and registry/policy availability vary by
build and SKU and **must be re-verified per target build**.

## Primary sources

- Get-NetFirewallRule (NetSecurity) — https://learn.microsoft.com/en-us/powershell/module/netsecurity/get-netfirewallrule (accessed 2026-06-24)
- Get-NetFirewallPortFilter (NetSecurity) — https://learn.microsoft.com/en-us/powershell/module/netsecurity/get-netfirewallportfilter (accessed 2026-06-24)
- Get-NetFirewallAddressFilter (NetSecurity) — https://learn.microsoft.com/en-us/powershell/module/netsecurity/get-netfirewalladdressfilter (accessed 2026-06-24)
- (Context menu) reproduced across multiple independent guides; **secondary** — to be confirmed by a
  VM reproduction before implementation (see Remaining uncertainty).

## Relevant identifiers

### Firewall (read-only enumeration)
- `Get-NetFirewallRule` returns rule objects. Useful properties on the rule itself: `Name`,
  `DisplayName`, `Enabled`, `Direction`, `Action`, `Profile`, `PolicyStoreSource` (distinguishes
  Group Policy from locally-set rules).
- Conditions (ports, addresses, application, service) are **separate filter objects** joined to the
  rule one-to-one by `InstanceID`; retrieve via piping the rule into:
  - `Get-NetFirewallApplicationFilter` (program/package),
  - `Get-NetFirewallPortFilter` (`Protocol`, `LocalPort`, `RemotePort`),
  - `Get-NetFirewallAddressFilter` (`LocalAddress`, `RemoteAddress`),
  - `Get-NetFirewallServiceFilter` (service).
- `$rule.LocalPort` is always `$null` — the port lives on the port filter. Must join.

### Classic context menu (candidate mechanism)
- Per-user key: `HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32`
  with an **empty** `(Default)` value forces the legacy (Win10) context menu. Apply requires an
  Explorer restart. Revert by **deleting** the `{86ca1aa0-...}` key and restarting Explorer.
- Other UX toggles live under `HKCU\...\Explorer\Advanced` (e.g. `LaunchTo`, `HideFileExt`,
  `Hidden`, `ShowTaskViewButton`, `TaskbarAl`, `ShowSyncProviderNotifications`) and
  `...\ContentDeliveryManager` (suggestions/Spotlight) — each to be verified per build; prefer an
  official Settings/Group Policy equivalent where one exists.

## Confirmed facts

- The `NetSecurity` module exposes read-only enumeration of all firewall rules and their filter
  objects, including `PolicyStoreSource` to separate GPO-managed rules from local ones (Microsoft
  Learn). This is sufficient to **enumerate and classify** without modifying anything.
- The rule↔filter relationship is one-to-one, joined by `InstanceID`; details require the filter
  cmdlets (Microsoft Learn).

## Assumptions

- The classic-context-menu CLSID mechanism and the `...\Explorer\Advanced` value names are correct
  and stable for the target builds. These are currently supported by **secondary** sources only and
  are assumed pending VM reproduction.
- A conservative "probably needed" firewall safe-list (core networking, private-profile discovery,
  user-installed apps) is enough to avoid recommending harmful disables. To be refined empirically.

## Conflicting documentation

- None found for the firewall cmdlets. For UX tweaks, value names occasionally differ across builds
  and some are undocumented by Microsoft (community-sourced) — treat as fragile.

## Local reproduction steps

1. In a disposable Win11 VM, run `Get-NetFirewallRule | Select Name,DisplayName,Enabled,Direction,Action,Profile,PolicyStoreSource`
   and join a sample rule to its port/address/application filters; confirm classification fields.
2. Identify enabled inbound `Allow` rules to `Any` remote address and sensitive ports (3389, 445,
   5985/5986); confirm the flagging logic.
3. Disable (not delete) a test rule via `Set-NetFirewallRule -Enabled False`, confirm reversibility
   by re-enabling; capture the full rule+filter definition first.
4. Apply the classic-context-menu key, restart Explorer, confirm the legacy menu; delete the key,
   restart Explorer, confirm revert.

## Observed results

Not yet executed — this record is research groundwork; results to be filled in during VM
verification before any implementation.

## Remaining uncertainty

- Exact per-build registry value names for several UX tweaks; which have official Settings/GPO
  equivalents (preferred) vs undocumented keys (fragile).
- The precise safe-list of firewall rules that must never be recommended for disable.
- Whether some "Windows built-in" rules are required for features the user actually uses (must err
  toward not recommending changes).

## Impact on architecture and tests

- Firewall **review is read-only**; any mutation is an explicit, reversible, audited policy
  operation that **disables** (never deletes) and **captures the rule definition first**
  (ADR 0005). Needs tests proving: no delete without explicit confirm; disable is reversible;
  rule+filter capture round-trips.
- UX restorations are reversible per-user policies in the engine (ADR 0004), each with capture +
  independent verify (re-read; Explorer restart where needed) + revert. Tests assert capture/verify/
  rollback and that unsupported third-party-dependent tweaks are flagged, not applied.
