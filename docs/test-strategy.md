# Test Strategy

Derived from [`agent_start.md`](../agent_start.md) sections 12 and 13. No feature is complete
without automated proof appropriate to its risk.

## Test tiers

| Tier | Project | Runs where | Network/privilege | Status (M0) |
|------|---------|------------|-------------------|-------------|
| Unit | `tests/Sovereign.UnitTests` | Any dev machine / CI | None | **Active** |
| Integration | `tests/Sovereign.IntegrationTests` | Windows host | Service/IPC, local | Scaffold |
| System | `tests/Sovereign.SystemTests` | Disposable Windows VMs | Privileged, isolated | Scaffold |
| Security | `tests/Sovereign.SecurityTests` | Windows host | Privilege-boundary | Scaffold |
| Failure injection | `tests/Sovereign.FailureInjectionTests` | Windows host / VM | Fault injection | Scaffold |

### Unit (section 12.1)

Required (as the relevant code lands): rule evaluation, precedence, expiration; policy
planning and state comparison; serialization; IPC validation; migration logic; event
deduplication; notification throttling; explanation generation; error classification. Unit
tests must not require network access or elevation.

Milestone 0 unit tests prove the fail-closed contract defaults: `NetworkDecisionAction.Block`
and `PolicyResultState.Unknown` are the zero/default values, `Unknown` is never `Compliant`,
and the result states are distinct.

### Integration (section 12.2)

Service/UI IPC; service authorization; SQLite migrations and recovery; Windows service
attribution; policy apply and rollback; Appx detection/removal; scheduled-task control; update
service control; WFP rule install/removal; persistence across service restart.

### System (section 12.3)

Run in disposable Windows VMs across supported editions/builds. Scenarios include clean
install, upgrade, reboot while locked, service restart while locked, UI absent while locked,
unknown app internet attempt, allowed app connects, temporary rule expiry, binary change,
IPv4/IPv6/UDP/QUIC/direct-IP/DNS/DoH attempts, `svchost.exe` service traffic, WSL/Hyper-V/VPN
traffic, Chrome allowed while Google helpers blocked, Windows Update inside/outside the update
window, blocked package reappearing, drift repair after a cumulative update, emergency network
restoration, and re-lock afterward.

### Failure injection (section 12.4)

Database locked/corrupted, disk full, service/UI terminated, IPC unavailable, access denied,
registry write failure, WFP transaction failure, partial policy apply, reboot between policy
steps, clock change, rule-expiration scheduler failure, notification flood, invalid config,
unsupported Windows build. Expected behavior must be explicit and **fail closed** for network
enforcement.

## VM matrix

To be enumerated as supported editions/builds are verified (unsupported builds are reported,
never guessed). At minimum the matrix will cover the supported Windows 11 editions and a
representative range of cumulative-update builds. Each VM is disposable and reset between runs.

## Packet-capture strategy (section 12.5)

At least one automated lab test must observe traffic **outside** the Windows guest (hypervisor,
gateway, or capture host). A Windows-local log is not sufficient proof that no packet escaped.

Locked-mode acceptance test outline:

1. Start packet capture outside the VM.
2. Boot the VM and log in.
3. Leave the system idle.
4. Open Start, Search, Settings, and managed system surfaces.
5. Trigger known blocked Windows components.
6. Run approved local applications.
7. Stop capture.
8. Assert no unauthorized packet left the VM.
9. Archive the capture summary as test evidence.

## Release gates (section 13)

**Definition of done:** implementation complete; tests for normal, failure, and rollback
paths; relevant tests pass; static analysis and formatting pass; public behavior and security
implications documented; unsupported cases explicit; no known silent bypass; final report
lists commands and outcomes.

**Release-blocking** (non-exhaustive): unauthorized traffic can leave in locked mode; a
service/UI restart creates an allow-all interval; IPv6 bypasses enforcement; rule expiration
fails open; update-window closure fails to restore locked mode; a failed policy is reported as
successful; rollback cannot restore a tested state; IPC permits unauthorized privileged action;
the database can be tampered with to widen access silently; logs expose sensitive request
contents; a Windows update silently disables enforcement; security-sensitive tests are skipped
in release builds.

**Warning-level** (releasable only with explicit documentation): hostname attribution
unavailable; unsupported optional Windows feature; non-critical UI degradation; missing
enrichment data; delayed notification with enforcement intact; unsupported third-party VPN
adapter. Warnings must never conceal an enforcement failure.
