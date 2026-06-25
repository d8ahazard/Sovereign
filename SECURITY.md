# Security

This document summarizes Sovereign's security model and non-goals. The binding requirements
are in [`agent_start.md`](agent_start.md); this file must describe **current** behavior, not
aspirational behavior.

## Current security posture (Milestone 1)

Milestone 1 delivers the privileged-service backbone and authenticated local IPC, but still
implements **no enforcement**. Specifically, nothing in this repository today:

- modifies Windows Firewall or WFP state,
- changes the registry,
- removes Windows packages or disables services,
- adds a kernel driver, or
- contacts the internet at runtime.

What the service does do in Milestone 1:

- It can be installed as a Windows service (running as LocalSystem) via the documented, reversible
  `install-service.ps1` / `uninstall-service.ps1` scripts, or run in the foreground for
  development. Installation alone uses Manual start and changes nothing else.
- It exposes a single local named pipe, `\\.\pipe\Sovereign.Ipc`, secured with an explicit ACL at
  creation (`NamedPipeServerStreamAcl.Create`). Only read-only operations are on the authorization
  allow-list; there are no privileged/mutating operations yet. Authorization never trusts the
  spoofable client PID (see [ADR 0002](docs/decisions/0002-local-ipc-over-secured-named-pipes.md)).
- It maintains a local SQLite event store under `%ProgramData%\Sovereign`.

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

Milestone 1 is pre-release; there are no supported released versions yet. A support policy
will be published with the first release.

## Reporting a vulnerability

This is an early-stage, local-first project. Report suspected security issues privately to the
repository owner rather than opening a public issue. Include reproduction steps, affected
Windows build, and observed versus expected behavior. Do not include secrets or unrelated
personal data in reports.
