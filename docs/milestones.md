# Milestones

Roadmap and gates from [`agent_start.md`](../agent_start.md) section 18. Each milestone has an
explicit gate; do not advance past a gate by bypassing it.

## Milestone 0: Repository foundation (complete)

Deliverables:

- [x] Solution and project structure (managed components + UI/Network placeholders).
- [x] Build scripts (`scripts/bootstrap.ps1`, `build.ps1`).
- [x] Test scripts (`scripts/test.ps1`, `verify.ps1`).
- [x] Formatting and analyzer configuration (`.editorconfig`, `Directory.Build.props`,
      `Directory.Build.targets`, `Directory.Packages.props`, `global.json`).
- [x] CI that builds and runs non-privileged tests (`.github/workflows/ci.yml`).
- [x] Architecture document (`docs/architecture.md`).
- [x] Threat-model skeleton (`docs/threat-model.md`).
- [x] ADR template (`docs/decisions/0000-adr-template.md`).
- [x] Research template (`docs/research/README.md`).
- [x] First threat-model draft.
- [x] Milestone checklist (this file).
- [x] No privileged behavior.

Gate:

- [x] Clean clone builds from documented prerequisites.
- [x] Unit tests pass.
- [x] No network access required to run already-restored tests.

## Milestone 1: Service, UI, and IPC skeleton (complete)

Deliver: installable Windows service; unelevated WinUI shell; authenticated local IPC; health
status; version negotiation; local event store; CLI diagnostics. UI structure and behavior are
specified in [`ui-design.md`](ui-design.md).

Decisions: local IPC over secured named pipes ([ADR 0002](decisions/0002-local-ipc-over-secured-named-pipes.md));
unpackaged self-contained WinUI 3 for V1 ([ADR 0003](decisions/0003-winui3-unpackaged-self-contained-v1.md));
security basis in [research/2026-06-24-named-pipe-ipc-security.md](research/2026-06-24-named-pipe-ipc-security.md).

Deliverables:

- [x] `Sovereign.Service` hosts a secured named-pipe IPC endpoint (ACL'd via
      `NamedPipeServerStreamAcl.Create`) and a local SQLite event store.
- [x] Protocol-version negotiation (`Hello`) that fails closed on no common version.
- [x] Authorization allow-list (read-only operations only in M1); decisions never use the
      spoofable client PID.
- [x] `Sovereign.Ipc` shared client (used by both CLI and UI; references only Contracts).
- [x] `sov` CLI: `status`, `health`, `events`, `version`.
- [x] Unelevated, unpackaged, self-contained WinUI 3 shell (dashboard + recent activity).
- [x] Reversible `install-service.ps1` / `uninstall-service.ps1`; console-mode run for dev.

Gate:

- [x] Unauthorized local process cannot invoke privileged operations (security tests: operations
      outside the allow-list are denied and audited; no privileged operations exist yet).
- [x] UI loss does not affect service state (integration test: service survives client
      disconnect and serves new connections).
- [x] Service restart preserves committed state (integration test: SQLite events persist across
      reopen).
- [ ] Cross-user pipe-ACL denial proven on a multi-account VM (deferred to a system test; see
      [`test-strategy.md`](test-strategy.md)).

## Milestone 2: Declarative policy engine (complete)

Deliver: policy contracts; detection; plan preview; apply; verify; rollback; audit; initial
harmless test policies.

Decision: declarative setting-based policy engine with a provider seam, engine-orchestrated
transactional apply, and capture-before-change restore points
([ADR 0004](decisions/0004-declarative-setting-based-policy-engine.md)). Policies act only on a
harmless in-memory sandbox provider in M2; real registry/Appx providers arrive in M5 behind the
same seam.

Deliverables:

- [x] Policy contract (`IPolicy` + `PolicyMetadata`): id, version, title, description, risk,
      scope, reboot/logoff, declarative desired settings; results use `PolicyResultState`.
- [x] `PolicyEngine` deriving detect/plan/apply/verify/repair/rollback generically (`Sovereign.Policy`).
- [x] Plan preview (`PlanPolicy`) and detection (`DetectPolicy`) over IPC; read-only.
- [x] Transactional apply with capture-before-change and independent verification.
- [x] Rollback to the last restore point; restore points persisted in SQLite
      (`SqliteRestorePointStore`).
- [x] Audit of every operation with a per-execution correlation id; mutating IPC operations
      (`ApplyPolicy`/`RollbackPolicy`) audited with the caller identity.
- [x] Initial harmless demo policies (`DemoPolicies`) operating on the in-memory sandbox.
- [x] `sov policy list|detect|plan|apply|rollback` commands.

Gate:

- [x] Partial failure rolls back safely (unit test: a write fails mid-apply; all changes are
      restored and the result is never compliant).
- [x] `Unknown` never reports compliant (unit tests: provider read failure -> `Unknown`;
      unsupported -> `Unsupported`; neither is treated as compliant).
- [x] Repeated apply is idempotent (unit + IPC end-to-end test: the second apply is a no-op that
      reports `Compliant`).

## Milestone 3: Network enforcement prototype (not started)

Deliver: default-deny outbound mode; explicit allow rules; event capture; executable identity;
basic service attribution; block-first notification queue; emergency local restore path.

Gate: external capture proves unknown IPv4 and IPv6 traffic does not escape; service restart
does not create an unrestricted interval; temporary rules expire closed.

## Milestone 4: Connection decision UI (not started)

Deliver: live event timeline; detailed connection prompt; allow once; allow until process
exit; timed allow; profile allow; permanent rule; explanation view.

Gate: timeout blocks; UI crash blocks; decisions bind to the correct process identity;
repeated events are rate-limited without audit loss.

## Milestone 5: Windows de-cloud policy pack (not started)

Deliver verified policies for OneDrive, Windows Backup, settings sync, cloud clipboard,
cross-device experiences, Phone Link, advertising ID, consumer experiences, Spotlight, search
highlights/web search, location, maps, Copilot, Recall, Click to Do, selected inbox-app AI
features, and relevant tasks/services. Candidate scope is in
[`debloat-catalog.md`](debloat-catalog.md) and the Windows 11 UX restorations in
[`windows11-ux-restorations.md`](windows11-ux-restorations.md); each policy's capture/restore follows
[`reversibility.md`](reversibility.md); mechanisms come from the dated
[`research/`](research/) records (verified in a VM before implementation). Entries carry a per-level
mapping for the Lite/Normal/Pro presets consumed by the Milestone 7 wizard.

Gate: every policy supports detect/apply/verify/repair/rollback; unsupported builds are
reported, not guessed; drift is detected after simulated restoration.

## Milestone 6: Update Window (not started)

Deliver: explicit update profile; controlled service start; controlled endpoint allowance;
scan; selection; install; reboot approval; closure; reapply policies; drift report; traffic
verification.

Gate: update traffic remains blocked outside the window; cancellation restores locked mode;
crash recovery restores locked mode; post-update drift is reported and repaired per policy.

## Milestone 7: Guided setup, hardening presets, and UX restorations (not started)

Deliver the friendly, Windows 11-style front-end over the policy engine: a re-runnable Setup
Wizard, hardening presets, an existing-firewall review, and the Windows 11 UX-restoration policies.

Decision: hardening presets (Lite / Normal / Pro) layered over declarative policies, report-first
firewall review, and the no-dark-patterns invariants
([ADR 0005](decisions/0005-hardening-presets-and-guided-setup.md)). UX behavior is specified in
[`setup-wizard-design.md`](setup-wizard-design.md); restorations in
[`windows11-ux-restorations.md`](windows11-ux-restorations.md); firewall/UX mechanisms in the
[firewall/UX research record](research/2026-06-24-firewall-review-and-win11-ux.md).

Deliverables:

- Hardening presets as named selection sets (Lite / Normal / Pro) over the M2 policy engine; the
  Pro tooltip renders verbatim. Distinct from network profiles.
- Setup Wizard (Win11 Fluent): welcome + level → snapshot → firewall review → cloud-services tiles →
  debloat inventory → Windows 11 fixes → other policies → review-diff → apply (one restore point) →
  done. Re-runnable; every selection step is skippable; only the review step mutates.
- Firewall review: read-only enumeration + classification + flagging; recommended action is
  **disable** (reversible), never delete; rule definitions captured before any change; Group Policy
  rules read-only.
- Cloud/MS-services preset tiles and the debloat checklist (live-resolved) as the wizard surfaces of
  the M5 policies.
- Windows 11 UX-restoration policies (classic context menu, Explorer/Start/taskbar/lock-screen
  tweaks), each reversible with capture/verify/rollback; unsupported third-party-dependent tweaks
  are flagged, not applied.
- The same toggles + hardening-level control surfaced outside the wizard (Features / Apps /
  Firewall views).

Gate:

- No change is applied without an explicit Apply on the review step; a preset only seeds selections.
- Applying a preset goes through the engine (idempotent, transactional, reversible) and creates one
  restore point; per-item revert works.
- Firewall review never deletes a rule without explicit confirm; disable is reversible; rule
  definitions round-trip.
- `Unknown`/`Unsupported`/`Care`/`System` items are never auto-selected; unsupported UX tweaks are
  flagged, not applied.

> Sequencing note: the firewall **audit** (read-only review) can ship alongside the network
> milestones (M3/M4); the cloud/debloat/UX **policies** are delivered by M5; M7 assembles them into
> the guided experience and the preset model.
