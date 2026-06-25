# 0002: Local IPC over secured named pipes

- **Status:** Accepted
- **Date:** 2026-06-24
- **Deciders:** Sovereign maintainer

## Context

Milestone 1 requires an authenticated local channel between the unprivileged UI/CLI and the
privileged `Sovereign.Service`. The product brief ([`instructions.txt`](../../instructions.txt))
specifies "a secured named pipe." [`agent_start.md`](../../agent_start.md) section 17 requires an
ADR before locking the IPC mechanism, and sections 15.2 and the Milestone 1 gate require that an
unauthorized local process cannot invoke privileged operations.

Research (see `docs/research/2026-06-24-named-pipe-ipc-security.md`) established two hard
constraints:

- On .NET 10 a server pipe's ACL must be set at creation via `NamedPipeServerStreamAcl.Create`
  with an explicit `PipeSecurity`. The `NamedPipeServerStream` constructor does not accept
  security on .NET Core/5+, and `SetAccessControl` after creation does not work.
- `GetNamedPipeClientProcessId` is spoofable; the client PID must never be the basis of a
  security decision.

## Decision

Use **named pipes** for local IPC with the following model.

- **Pipe + ACL.** A single server pipe `\\.\pipe\Sovereign.Ipc` created with
  `NamedPipeServerStreamAcl.Create` and a `PipeSecurity` that grants: FullControl to LocalSystem
  and the local Administrators group; ReadWrite (explicitly **not** `CreateNewInstance`) to the
  interactive logged-on user; and no access to Everyone/Anonymous. The OS access-control model is
  the primary gate.
- **Caller identity.** The server records the connected caller's Windows identity (via
  impersonation / `GetImpersonationUserName`) in the audit log. Identity is used for auditing and
  authorization, never the client PID.
- **Authorization.** Every operation is dispatched through an explicit allow-list. Milestone 1
  exposes only read-only operations (Ping, Health, Version, QueryEvents). Future privileged
  operations are added to the allow-list deliberately, each with its own authorization check.
- **Framing.** Length-prefixed messages: a 4-byte little-endian unsigned length followed by a
  UTF-8 JSON body, with a hard maximum message size (DoS guard). Frames exceeding the bound are
  rejected and the connection is closed.
- **Serialization.** `System.Text.Json` via a source-generated context (trim/self-contained
  safe). Deserialization is strict where ambiguity could affect security: unknown protocol
  versions are rejected, and unknown fields are not silently accepted for security-relevant
  messages.
- **Version negotiation.** The first message is a `Hello` carrying the client's supported
  protocol version range. The server replies with the agreed version or an explicit rejection.
  Incompatible or unknown versions are refused (fail closed), never best-effort guessed.

## Alternatives considered

- **gRPC over named pipes (Kestrel `UseNamedPipes`).** Viable and supports `PipeSecurity`, but
  pulls in ASP.NET Core hosting and a larger surface for a tiny local API. Rejected for V1 in
  favor of a minimal, auditable hand-rolled protocol.
- **TCP loopback.** Rejected: harder to ACL, exposed to local port scanning, and contrary to the
  brief.
- **COM / WCF.** Rejected: heavier, less transparent, weaker fit for cross-process auth on modern
  .NET.

## Security implications

- The trust boundary is the pipe ACL plus authenticated caller identity. No decision relies on
  the spoofable client PID.
- Strict framing and size bounds limit local DoS; strict version/field handling avoids ambiguous
  parsing of security-relevant messages.
- Milestone 1 has no privileged mutating operations, so the gate is met by construction and
  locked in by security tests; the model is designed to extend safely as privileged operations
  are added.

## Privacy implications

- Purely local. No network, no account, no telemetry. The audit log records caller identity and
  operation, not payloads containing secrets.

## Operational implications

- The service must run with sufficient privilege to create the ACL'd pipe (LocalSystem).
- `Sovereign.Service` targets `net10.0-windows` for the pipe-ACL and Windows-service APIs.
- A shared `Sovereign.Ipc` library hosts the client and protocol so the UI never references
  privileged projects (agent_start.md section 4).

## Test requirements

- Unit: framing round-trip; version negotiation (compatible/incompatible/unknown); authorization
  allow-list; `PipeSecurity` ACL builder produces the expected rules.
- Integration: client/server Hello + read-only operations; UI-absence does not change service
  state; service restart preserves committed state.
- Security: out-of-allow-list operations denied; malformed/oversized/unknown-version messages
  rejected. Full cross-user ACL denial is validated by a VM/system test in a later milestone.

## Rollback strategy

- The IPC layer is internal and not yet a persisted public contract; it can be revised behind the
  versioned `Hello` negotiation. Reverting the mechanism means removing `Sovereign.Ipc` and the
  service's pipe server; no persistent data format depends on it in Milestone 1.
