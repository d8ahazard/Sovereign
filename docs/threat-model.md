# Threat Model (draft)

This is the first-draft threat model required by [`agent_start.md`](../agent_start.md) section
7. It enumerates the threats Sovereign must model and the per-threat fields each entry must
eventually carry. Most entries are **Not yet mitigated** because enforcement does not exist in
Milestone 0; this document tracks the obligation and is updated as mechanisms and tests land.

> Sovereign does not claim protection against a fully compromised kernel or a malicious
> administrator account unless a specific mechanism and test support that claim
> (`agent_start.md` section 7).

## Per-threat fields

Each threat must document: **Asset**, **Actor**, **Entry point**, **Failure mode**,
**Prevention**, **Detection**, **Recovery**, **Test coverage**, **Residual risk**.

## Threat register

Status legend: `Planned` = obligation recorded, not yet mitigated; `Partial` = some mitigation
exists; `Covered` = mitigated with tests.

| # | Threat | Primary asset | Milestone | Status |
|---|--------|---------------|-----------|--------|
| 1 | Malicious or compromised applications | Outbound network boundary | M3 | Planned |
| 2 | Signed applications behaving unexpectedly | Outbound network boundary | M3 | Planned |
| 3 | Windows services sharing `svchost.exe` | Service attribution | M3 | Planned |
| 4 | Child processes escaping parent-based rules | Rule integrity | M3/M4 | Planned |
| 5 | Path replacement / executable swapping | Rule identity | M3 | Planned |
| 6 | Publisher certificate changes | Rule identity | M3 | Planned |
| 7 | DNS rebinding and CDN address reuse | Hostname attribution | V2 | Planned |
| 8 | Direct-IP connections | Outbound boundary | M3 | Planned |
| 9 | DNS over HTTPS / alternate resolvers | DNS visibility | V2 | Planned |
| 10 | QUIC and UDP traffic | Outbound boundary | M3 | Planned |
| 11 | IPv6 bypasses | Outbound boundary | M3 | Planned |
| 12 | VPN and virtual adapter behavior | Outbound boundary | M3 | Planned |
| 13 | WSL, Hyper-V, Docker, container networking | Outbound boundary | M3 | Planned |
| 14 | Local unprivileged attempts to alter policy | Privilege boundary | M1/M2 | Partial |
| 15 | Privileged malware | Enforcement integrity | M1+ | Planned |
| 16 | IPC spoofing or replay | IPC boundary | M1 | Partial |
| 17 | UI impersonation | User consent | M1/M4 | Planned |
| 18 | Database tampering or corruption | Audit/rule integrity | M2 | Partial |
| 19 | Update-window abuse | Update gate | M6 | Planned |
| 20 | Race conditions on startup/shutdown/upgrade/restart | Enforcement continuity | M1/M3 | Partial |
| 21 | Rule drift after Windows updates | Desired state | M5/M6 | Planned |
| 22 | Recovery-tool abuse | Emergency recovery | M3 | Planned |
| 23 | Log flooding and notification denial of service | Availability of consent | M4 | Planned |

## Elaborated examples

These illustrate the required depth; remaining entries will be expanded as their milestones
are implemented.

### Threat 16: IPC spoofing or replay

- **Asset:** the privileged operations exposed by `Sovereign.Service`.
- **Actor:** a local unprivileged process attempting to invoke privileged actions, or to
  replay a previously captured request.
- **Entry point:** the local IPC channel between UI/CLI and the service.
- **Failure mode:** an unauthorized caller installs an allow rule, disables a policy, or opens
  an update window.
- **Prevention (implemented in M1):** the pipe is ACL'd at creation via
  `NamedPipeServerStreamAcl.Create` (LocalSystem/Administrators full control; interactive user
  read/write without `CreateNewInstance`; no Everyone/Anonymous). Every operation passes an
  explicit authorization allow-list that fails closed. Protocol versions are negotiated and
  unknown versions are rejected. The client PID is never used for authorization. See
  [ADR 0002](decisions/0002-local-ipc-over-secured-named-pipes.md).
- **Mutating operations (M2):** `ApplyPolicy` and `RollbackPolicy` are the first mutating
  operations. They remain behind the same ACL'd pipe and allow-list and are audited with the caller
  identity and a correlation id. In M2 they act only on a harmless in-memory sandbox provider, so no
  real machine state changes yet.
- **Prevention (deferred):** dedicated replay protection (nonce/sequence) is still deferred: the
  current mutating operations are effectively idempotent (re-applying an already-compliant policy is
  a no-op; rollback restores the same captured state), so a replayed request does not widen state.
  A nonce/sequence guard will be added when a non-idempotent mutating operation is introduced.
  Cross-user ACL denial is to be proven on a multi-account VM (system test).
- **Detection:** audit log of connections, denied operations, and mutating policy operations with
  caller identity; rejected malformed/oversized/unknown-version frames are closed and logged.
- **Recovery:** reject and log; no state change on failed authorization.
- **Test coverage (M1+M2):** security tests assert operations outside the allow-list are denied and
  audited and that mutating policy operations are audited with the caller identity; integration
  tests assert version negotiation, that UI loss does not change service state, that committed state
  persists across restart, and a full policy plan/apply/idempotent-reapply/rollback round-trip over
  IPC. Replay and cross-user denial tests arrive with a non-idempotent mutating operation and the VM
  system tier respectively.
- **Residual risk:** a caller running with equivalent privilege to the service is out of scope
  unless a specific mechanism is added.

### Threat 14: Local unprivileged attempts to alter policy

- **Asset:** managed policy state and the apply/rollback operations.
- **Actor:** a local unprivileged process trying to apply, roll back, or otherwise change policy.
- **Entry point:** the local IPC channel.
- **Failure mode:** an unauthorized caller changes managed state or rolls back a desired policy.
- **Prevention (M2):** policy mutation is only reachable through `ApplyPolicy`/`RollbackPolicy`,
  which sit behind the ACL'd pipe and the fail-closed allow-list and are audited with caller
  identity. The engine itself is transactional and verifies state independently, so a partially
  applied change cannot be mistaken for success.
- **Detection:** audit events for requested and completed mutating operations, each with a
  correlation id.
- **Recovery:** transactional apply rolls back on failure; restore points enable user-initiated
  rollback to the captured original state.
- **Test coverage (M2):** engine unit tests (idempotent apply, partial-failure rollback,
  `Unknown`/`Unsupported` never compliant, verify-failure and rollback-failure paths); security
  test that mutating operations are audited with caller identity.
- **Residual risk:** finer-grained authorization (e.g., requiring elevation for specific mutating
  operations) is not yet implemented; the boundary today is the pipe ACL plus allow-list.

### Threat 18: Database tampering or corruption

- **Asset:** the local SQLite event store and restore points.
- **Actor:** anything able to write the database file, or a crash mid-write.
- **Failure mode:** lost/forged audit history or unusable restore points (a silent rollback gap).
- **Prevention (M2):** the store opens in WAL mode and **fails safe** — an open/migration/read error
  surfaces as an error rather than a silent empty success, so a broken store is never mistaken for
  "nothing happened". Restore points are captured before any change and persisted before the change
  is applied.
- **Detection:** initialization and write failures are logged; restore-point persistence is verified
  by integration tests (including across a simulated restart).
- **Prevention (deferred):** file ACL hardening of `%ProgramData%\Sovereign`, integrity/signing of
  audit records, and tamper-evidence are deferred to a later hardening pass.
- **Test coverage (M2):** integration tests round-trip events and restore points and prove they
  survive a fresh store instance over the same file.
- **Residual risk:** an attacker with write access to the database file can corrupt or delete local
  history until file-level hardening lands.

### Threat 11: IPv6 bypasses

- **Asset:** the default-deny outbound boundary.
- **Actor:** any application or Windows component initiating IPv6 traffic.
- **Entry point:** the IPv6 stack, including link-local and temporary addresses.
- **Failure mode:** traffic escapes over IPv6 while IPv4 is filtered.
- **Prevention:** WFP filters that cover IPv6 explicitly. Disabling IPv6 is **not** an
  acceptable substitute for filtering it (`agent_start.md` section 2.1).
- **Detection:** external packet capture proving no unauthorized IPv6 packet leaves the VM.
- **Recovery:** fail closed; a filter that cannot be applied safely must not partially widen
  access.
- **Test coverage:** system test "IPv6 attempt" plus the external packet-capture acceptance
  test (Milestone 3).
- **Residual risk:** documented per supported Windows build.

## Maintenance

Update this document whenever a threat's prevention, detection, recovery, or test coverage
changes, and when new threats are identified. An IPv6 fail-open, a restart allow-all interval,
a rule-expiry fail-open, or an update-window failing to restore locked mode are
release-blocking defects (`agent_start.md` section 13.2).
