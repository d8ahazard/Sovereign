# Sovereign Agent Start

This file is the governing instruction set for any coding agent working in this repository.

The project is **Sovereign**: a local-first Windows control plane that makes Windows 11 behave like an explicitly controlled application host rather than an advertising, telemetry, cloud, and AI delivery platform.

The product must let the user run ordinary Windows programs, approve updates manually, inspect every attempted outbound connection, and prevent unapproved traffic from leaving the machine.

This document is binding. Repository-local rules may add stricter requirements, but may not weaken these requirements without an explicit architectural decision recorded in `docs/decisions/`.

---

## 1. Mission

Build a reliable Windows application that:

1. Blocks outbound traffic by default.
2. Allows only traffic explicitly approved by the user or an active user-selected profile.
3. Notifies the user when an unapproved connection is attempted.
4. Identifies the responsible executable, Windows service, publisher, destination, and reason for the decision whenever technically possible.
5. Removes or disables Windows cloud, telemetry, advertising, AI, synchronization, and cross-device features selected by policy.
6. Prevents Windows Update, Store updates, driver updates, and application updates from running outside a user-authorized update window.
7. Detects and reports configuration drift.
8. Keeps all policy, event, decision, and audit data local.
9. Makes every destructive or invasive system change reversible.
10. Fails closed when a security-sensitive component cannot determine the correct action.

Sovereign itself must not phone home, collect telemetry, require an account, check for updates automatically, or contact any remote service unless the user explicitly initiates the action and the UI clearly identifies the destination and purpose.

---

## 2. Non-Negotiable Product Invariants

These invariants override convenience, velocity, and implementation preference.

### 2.1 Network invariants

- Unknown outbound traffic is denied by default.
- No connection may be allowed solely because the requesting binary is signed by Microsoft or another trusted publisher.
- A temporary UI failure must not silently disable network enforcement.
- A service restart must not create an unrestricted network interval.
- Rebooting must preserve the last committed enforcement state.
- An expired temporary rule must be removed automatically.
- DNS, IPv4, IPv6, QUIC, loopback, LAN, VPN, Hyper-V, WSL, and container traffic must be considered explicitly.
- Disabling IPv6 is not an acceptable substitute for filtering IPv6.
- Rules must be deterministic and explainable.
- Every allow or block decision must be auditable.
- A rule that cannot be applied safely must fail without partially widening access.
- Emergency recovery must be possible through a local, documented, authenticated mechanism.
- Recovery tooling must not create a permanent bypass.

### 2.2 Privacy invariants

- No telemetry from Sovereign.
- No analytics SDKs.
- No crash uploads.
- No cloud-hosted configuration.
- No remote feature flags.
- No account system.
- No silent external requests for icons, reputation, geolocation, certificate metadata, or enrichment.
- Any optional remote lookup must be off by default, initiated by the user, and visibly scoped.
- Logs must avoid capturing secrets, authorization headers, browser content, request bodies, or unrelated user data.

### 2.3 System-change invariants

Every managed policy must support:

1. Detection of current state.
2. Declaration of desired state.
3. Application.
4. Verification.
5. Repair after drift.
6. Rollback.
7. Audit logging.
8. Clear reporting of unsupported or partially supported states.

Do not permanently delete system state when disabling or preserving it is sufficient.

Before changing a value, capture the original state needed for rollback.

Never claim a change succeeded merely because a command returned exit code `0`. Verify the resulting system state independently.

### 2.4 User-control invariants

- The user remains the final authority.
- No update is downloaded or installed without explicit approval.
- No reboot occurs without explicit approval, except in a separately enabled emergency policy.
- No permanent allow rule is created from a timeout, crash, or ambiguous answer.
- User-facing labels must describe consequences, not implementation details.
- Dangerous actions require a clear explanation and confirmation.
- Routine reversible actions should not bury the user in confirmation dialogs.
- Notifications must identify what was blocked before offering permission.
- The UI must distinguish `Allow once`, `Allow until process exits`, `Allow for profile`, and `Allow permanently`.

---

## 3. Initial Architecture Boundaries

The initial architecture is:

- `Sovereign.UI`
  - .NET 10
  - WinUI 3
  - Runs unelevated
  - Displays state, prompts, events, profiles, policies, and update controls
  - Never directly mutates privileged machine state

- `Sovereign.Service`
  - Windows Service
  - Runs with the minimum privileges necessary
  - Owns privileged policy application, verification, drift repair, update orchestration, and network-policy coordination
  - Exposes a versioned, authenticated local IPC API

- `Sovereign.Network`
  - Native Windows networking component
  - V1 uses supported Windows Filtering Platform and event APIs without a custom kernel driver
  - V1 behavior is **block first, notify second**
  - A kernel callout driver is explicitly out of scope until the user-mode implementation is stable, tested, threat-modeled, and approved in an ADR

- `Sovereign.Policy`
  - Declarative desired-state engine
  - Policies are idempotent, reversible, testable, and independently verifiable

- `Sovereign.Storage`
  - Local SQLite database
  - Migrations are versioned and tested
  - Sensitive local data is protected with appropriate Windows facilities
  - Database corruption must not silently relax enforcement

- `Sovereign.CLI`
  - Local administration, diagnostics, export, repair, and emergency recovery
  - Must use the same service API and authorization model as the UI
  - Must not become an undocumented bypass around policy enforcement

Do not collapse privileged and unprivileged responsibilities into one process for convenience.

Do not add a kernel driver in V1.

Do not add a cloud service.

Do not add Electron, a browser-hosted UI, or a local web server unless an ADR demonstrates a compelling security and operational benefit.

---

## 4. Required Repository Structure

Create and maintain the following structure unless the existing repository has an equivalent:

```text
/
├─ agent_start.md
├─ README.md
├─ SECURITY.md
├─ CONTRIBUTING.md
├─ Directory.Build.props
├─ Directory.Packages.props
├─ docs/
│  ├─ architecture.md
│  ├─ threat-model.md
│  ├─ test-strategy.md
│  ├─ research/
│  ├─ decisions/
│  ├─ protocols/
│  └─ runbooks/
├─ src/
│  ├─ Sovereign.UI/
│  ├─ Sovereign.Service/
│  ├─ Sovereign.Network/
│  ├─ Sovereign.Policy/
│  ├─ Sovereign.Storage/
│  ├─ Sovereign.Contracts/
│  └─ Sovereign.CLI/
├─ tests/
│  ├─ Sovereign.UnitTests/
│  ├─ Sovereign.IntegrationTests/
│  ├─ Sovereign.SystemTests/
│  ├─ Sovereign.SecurityTests/
│  └─ Sovereign.FailureInjectionTests/
├─ tools/
│  ├─ test-lab/
│  ├─ packet-capture/
│  └─ policy-fixtures/
└─ scripts/
   ├─ bootstrap.ps1
   ├─ build.ps1
   ├─ test.ps1
   ├─ verify.ps1
   └─ restore-network.ps1
```

Keep contracts independent from UI and infrastructure implementations.

Do not let UI projects reference privileged implementation projects directly.

---

## 5. Agent Operating Rules

For every task:

1. Read this file.
2. Read repository-specific rule files.
3. Inspect the relevant code, tests, documentation, and recent decisions.
4. State the intended change in a short implementation plan.
5. Identify security, networking, privilege, persistence, and rollback implications.
6. Make the smallest complete change that satisfies the requirement.
7. Add or update tests before claiming completion.
8. Run the relevant test suites.
9. Run static analysis and formatting.
10. Verify the behavior, not merely compilation.
11. Update documentation and decisions where architecture or behavior changed.
12. Report exactly what changed, what passed, what failed, and what remains uncertain.

Do not:

- Guess about Windows behavior.
- Invent undocumented APIs, policy names, registry keys, event IDs, or service behavior.
- Copy commands from random blogs without primary-source verification.
- Disable tests to obtain a green build.
- weaken a security boundary to simplify implementation.
- introduce hidden fallback behavior.
- swallow exceptions in security-sensitive paths.
- catch `Exception` without logging, classification, and an explicit recovery strategy.
- leave TODO comments for correctness-critical behavior without creating a tracked issue or documented gate.
- claim success when tests were skipped or could not run.
- modify unrelated files merely to make the diff look cleaner.
- rewrite working architecture without an ADR.
- silently change public contracts or persistent data formats.

When blocked by missing information, research first. Ask the user only when the decision genuinely requires product intent or access the agent does not have.

---

## 6. Research Rules

Windows internals, security policy, update behavior, WFP behavior, AppLocker, WinUI, service isolation, driver signing, and enterprise policy change over time. Treat memory as untrusted.

### 6.1 Source hierarchy

Use sources in this order:

1. Microsoft Learn and official Windows documentation.
2. Windows SDK and WDK headers, samples, and source comments.
3. Official .NET and Windows App SDK documentation.
4. Published protocol specifications.
5. Reproducible local experiments on supported Windows builds.
6. Maintained, reputable open-source implementations.
7. Secondary technical analysis only when primary sources are incomplete.

Never use a blog post as the only authority for a security-sensitive behavior.

### 6.2 Required research record

For any material behavior based on external research, create or update:

```text
docs/research/YYYY-MM-DD-topic.md
```

Include:

- Question being answered.
- Target Windows editions and builds.
- Primary sources with URLs and access dates.
- Relevant API, policy, registry, service, package, or event identifiers.
- Confirmed facts.
- Assumptions.
- Conflicting documentation.
- Local reproduction steps.
- Observed results.
- Remaining uncertainty.
- Impact on architecture and tests.

### 6.3 Verification requirements

A researched claim is not implementation-ready until at least one of these is true:

- Confirmed by current official documentation and matching SDK definitions.
- Reproduced in a disposable Windows VM.
- Verified by an automated integration or system test.

For undocumented behavior, require a reproducible experiment and mark the dependency as fragile.

Never hard-code Microsoft endpoints from an unverified third-party list. Endpoint groups must be versioned, sourced, testable, and overridable.

---

## 7. Threat Model Requirements

Maintain `docs/threat-model.md`.

At minimum, model:

- Malicious or compromised applications.
- Signed applications behaving unexpectedly.
- Windows services sharing `svchost.exe`.
- Child processes escaping parent-based rules.
- Path replacement and executable swapping.
- Publisher certificate changes.
- DNS rebinding and CDN address reuse.
- Direct-IP connections.
- DNS over HTTPS and alternate resolvers.
- QUIC and UDP traffic.
- IPv6 bypasses.
- VPN and virtual adapter behavior.
- WSL, Hyper-V, Docker, and container networking.
- Local unprivileged attempts to alter policy.
- Privileged malware.
- IPC spoofing or replay.
- UI impersonation.
- Database tampering or corruption.
- Update-window abuse.
- Race conditions during startup, shutdown, upgrade, or service restart.
- Rule drift after Windows updates.
- Recovery-tool abuse.
- Log flooding and notification denial of service.

For each threat, document:

- Asset.
- Actor.
- Entry point.
- Failure mode.
- Prevention.
- Detection.
- Recovery.
- Test coverage.
- Residual risk.

Do not describe the product as protection against a fully compromised kernel or administrator account unless a specific mechanism and test support that claim.

---

## 8. Policy Model Requirements

Every policy must expose a contract equivalent to:

```text
Id
Version
Title
Description
RiskLevel
Scope
SupportedWindowsEditions
SupportedBuildRange
Detect()
Plan()
Apply()
Verify()
Repair()
Rollback()
Dependencies
Conflicts
RequiresReboot
RequiresLogoff
NetworkEffects
Evidence
```

Policy execution must be transactional where possible.

If a multi-step policy fails:

1. Stop.
2. Record the failed step.
3. Preserve evidence.
4. Roll back completed steps when safe.
5. Verify rollback.
6. Report any residual drift.
7. Never mark the policy compliant.

Policy results must distinguish:

- Compliant
- Non-compliant
- Applied
- Partially applied
- Unsupported
- Verification failed
- Rollback failed
- Requires reboot
- Requires user action
- Unknown

`Unknown` must never be treated as compliant.

---

## 9. Network Rule Model Requirements

A network decision must be capable of representing:

- Executable path.
- Executable hash.
- Signed publisher and certificate identity.
- Package family identity.
- Process ID and creation time.
- Parent process identity.
- Windows service identity.
- User identity.
- App container identity.
- Protocol.
- Local and remote addresses.
- Local and remote ports.
- Interface and network profile.
- Resolved hostname when evidence exists.
- Rule source.
- Rule priority.
- Decision.
- Lifetime.
- Profile.
- Creation actor.
- Creation timestamp.
- Expiration.
- Explanation.

Never attribute a hostname to a connection without retaining evidence for the correlation.

Never display a guessed hostname as fact.

A broad publisher rule must warn that it may allow multiple binaries.

A broad interpreter rule for `python.exe`, `node.exe`, PowerShell, Java, or similar runtimes must warn that arbitrary code may inherit access.

---

## 10. Notification Requirements

A blocked-connection notification must include, when available:

- Human-friendly application name.
- Executable path.
- Publisher.
- Windows service.
- Destination hostname.
- Destination IP.
- Port and protocol.
- First-seen time.
- Number of attempts.
- Active profile.
- Why the connection was blocked.

Available actions:

- Keep blocked.
- Allow once.
- Allow until process exits.
- Allow for a selected duration.
- Allow for the active profile.
- Create permanent rule.
- Inspect details.

If the UI is unavailable, the connection remains blocked and the event is queued locally.

A timeout must default to block.

Repeated identical events must be rate-limited without losing the underlying audit record.

---

## 11. Update Window Requirements

An update window is a temporary, explicit capability grant.

Opening an update window must:

1. Record the requesting user and timestamp.
2. Display exactly which update categories will be allowed.
3. Apply only the minimum temporary service and network changes.
4. Scan before installing.
5. Present detected updates.
6. Require explicit installation approval.
7. Prevent automatic reboot.
8. Record installed package identifiers and resulting build versions.
9. Close temporary network and service access.
10. Reapply desired policies.
11. Run drift detection.
12. Run outbound-network verification.
13. Report all changes.

Update-window tests must cover:

- User cancellation.
- Network loss.
- Service crash.
- UI crash.
- Reboot during update.
- Partial update failure.
- Rollback failure.
- Unexpected package installation.
- Rule expiration.
- Failure to restore locked mode.

Failure to restore locked mode is a release-blocking defect.

---

## 12. Testing Strategy

No feature is complete without automated proof appropriate to its risk.

### 12.1 Unit tests

Required for:

- Rule evaluation.
- Rule precedence.
- Rule expiration.
- Policy planning.
- Policy-state comparison.
- Serialization.
- IPC validation.
- Migration logic.
- Event deduplication.
- Notification throttling.
- Explanation generation.
- Error classification.

### 12.2 Integration tests

Required for:

- Service and UI IPC.
- Service authorization.
- SQLite migrations and recovery.
- Windows service attribution.
- Policy application and rollback.
- Appx detection and removal.
- Scheduled-task control.
- Update service control.
- WFP rule installation and removal.
- Persistence across service restart.

### 12.3 System tests

Run in disposable Windows VMs representing supported editions and builds.

Required scenarios:

- Clean installation.
- Upgrade from previous Sovereign release.
- Reboot while locked.
- Service restart while locked.
- UI absent while locked.
- New unknown application attempts internet access.
- Allowed application connects.
- Temporary rule expires.
- Application binary changes.
- IPv4 attempt.
- IPv6 attempt.
- UDP attempt.
- QUIC attempt.
- Direct-IP attempt.
- DNS attempt.
- DoH bypass attempt.
- `svchost.exe` service traffic.
- WSL traffic.
- Hyper-V traffic.
- VPN traffic.
- Chrome unrestricted while Google helper processes remain blocked.
- Windows Update outside update window.
- Windows Update inside update window.
- Copilot or another blocked package reappears.
- Drift repair after cumulative update.
- Emergency network restoration.
- Re-lock after emergency restoration.

### 12.4 Failure-injection tests

Inject:

- Database locked.
- Database corrupted.
- Disk full.
- Service terminated.
- UI terminated.
- IPC unavailable.
- Access denied.
- Registry write failure.
- WFP transaction failure.
- Partial policy apply.
- Reboot between policy steps.
- Clock change.
- Rule-expiration scheduler failure.
- Notification flood.
- Invalid configuration.
- Unsupported Windows build.

Expected behavior must be explicit and fail closed for network enforcement.

### 12.5 External packet verification

At least one automated lab test must observe traffic outside the Windows guest, such as from the hypervisor, gateway, or packet-capture host.

A Windows-local log is not sufficient proof that no packet escaped.

The locked-mode acceptance test must:

1. Start packet capture outside the VM.
2. Boot the VM.
3. Log in.
4. Leave the system idle.
5. Open Start, Search, Settings, and managed system surfaces.
6. Trigger known blocked Windows components.
7. Run approved local applications.
8. Stop capture.
9. Assert that no unauthorized packet left the VM.
10. Archive the capture summary as test evidence.

---

## 13. Success and Failure Gates

### 13.1 Definition of done

A task is done only when:

- The implementation is complete.
- Tests exist for normal, failure, and rollback paths.
- Relevant tests pass.
- Static analysis passes.
- Formatting passes.
- Public behavior is documented.
- Security implications are documented.
- Unsupported cases are explicit.
- No known silent bypass remains.
- The final report lists commands executed and outcomes.

### 13.2 Release-blocking failures

Do not release when any of the following is true:

- Unauthorized traffic can leave in locked mode.
- The service or UI restart creates an allow-all interval.
- IPv6 bypasses enforcement.
- Rule expiration fails open.
- Update-window closure fails to restore locked mode.
- A failed policy is reported as successful.
- Rollback cannot restore a tested supported state.
- IPC permits unauthorized privileged action.
- The database can be tampered with to widen access silently.
- Logs expose sensitive request contents.
- A Windows update silently disables enforcement.
- Emergency recovery permanently weakens enforcement.
- Tests depend on internet services without explicit isolation and documentation.
- Security-sensitive tests are skipped in release builds.
- The build uses unverifiable binary dependencies.

### 13.3 Warning-level failures

May be released only with explicit documentation:

- Hostname attribution unavailable.
- Unsupported optional Windows feature.
- Non-critical UI degradation.
- Missing enrichment data.
- Delayed notification with enforcement still intact.
- Unsupported third-party VPN adapter.

Warnings must never conceal an enforcement failure.

---

## 14. Build and Dependency Rules

- Pin SDK and package versions.
- Use central package management.
- Commit lock files where supported.
- Treat warnings as errors in production projects.
- Enable nullable reference types.
- Enable analyzers.
- Prefer deterministic builds.
- Generate an SBOM for releases.
- Verify dependency licenses.
- Avoid dependencies that introduce telemetry, embedded browsers, cloud services, or opaque native binaries.
- Native binaries must have source provenance and reproducible acquisition.
- No post-install download of executable code.
- No remote scripts piped into a shell.
- No unsigned helper binaries.
- No auto-update framework in V1.
- Any future self-update feature must be user-initiated, signed, reproducible, and separately threat-modeled.

---

## 15. Coding Rules

### 15.1 General

- Prefer clear code over clever code.
- Keep security decisions centralized.
- Make state transitions explicit.
- Use structured logging.
- Include correlation IDs for policy operations and network decisions.
- Use cancellation tokens for cancellable work.
- Bound queues and memory use.
- Handle shutdown explicitly.
- Do not perform blocking I/O on UI threads.
- Do not rely on sleep-based synchronization in tests.
- Do not use global mutable state for enforcement decisions.

### 15.2 Privilege boundaries

- Validate every IPC request.
- Authenticate the caller.
- Authorize the requested operation.
- Version contracts.
- Reject unknown fields or versions where ambiguity could affect security.
- Protect against replay where relevant.
- Never trust paths, hashes, publishers, PIDs, or service names supplied by the UI without service-side verification.
- Canonicalize paths before comparison.
- Account for process ID reuse.
- Bind process decisions to process creation time or another stable identity.

### 15.3 Error handling

Security-sensitive errors must include:

- Classification.
- User-visible consequence.
- Enforcement consequence.
- Recovery action.
- Audit event.
- Test coverage.

Do not convert an enforcement error into an allow decision.

---

## 16. Documentation Requirements

Keep these documents current:

- `README.md`
  - Product purpose.
  - Supported systems.
  - Current capabilities.
  - Known limitations.
  - Build and run instructions.

- `SECURITY.md`
  - Security model.
  - Reporting process.
  - Supported versions.
  - Explicit non-goals.

- `docs/architecture.md`
  - Components.
  - Trust boundaries.
  - Data flow.
  - Startup and shutdown.
  - Enforcement lifecycle.

- `docs/threat-model.md`
  - Required threat model.

- `docs/test-strategy.md`
  - Test tiers.
  - VM matrix.
  - Packet-capture strategy.
  - Release gates.

- `docs/decisions/`
  - Architecture Decision Records.

- `docs/runbooks/`
  - Locked-mode recovery.
  - Database recovery.
  - Failed update-window recovery.
  - Policy rollback.
  - Diagnostic collection.

Documentation must describe current behavior, not aspirational behavior.

---

## 17. ADR Requirements

Create an ADR before:

- Adding a kernel driver.
- Changing the privilege model.
- Changing default-deny semantics.
- Adding any cloud dependency.
- Adding remote update checks.
- Adding a browser-based UI.
- Changing the IPC mechanism.
- Changing persistent rule identity.
- Adding a broad publisher-trust mechanism.
- Allowing automatic remediation that can widen access.
- Supporting remote administration.
- Supporting enterprise centralized management.
- Storing packet payloads.
- Adding TLS interception.
- Adding a third-party security driver.
- Weakening rollback requirements.

Use:

```text
docs/decisions/NNNN-title.md
```

Each ADR must contain:

- Context.
- Decision.
- Alternatives considered.
- Security implications.
- Privacy implications.
- Operational implications.
- Test requirements.
- Rollback strategy.
- Status.

---

## 18. Initial Milestones

### Milestone 0: Repository foundation

Deliver:

- Solution and project structure.
- Build scripts.
- Test scripts.
- Formatting and analyzer configuration.
- CI that builds and runs non-privileged tests.
- Architecture document.
- Threat-model skeleton.
- ADR template.
- Research template.
- No privileged behavior yet.

Gate:

- Clean clone builds from documented prerequisites.
- Unit tests pass.
- No network access is required to run already-restored tests.

### Milestone 1: Service, UI, and IPC skeleton

Deliver:

- Installable Windows service.
- Unelevated WinUI shell.
- Authenticated local IPC.
- Health status.
- Version negotiation.
- Local event store.
- CLI diagnostics.

Gate:

- Unauthorized local process cannot invoke privileged operations.
- UI loss does not affect service state.
- Service restart preserves committed state.

### Milestone 2: Declarative policy engine

Deliver:

- Policy contracts.
- Detection.
- Plan preview.
- Apply.
- Verify.
- Rollback.
- Audit.
- Initial harmless test policies.

Gate:

- Partial failure rolls back safely.
- `Unknown` never reports compliant.
- Repeated apply is idempotent.

### Milestone 3: Network enforcement prototype

Deliver:

- Default-deny outbound mode.
- Explicit allow rules.
- Event capture.
- Executable identity.
- Basic service attribution.
- Block-first notification queue.
- Emergency local restore path.

Gate:

- External capture proves unknown IPv4 and IPv6 traffic does not escape.
- Service restart does not create an unrestricted interval.
- Temporary rules expire closed.

### Milestone 4: Connection decision UI

Deliver:

- Live event timeline.
- Detailed connection prompt.
- Allow once.
- Allow until process exit.
- Timed allow.
- Profile allow.
- Permanent rule.
- Explanation view.

Gate:

- Timeout blocks.
- UI crash blocks.
- Decisions bind to the correct process identity.
- Repeated events are rate-limited without audit loss.

### Milestone 5: Windows de-cloud policy pack

Deliver verified policies for:

- OneDrive.
- Windows Backup.
- Settings synchronization.
- Cloud clipboard.
- Cross-device experiences.
- Phone Link.
- Advertising ID.
- Consumer experiences.
- Spotlight.
- Search highlights and web search.
- Location services.
- Maps data.
- Copilot.
- Recall.
- Click to Do.
- Selected inbox app AI features.
- Relevant scheduled tasks and services.

Gate:

- Every policy supports detect, apply, verify, repair, and rollback.
- Unsupported builds are reported, not guessed.
- Drift is detected after simulated restoration.

### Milestone 6: Update Window

Deliver:

- Explicit update profile.
- Controlled service start.
- Controlled endpoint allowance.
- Scan.
- Selection.
- Install.
- Reboot approval.
- Closure.
- Reapply policies.
- Drift report.
- Traffic verification.

Gate:

- Update traffic remains blocked outside the window.
- Cancellation restores locked mode.
- Crash recovery restores locked mode.
- Post-update drift is reported and repaired according to policy.

---

## 19. Required Task Report Format

At the end of every implementation task, report:

```text
Summary
- What changed.

Security impact
- New privileges, trust boundaries, network behavior, or risks.

Files changed
- Exact files.

Tests added or changed
- Exact tests and what they prove.

Commands run
- Exact commands.

Results
- Passed, failed, skipped, or not available.

Verification
- How resulting behavior was independently checked.

Known limitations
- Anything incomplete or uncertain.

Next gated step
- The next logical task that does not bypass a required gate.
```

Never report “all tests pass” without naming the command and test scope.

---

## 20. Bootstrap Instructions for the First Agent

On first use in a new repository:

1. Inspect the repository.
2. Do not begin implementing WFP filters immediately.
3. Create the repository structure and foundational documents.
4. Create the solution and project boundaries.
5. Add strict compiler, analyzer, formatting, and dependency settings.
6. Add unit-test infrastructure.
7. Add ADR and research templates.
8. Create the first threat-model draft.
9. Create a milestone checklist.
10. Propose the smallest Milestone 0 implementation.
11. Build and test it.
12. Stop after Milestone 0 unless explicitly asked to continue.

The first agent must create:

- `docs/decisions/0000-adr-template.md`
- `docs/research/README.md`
- `docs/threat-model.md`
- `docs/test-strategy.md`
- `docs/architecture.md`
- `docs/milestones.md`
- `SECURITY.md`
- `CONTRIBUTING.md`
- Build, test, verify, and formatting scripts

The first implementation must not:

- Modify Windows firewall state.
- Install a service.
- Request elevation.
- Change registry values.
- Remove Windows packages.
- Disable Windows services.
- Add a kernel driver.
- Contact the internet at runtime.

Milestone 0 exists to make later dangerous work controlled and testable rather than enthusiastic.

---

## 21. Final Principle

Sovereign is not a debloat script.

It is a local desired-state manager, application firewall, update gate, audit system, and user-consent boundary for Windows.

The project succeeds only when it can prove that the machine did what the user authorized, refused what the user did not authorize, preserved enough evidence to explain the decision, and recovered safely when something failed.

When correctness and convenience conflict, choose correctness.

When privacy and convenience conflict, choose explicit user consent.

When enforcement state is uncertain, block and report.
