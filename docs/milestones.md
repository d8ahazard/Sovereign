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

## Milestone 2: Declarative policy engine (not started)

Deliver: policy contracts; detection; plan preview; apply; verify; rollback; audit; initial
harmless test policies.

Gate: partial failure rolls back safely; `Unknown` never reports compliant; repeated apply is
idempotent.

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
[`debloat-catalog.md`](debloat-catalog.md); each policy's capture/restore follows
[`reversibility.md`](reversibility.md); mechanisms come from the dated
[`research/`](research/) records (verified in a VM before implementation).

Gate: every policy supports detect/apply/verify/repair/rollback; unsupported builds are
reported, not guessed; drift is detected after simulated restoration.

## Milestone 6: Update Window (not started)

Deliver: explicit update profile; controlled service start; controlled endpoint allowance;
scan; selection; install; reboot approval; closure; reapply policies; drift report; traffic
verification.

Gate: update traffic remains blocked outside the window; cancellation restores locked mode;
crash recovery restores locked mode; post-update drift is reported and repaired per policy.
