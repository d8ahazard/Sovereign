# Setup Wizard & Guided Hardening — Design Spec

The first-run **Setup Wizard** turns Sovereign's capabilities into a friendly, modern,
Windows 11-style guided experience: review what's already on the machine, choose how aggressive to
be, and apply a clearly-shown set of reversible changes. It is also **re-runnable any time** from
the main UI, so it doubles as the "clean this up" flow, not just a one-shot.

This spec extends [`ui-design.md`](ui-design.md) (the overall control-panel spec) and is bound by
the no-dark-patterns and preset model in
[ADR 0005](decisions/0005-hardening-presets-and-guided-setup.md), the policy engine in
[ADR 0004](decisions/0004-declarative-setting-based-policy-engine.md), and the reversibility model
in [`reversibility.md`](reversibility.md). Windows mechanisms referenced here are **candidates to
verify in a VM** before implementation (see the dated firewall/UX research record).

> Runs **unelevated**. Every change is a request to `Sovereign.Service` over authenticated local
> IPC. Nothing is applied until the user hits **Apply** on the review step, and everything applied
> is captured in one restore point.

## Visual style (Windows 11 Fluent)

- WinUI 3 with **Mica** backdrop, rounded 8px cards, light/dark + high-contrast, system accent.
- A left **progress rail** (vertical stepper) showing the wizard steps with done/current/upcoming
  states; **Back / Next** at the bottom, **Skip** where a step is optional.
- Content is **square tiles** in a responsive `GridView` (preset suggestions) and **list cards**
  with toggle switches (item-by-item). Tiles use an icon, a short title, a one-line "what this
  does," and a state pill (the badge vocabulary from `ui-design.md`).
- Calm motion only (connected-animation page transitions); no flashing console windows; all work is
  async with progress and cancel.

## The hardening level control (Lite / Normal / Pro)

A segmented control pinned at the top of every selection step, plus a prominent picker on the
welcome step. Selecting a level re-seeds the suggested checkboxes/tiles on the cleanup steps; it
never applies anything by itself.

| Level | One-liner | Tooltip | Selects |
|-------|-----------|---------|---------|
| **Lite** | "Just the worst offenders." | "Only the most intrusive stuff — ads, suggestions, web results. Nothing you'd miss." | `Safe`, high-nuisance items only |
| **Normal** *(default, recommended)* | "The balanced cleanup most people want." | "Recommended. Reversible cloud/telemetry/AI reductions and safe debloat." | Lite + broadly-agreed reductions |
| **Pro** | "Strip it down." | **"I just want a fucking windows computer."** | Normal + aggressive cloud/AI/bloat removal |

The Pro tooltip text is intentional product voice (ADR 0005); it renders verbatim. Changing any
individual item relabels the level as e.g. **"Normal (customized)"** — the level is a starting
point, never a lock.

This is distinct from **Network profiles** (Locked/Normal/Development/Gaming/...) which govern
outbound rules; see ADR 0005 for the two-axes distinction.

## Wizard flow (steps)

```text
1. Welcome           → what Sovereign is; pick a hardening level; "everything is reversible"
2. Snapshot          → quick read-only scan; show current posture + a restore point will be made
3. Firewall review   → existing firewall rules: classify, flag, recommend (report-first)
4. Cloud & MS services → square preset tiles; "Disable this?" per service
5. Debloat apps      → full inventory of preinstalled apps; junk pre-deselected; user picks
6. Windows 11 fixes  → UX restorations (classic right-click, taskbar/Start/Explorer tweaks)
7. Other policies    → anything else we manage, same level + per-item control
8. Review & apply    → exact diff of what will change; one restore point; Apply
9. Done              → summary, what changed, how to revert, link to Restore points
```

Steps 3–7 are each **skippable** and each carry the level control. Step 8 is the only place changes
happen.

### Step 1 — Welcome

Plain explanation, the level picker (default **Normal**), and a reassurance line: "You'll see
exactly what changes before anything happens, and you can undo all of it." A "Skip wizard, I'll
explore myself" link drops to the dashboard.

### Step 2 — Snapshot (read-only)

Runs detect across managed policies and a quick system inventory. Shows a compact posture summary
(telemetry, cloud, AI, updates, account type, # preinstalled apps, # firewall rules). No changes;
states use the truth triad (Current / Desired / Enforcement). Sets expectation that **Apply** will
create a single restore point.

### Step 3 — Firewall review *(report-first, non-destructive)*

Goal: surface the firewall rules the user already has, flag the sneaky/needless Windows ones, and
**avoid breaking anything they need**.

- **Enumerate read-only:** `Get-NetFirewallRule` joined to `Get-NetFirewallApplicationFilter`,
  `Get-NetFirewallPortFilter`, and `Get-NetFirewallAddressFilter` (filters are separate objects,
  joined by `InstanceID`). Key rule fields: `DisplayName`, `Enabled`, `Direction`, `Action`,
  `Profile`, and `PolicyStoreSource` (local vs Group Policy).
- **Classify** into buckets: *User-created*, *Third-party app*, *Windows built-in*, *Group Policy*
  (read-only here), with sub-tags *Allow/Block*, *Inbound/Outbound*, and scope (Any remote address,
  broad port ranges, etc.).
- **Flag** likely-risky or stale rules: enabled **inbound allow** rules to **Any** address,
  sensitive ports (e.g. RDP 3389, WinRM 5985/5986, SMB 445), disabled-but-leftover rules, and
  duplicates. Each flag explains *why* in plain language.
- **Safe-list:** a conservative "probably needed" list (core networking, mDNS/printer discovery on
  private profile, the user's installed apps) so we do not recommend breaking everyday things.
- **Actions are conservative:** default recommendation is **Disable** (reversible) not **Delete**;
  Group Policy rules are shown read-only with guidance. Before any change, the full rule definition
  is captured for one-click recreate. Hardening level only changes *how many* flagged rules are
  pre-suggested for disable (Lite = almost none; Pro = all flagged inbound-allow-Any), never the
  safety rails.

Result of this step is a set of *recommendations*; nothing is applied here.

### Step 4 — Cloud & Microsoft services *(square tiles)*

A panel of **square preset tiles**, one per cloud/connected feature, grouped by category
(Telemetry & diagnostics, Advertising, Connected/Cloud experiences, Cross-device, Search & web,
AI). Each tile:

- icon + short title (e.g. "Telemetry", "Cloud clipboard", "Search highlights", "Cross-device"),
- a one-line "what this does / what you lose",
- a state pill (Current/Desired), and a single neutral toggle — the question is literally
  *"Disable this?"*.

Tiles are pre-toggled per the chosen level (per the catalog's per-level mapping). Clicking a tile
opens a detail flyout (mechanism summary in plain language, risk label, restore method). Mechanisms
come from the privacy/Copilot research record and the policy engine; everything maps to a
declarative policy so it is detect/verify/rollback-capable.

### Step 5 — Debloat preinstalled apps *(inventory list, junk pre-deselected)*

The full [`debloat-catalog.md`](debloat-catalog.md) rendered as a friendly checklist resolved
**live** on this machine (`Get-AppxProvisionedPackage -Online`, `Get-AppxPackage`) — never
hard-coded names.

- Category filters (Games/promo, Bing/web/news, AI, Cloud/sync, Inbox apps, Support, Media
  extensions, System) and risk labels (`Safe`/`Caution`/`Care`/`System`).
- "Junk" (per level) is **pre-selected for removal**; `Care`/`System` items are visible but guarded
  and never pre-selected. Each row shows its **restore method** (Store / Reprovision / Re-enable /
  Flagged). `Flagged` (no guaranteed restore) requires an explicit confirm with plain wording.
- A header summary: "Remove N · Keep M · Needs care K." Select-all/none per category. Items not
  present on this build are hidden; `Unknown` state is shown, never auto-selected.

### Step 6 — Windows 11 fixes (UX restorations)

The [`windows11-ux-restorations.md`](windows11-ux-restorations.md) catalog as toggles: restore the
classic right-click menu, open Explorer to "This PC", show file extensions, de-clutter Start
"Recommended", remove web/Bing from search, taskbar tweaks, lock-screen/spotlight tips off, etc.
Each is a reversible per-user policy with the same truth triad and a clear revert. Officially
unsupported or third-party-dependent tweaks (e.g. shell replacement) are **not** included or are
clearly flagged as unsupported.

### Step 7 — Other policies

Anything else Sovereign manages that didn't fit the themed steps, presented with the same level
control and per-item toggles, so nothing is hidden from the user. This keeps requirement #4
(everything we do is clearly surfaced) honest.

### Step 8 — Review & apply *(the only step that changes anything)*

A single, scannable **diff**: grouped by area, each line "X: from → to" with its risk and restore
method. Counts at top. Buttons: **Apply** (primary) and **Back**. On Apply:

- one labeled **restore point** is created (capture-before-change) for the whole batch;
- the engine applies each selected policy transactionally (idempotent; rolls back on failure;
  never reports compliant on failure — ADR 0004);
- a live progress list shows per-item Applied/Verified/Failed; failures are explained and were
  rolled back.

### Step 9 — Done

Summary of what changed and what was skipped, a "Revert everything from this run" button, and links
to **Restore points** and **Events** (every action is audited with a correlation id). Offers to
reboot/sign-out only if a change requires it (never automatically).

## Surfacing the same controls outside the wizard

- The main UI's **Features**, **Apps**, and a new **Firewall** view expose the identical toggles
  with the hardening-level control, so the wizard is a guided front-end over the same engine, not a
  separate code path.
- The current hardening level (and "customized" state) is shown on the Dashboard and is itself a
  saved selection set the user can re-apply or edit.

## Accessibility & quality bar

- Full keyboard navigation and screen-reader labels on every tile/toggle; no meaning by color alone
  (pills pair color + text + icon); high-contrast safe; respects reduced-motion.
- All scans/applies are async with progress and cancel; the UI degrades safely if the service is
  unavailable (read-only, with a clear "service not reachable" state).

## Out of scope for this spec

Exact XAML, tile artwork, and the verified registry keys/package families. Those are implementation
details gated on the VM-verified research records and the per-build live resolution rules.

## Maps to milestones

- Firewall **audit/review** (read-only) can land with the network milestones (M3/M4); destructive
  firewall actions follow the same reversible-apply rules.
- Cloud/debloat/UX policies are delivered by the de-cloud policy pack (M5).
- The wizard, tiles, hardening presets, and UX-restoration surface are **Milestone 7** (see
  [`milestones.md`](milestones.md)).
