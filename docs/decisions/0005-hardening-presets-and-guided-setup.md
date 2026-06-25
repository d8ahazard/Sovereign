# 0005: Hardening presets and the guided setup model

- **Status:** Accepted
- **Date:** 2026-06-24
- **Deciders:** Sovereign maintainer

## Context

Sovereign manages many independent toggles (cloud/telemetry/AI policies, debloat targets, Windows 11
UX restorations, firewall posture). Users want a fast, friendly way to pick a sane bundle without
reading every entry, but the product's rules forbid dark patterns and forbid any change that is not
explicit, reversible, and verified ([`agent_start.md`](../../agent_start.md) sections 2.2-2.4).

The owner asked for "3x aggression settings" for the cleanup steps and for the whole product:

- **Lite** — only the most intrusive offenders.
- **Normal** — recommended, balanced (the default).
- **Pro** — strip it down; tooltip/motto: *"I just want a fucking windows computer."*

We also already have a separate concept called **Profiles** in the UI spec
([`ui-design.md`](../ui-design.md)) for *network* rule sets (Locked / Normal / Development / Gaming /
Update Window / Offline). These are a different axis and must not be conflated.

## Decision

Introduce **Hardening levels** as named *presets* that select which declarative policies are applied
and to what desired value. They are a selection layer on top of the Milestone 2 policy engine
(ADR 0004) — **not** a new enforcement mechanism.

### Two orthogonal axes (named distinctly)

| Axis | Name | Values | Meaning |
|------|------|--------|---------|
| How much to clean up / harden | **Hardening level** | Lite, Normal, Pro | Which cleanup/privacy/UX policies are selected by default. |
| Which outbound traffic is allowed | **Network profile** | Locked, Normal, Development, Gaming, Update Window, Offline | Which explicit allow rule sets are active over default-deny. |

Both default to **Normal**. They are independent: a user can run network profile `Development` with
hardening level `Pro`. Docs and UI must always use these distinct names.

### Presets map to policy selections, not to hidden actions

- A hardening level is a **manifest**: for each managed policy/catalog entry it records whether that
  entry is *selected* at that level (and, where a policy has variants, the desired value).
- Applying a level never bypasses the engine. The level produces a **selection set**; the engine
  still runs detect -> plan -> apply -> verify -> rollback per policy (ADR 0004), capturing
  restore state first.
- The user's explicit choices are authoritative and persisted. A preset only sets the *initial*
  checkbox state; every item remains individually toggleable before apply. Changing items does not
  silently switch the level label — the UI shows "Normal (customized)".

### Level definitions live with the catalog, reviewed per item

Each entry in [`debloat-catalog.md`](../debloat-catalog.md) and
[`windows11-ux-restorations.md`](../windows11-ux-restorations.md) carries a per-level mapping
(included at Lite / Normal / Pro or not). Guidelines:

- **Lite:** only `Safe` items with the highest nuisance value (ads, suggestions, consumer
  promotions, web results in search, lock-screen tips). Nothing that removes an app a typical user
  might use; nothing `Care`/`System`.
- **Normal:** Lite plus the broadly-agreed cloud/telemetry/AI reductions and safe debloat that most
  privacy-minded users want, all reversible, no functional surprises beyond clearly-labeled ones.
- **Pro:** Normal plus aggressive removal/disable of cloud, AI, cross-device, and most inbox bloat —
  "just a Windows computer." Still never touches `System`/load-bearing items without an explicit,
  clearly-worded confirm, and still fully reversible.

`Unknown`/`Unsupported` items are shown as such and are never auto-selected (engine rule from
ADR 0004 carries through to the UI).

## No-dark-patterns invariants (binding for the wizard and every preset surface)

1. **Opt-in, neutral framing.** Each item asks plainly ("Disable this?" / "Remove this?"). Keep and
   change get equal visual weight; no pre-ticked scare-boxes, no "recommended" coloring that shames
   the safe choice.
2. **Nothing applies without an explicit Apply.** Selecting a level or tile only stages a selection;
   a single review screen shows exactly what will change before anything happens.
3. **One restore point per run.** A wizard/preset apply creates one labeled restore point covering
   the whole batch (capture-before-change), with per-item revert available afterward.
4. **Plain language, real consequences.** Show what you lose, not registry keys; risky items show a
   clear, specific confirm.
5. **No nagging, no re-prompting.** Declined items stay declined; the wizard is re-runnable on
   demand but never pesters.
6. **Report before destroy.** For existing user/system state we did not create (notably firewall
   rules), the default is to *report and recommend*, prefer *disable* over *delete*, and require an
   explicit confirm to change anything.

## Firewall review is report-first and non-destructive

Reviewing pre-existing Windows Defender Firewall rules (enumerated read-only via `Get-NetFirewallRule`
and its filter cmdlets; see the firewall/UX research record) must:

- never auto-delete a rule; prefer **disable** (reversible) over delete, and capture the full rule
  definition before any change so it can be recreated;
- classify rules (user-created vs Group Policy vs Windows built-in; allow vs block; inbound vs
  outbound; scope) using `PolicyStoreSource` and the associated filters, and **flag** likely-risky
  or stale rules without acting on them;
- maintain a conservative "probably needed" safe-list so we do not recommend breaking things the
  user relies on; when in doubt, recommend nothing and explain.

## Alternatives considered

- **One-click "harden everything" with an undo.** Rejected: violates the explicit-consent and
  no-dark-pattern rules; batch-apply without per-item visibility is exactly what we refuse to do.
- **Reuse the word "Profile" for both axes.** Rejected: guaranteed user and code confusion between
  network rule sets and cleanup aggression.
- **Bake levels into code only.** Rejected: levels are content that drifts with Windows builds;
  they live with the catalog entries and are VM-verified like everything else.

## Security implications

- Presets only ever produce selections fed to the existing authenticated, authorized, audited apply
  path. No preset is a privileged shortcut. Firewall review is read-only by default and any mutation
  is an explicit, audited, reversible operation.

## Privacy implications

- All selections, restore points, and audit stay local (no cloud preset sync).

## Operational implications

- The policy engine gains the notion of a *selection set* (a named bundle of policy ids + desired
  values). The wizard and the main UI both produce selection sets; nothing else changes in the
  engine.

## Test requirements

- A preset produces exactly the documented selection set per level; customizing an item is preserved
  and relabels the level "customized".
- Applying a preset goes through the engine (idempotent, transactional, reversible) and creates one
  restore point.
- Firewall review never deletes a rule without explicit confirm; disable is reversible; rule
  definitions are captured before change.
- Pro tooltip text renders verbatim.

## Rollback strategy

- Hardening levels are content + a thin selection layer; they can be revised without touching the
  engine. Every applied selection is reversible through the existing restore-point mechanism.
