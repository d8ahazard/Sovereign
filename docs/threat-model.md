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
| 14 | Local unprivileged attempts to alter policy | Privilege boundary | M1 | Planned |
| 15 | Privileged malware | Enforcement integrity | M1+ | Planned |
| 16 | IPC spoofing or replay | IPC boundary | M1 | Planned |
| 17 | UI impersonation | User consent | M1/M4 | Planned |
| 18 | Database tampering or corruption | Audit/rule integrity | M2 | Planned |
| 19 | Update-window abuse | Update gate | M6 | Planned |
| 20 | Race conditions on startup/shutdown/upgrade/restart | Enforcement continuity | M1/M3 | Planned |
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
- **Prevention:** authenticated channel, caller authorization, versioned contracts, rejection
  of unknown fields/versions where ambiguity could affect security, and replay protection.
- **Detection:** audit log of every privileged request with caller identity; rejected-request
  events.
- **Recovery:** reject and log; no state change on failed authorization.
- **Test coverage:** security tests asserting unauthorized callers cannot invoke privileged
  operations and that replayed requests are rejected (Milestone 1).
- **Residual risk:** a caller running with equivalent privilege to the service is out of scope
  unless a specific mechanism is added.

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
