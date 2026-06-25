# Security

This document summarizes Sovereign's security model and non-goals. The binding requirements
are in [`agent_start.md`](agent_start.md); this file must describe **current** behavior, not
aspirational behavior.

## Current security posture

The policy engine is now backed by the **real Windows registry**. It makes machine-wide changes
**only when a caller explicitly applies a policy**, and only to documented Group Policy values under
`HKLM\SOFTWARE\Policies\Microsoft\...`. The service can also remove installed Appx/MSIX packages for
all users (app debloat), but **only when a caller explicitly requests a specific package**: package
names are validated against a strict character allow-list before reaching the shell, a protected-app
list refuses OS-critical packages (Store, Defender, shell hosts, runtimes, etc.), and every request
is audited with the caller identity. App removal is **not** reversible and is surfaced as such.

Nothing is applied automatically; there is no background enforcement loop. Specifically, the service
today still does **not**:

- modify Windows Firewall or WFP state,
- disable Windows services,
- add a kernel driver, or
- contact the internet at runtime.

What the service does do:

- It can be installed as a Windows service (running as LocalSystem) via the documented, reversible
  `install-service.ps1` / `uninstall-service.ps1` scripts, or run in the foreground for
  development.
- It exposes a single local named pipe, `\\.\pipe\Sovereign.Ipc`, secured with an explicit ACL at
  creation (`NamedPipeServerStreamAcl.Create`). The authorization allow-list includes the
  **mutating** operations `ApplyPolicy` and `RollbackPolicy`; these stay behind the ACL'd pipe and
  are audited with the caller's Windows identity. Authorization never trusts the spoofable client
  PID (see [ADR 0002](docs/decisions/0002-local-ipc-over-secured-named-pipes.md)).
- It runs a transactional, reversible policy engine
  ([ADR 0004](docs/decisions/0004-declarative-setting-based-policy-engine.md)) over a real registry
  provider. Apply captures the original value of every targeted registry value before changing
  anything, persists it as a restore point, verifies each write, and rolls back on any failure.
  Rolling back (or "Revert" in the UI) restores the captured original; for values that did not exist
  before, that means deleting them so Windows returns to its default behavior.
- It maintains a local SQLite event store and restore-point store under `%ProgramData%\Sovereign`.

The only network activity is the standard one-time NuGet restore during build, which is a
developer/CI action, not product runtime behavior.

## Intended security model

When implemented (see [`docs/architecture.md`](docs/architecture.md) and
[`docs/threat-model.md`](docs/threat-model.md)):

- **Default deny.** Unknown outbound traffic is denied by default. No connection is allowed
  merely because the binary is signed by Microsoft or any trusted publisher.
- **Fail closed.** A UI failure, service restart, crash, ambiguous answer, or rule-expiry
  failure must never widen access. Reboots preserve the last committed enforcement state.
- **Privilege separation.** An unelevated UI requests actions; a minimally privileged service
  validates, authenticates, authorizes, and performs them over a versioned, authenticated
  local IPC channel. The UI never mutates privileged machine state directly.
- **Local only.** All policy, event, decision, and audit data stays on the machine. No
  account, no cloud config, no remote feature flags, no telemetry, no crash uploads.
- **Reversible.** Destructive or invasive changes capture rollback state first and are
  reversible. Emergency recovery is local, documented, and authenticated, and must not create
  a permanent bypass.
- **Auditable.** Every allow/block decision and policy operation is explainable and recorded.

## Explicit non-goals

- Sovereign does **not** claim to protect against a fully compromised kernel or a malicious
  administrator account, unless a specific mechanism and test support that claim.
- Sovereign is **not** an antivirus or EDR product.
- Sovereign does **not** intercept TLS or inspect request payloads.
- Sovereign does **not** provide remote administration or centralized enterprise management
  in V1.

## Supported versions

This is pre-release software; there are no supported released versions yet. A support policy
will be published with the first release.

## Reporting a vulnerability

This is an early-stage, local-first project. Report suspected security issues privately to the
repository owner rather than opening a public issue. Include reproduction steps, affected
Windows build, and observed versus expected behavior. Do not include secrets or unrelated
personal data in reports.
