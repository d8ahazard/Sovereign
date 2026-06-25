# Sovereign Next-Phase Agent Directive

**Repository:** `d8ahazard/Sovereign`  
**Reviewed baseline:** `02bc58684ae248534cab4bbc6f82c10cd67dde65` (`Wizzard`)  
**Date:** 2026-06-24

Read `agent_start.md` before doing anything in this file. Its security, research, testing, rollback, and reporting rules remain binding. This document is a task directive and may add stricter gates, but it does not replace the project constitution.

---

## 1. Objective

Advance Sovereign from a useful cleanup utility into the product it claims to be:

1. A persistent desired-state controller for Windows.
2. A default-deny network and update gate.
3. A drift detector that notices when Windows, an update, an installer, or another administrator changes protected state.
4. A user-consent boundary that can safely restore approved settings.
5. A friendly software center built on existing package managers rather than the Microsoft Store.

The immediate product direction is:

> Sovereign should remember the state the user approved, verify it after reboot and after updates, notify the user when it changes, safely restore enforceable settings, and provide a friendly package-management UI over WinGet first, with Scoop and Chocolatey as later opt-in providers.

Do not invent a new package repository, package format, download CDN, account system, telemetry service, or remote catalog service.

Do not begin the software-center implementation until the current wizard, build gates, privileged uninstaller path, and desired-state persistence are corrected.

---

## 2. Current Code Review

The latest commit adds a reachable `WizardPage` and wires **Get started** into the main navigation. That closes the earlier missing-code-behind problem. The implementation is still an initial prototype and does not yet satisfy the repository’s own design, safety, or transactional claims.

Treat every item in this section as an explicit finding to resolve or document.

### 2.1 P0: WinUI changes are outside the normal build and CI gates

Current state:

- `Sovereign.slnx` does not include `Sovereign.UI`.
- `scripts/build.ps1` builds the UI only with `-Full`.
- `scripts/verify.ps1` does not build the UI.
- `.github/workflows/ci.yml` does not restore or build the UI.
- The latest commit is almost entirely UI code.
- No workflow result currently proves that the wizard compiles.

Required correction:

1. Add a dedicated UI build gate to CI.
2. Add `-Full` or an equivalent UI verification path to `scripts/verify.ps1`.
3. Keep privileged/system tests separate, but compilation of all production projects is mandatory.
4. Add a lightweight UI/view-model test project if direct WinUI test hosting is impractical.
5. A PR or push that changes `src/Sovereign.UI/**` must fail if the UI does not compile.

Gate:

- A clean checkout builds the managed solution and `Sovereign.UI` in Release.
- CI visibly runs both gates.
- UI compilation cannot be skipped silently.

### 2.2 P0: Wizard apply performs UI work from a non-UI thread

`SovereignClient.RunAsync` uses `ConfigureAwait(false)` before invoking the supplied delegate. In `WizardPage.ApplyAsync`, the delegate calls:

- `SetApplyStatus`
- `StepProgress`
- view-model property setters bound to WinUI controls

Those calls may execute on a worker thread and violate WinUI thread affinity.

Required correction:

- Never update controls or UI-bound collections inside the IPC delegate.
- Keep service calls off the UI thread.
- Marshal progress/results back through `DispatcherQueue`, `IProgress<T>`, an observable operation model, or a view model whose updates are explicitly dispatched.
- Add a testable operation coordinator so this logic is not embedded in event handlers.
- Use `try/finally` so `_applying`, busy state, buttons, and navigation state are restored after every exception and cancellation.

Gate:

- Simulated delayed IPC calls can report progress without cross-thread access.
- Any unexpected exception returns the UI to a usable state.
- Cancellation and navigation cannot leave the page permanently busy.

### 2.3 P0: The wizard is not a transaction and does not create one restore point

The UI claims that one restore point is created for the wizard run. The current implementation loops over `ApplyPolicyAsync`, causing each policy to create its own correlation ID and restore point.

It then continues into Appx and Win32 removals even if earlier policy applications fail.

Required correction:

Create a service-owned batch operation. Do not emulate a transaction with a UI loop.

Proposed IPC operations:

- `PlanSetupBatch`
- `ApplySetupBatch`
- `GetOperation`
- `CancelOperation`
- `ResumeOperation`
- `RollbackSetupBatch`

Proposed request model:

```text
SetupBatchRequest
- OperationId / idempotency key
- Selected hardening level
- Selected policy IDs
- Selected Appx package identities
- Selected Win32 program identities
- Requested by
- Explicit acknowledgements for irreversible actions
```

The service must:

1. Resolve every requested identity again.
2. Build an exact plan.
3. Capture all reversible policy originals into one labeled batch restore point.
4. Persist an operation record before mutation.
5. Apply reversible policies transactionally.
6. Verify the full reversible batch.
7. Stop before irreversible removals if the reversible batch fails.
8. Run separately acknowledged irreversible removals only after the reversible phase succeeds.
9. Persist per-item results and final operation state.
10. Permit the UI to reconnect and display the operation after navigation or restart.

A single batch may have:

- one batch correlation ID,
- one parent operation record,
- one batch restore point containing multiple policy/settings groups,
- child result records for each selected action.

Do not describe irreversible app removals as part of an atomic rollback transaction.

Gate:

- Policy failure before app removal prevents all irreversible removals.
- Partial policy failure restores the entire reversible batch.
- A disconnected UI can reconnect and read final results.
- Repeating the same idempotency key does not perform actions twice.

### 2.4 P0: Classic uninstaller execution is unsafe and functionally incorrect

`Win32ProgramManager` currently reads an uninstall command from HKLM and executes it from the LocalSystem service.

The parser splits an unquoted command at the first space. A command such as:

```text
C:\Program Files\Vendor\uninstall.exe /silent
```

is parsed as executable `C:\Program`, which is incorrect and potentially dangerous in a privileged context.

More broadly, a registry-provided vendor command must not automatically become arbitrary LocalSystem code execution.

Required correction:

1. Suspend automatic non-MSI Win32 removal until the hardened executor exists.
2. For MSI:
   - Validate an exact MSI product-code GUID.
   - Invoke the canonical `%SystemRoot%\System32\msiexec.exe`.
   - Never trust an arbitrary `MsiExec` path from the registry.
3. For non-MSI:
   - Parse with Windows command-line semantics, not first-space splitting.
   - Require a canonical absolute local executable path.
   - Reject relative paths, UNC paths, device paths, alternate data streams, shell metacharacters, and missing executables.
   - Reject or specially review script hosts and proxy executables including `cmd.exe`, PowerShell, `wscript.exe`, `cscript.exe`, `mshta.exe`, `rundll32.exe`, and similar launchers.
   - Inspect owner, ACLs, Authenticode signature, file identity, and writable parent directories.
   - Bind the removal request to the exact identity observed during enumeration to prevent a registry/path replacement race.
4. Prefer running the vendor uninstaller in the requesting interactive user’s context.
5. Request UAC elevation for the exact child process when machine-wide privilege is required.
6. LocalSystem execution must be reserved for reviewed built-in adapters with fixed executable paths and validated arguments.
7. Add explicit interactive-uninstaller support instead of forcing every vendor command into a silent SYSTEM operation.

Gate:

- Unquoted Program Files paths are parsed correctly.
- A writable or replaced executable is rejected.
- Relative, UNC, and shell-host commands are rejected.
- A registry change between enumeration and removal cannot redirect execution.
- Unit and security tests cover malicious uninstall strings.

### 2.5 P1: The wizard launches on every application start

The current shell selects **Get started** and navigates directly to `WizardPage` every time.

Required correction:

Persist local first-run state:

```text
setup_state
- schema_version
- completed_utc
- skipped_utc
- last_completed_version
- selected_level
- selected_policy_set_hash
```

Behavior:

- First launch: open the wizard.
- After completion: open Dashboard on later launches.
- After skip: open Dashboard later, while keeping **Get started** available.
- Major setup schema changes may show a non-blocking “Review new protections” card, not forcibly restart the wizard.
- Add **Run setup again** from Dashboard/Settings.

Do not store this state in roaming or cloud-backed settings.

### 2.6 P1: The review step is a count, not an exact plan

The design requires an exact diff. The current wizard shows counts and generic text.

Required correction:

- Call the service batch planner before the review screen.
- Show each planned change:
  - current value/state,
  - desired value/state,
  - risk,
  - whether reversible,
  - whether reboot/logoff is required,
  - source policy,
  - expected side effects.
- Show every app/program selected by name and source.
- Require a separate acknowledgement for irreversible actions.
- If the plan changes before apply, invalidate the review and require another review.
- Do not claim a Store app is reinstallable from the Microsoft Store when the product intentionally disables/removes Store access. Use provider-neutral wording such as “reinstall source will be recorded when known.”

### 2.7 P1: Hardening level does not govern app selection

The policy selection changes with Lite/Normal/Pro. The bloat list is filtered to recommended/removable items, and each `AppRowViewModel` preselects recommended items regardless of selected level.

Required correction:

- Add an explicit app-removal recommendation level to the service contract.
- Never infer hardening level solely in the UI.
- Lite, Normal, and Pro must produce distinct app suggestions.
- `Care`, `System`, `Unknown`, `Protected`, and irreversible/flagged entries are never auto-selected.
- Show optional non-recommended entries rather than hiding everything outside the recommended list.
- User customization must switch the preset label to `Customized`.

### 2.8 P1: No cancellation, resume, or navigation safety

Required correction:

- Add a `CancellationTokenSource` for scans and plans.
- Service-owned apply operations must continue safely or stop at an explicit boundary if the UI closes.
- Disable or warn on navigation while a local-only scan is running.
- Do not let page navigation orphan a privileged mutation.
- Expose active operations globally in the shell.
- On restart, show “An operation was interrupted/in progress” and resume status from the service.
- Never cancel midway through a non-cancellable installer phase and claim it stopped.

### 2.9 P1: Mutating app operations may proceed without durable audit

`TryAuditAsync` swallows event-store failures. That behavior is acceptable for secondary diagnostics but not for irreversible installs/removals.

Required correction:

- Before any irreversible operation, persist a durable operation record.
- If the durable write fails, do not execute the operation.
- Secondary informational events may remain best-effort.
- Distinguish:
  - required operation journal,
  - append-only user-visible audit,
  - diagnostic logging.
- Package installation/removal must never proceed with no durable record.

### 2.10 P1: There are no tests for the new wizard behavior

Required correction:

Add tests for:

- First-run routing.
- Skip/completion persistence.
- Preset selection.
- Customized selection state.
- Exact review-plan rendering.
- No apply before explicit Apply.
- UI-thread progress dispatch.
- Cancellation.
- Service disconnect.
- Batch failure before irreversible actions.
- Reconnect to active/completed operation.
- Duplicate idempotency key.
- Unexpected exception cleanup.
- App recommendation levels.

---

## 3. Persistent Desired State and Drift Reconciliation

The user’s approved configuration must survive reboot, servicing, cumulative updates, feature updates, package reprovisioning, and configuration drift.

Do not call the underlying OS literally immutable. Sovereign is a desired-state reconciler. It observes and repairs the areas it explicitly manages.

### 3.1 Desired-state modes

Each managed item must have one of these modes:

```text
Disabled
- Sovereign does not monitor or enforce this item.

Observe
- Record state changes locally.
- Do not notify or repair.

Notify
- Detect drift and notify the user.
- Do not repair automatically.

Enforce
- Detect drift.
- Repair when the repair is pre-authorized and safe.
- Verify the repair.
- Notify the user with what changed and what Sovereign did.
```

Default behavior:

- Reversible machine-wide registry policy: `Enforce` after the user applies it.
- Firewall/network invariant: `Enforce` once network enforcement is available.
- Service/task state: `Notify` until individually verified, then eligible for `Enforce`.
- Removed Appx package: `Notify` by default.
- “Keep removed” Appx package: explicit opt-in `Enforce`, with package identity captured.
- Win32 uninstall: never auto-repeat solely because an uninstall registry entry reappears.
- Unsupported/Unknown state: never auto-repair.

### 3.2 Persisted model

Create a dedicated storage abstraction, not ad hoc fields in the event table.

Proposed tables:

```text
desired_state
- target_id TEXT PRIMARY KEY
- target_type TEXT NOT NULL
- target_version INTEGER NOT NULL
- mode TEXT NOT NULL
- desired_json TEXT NOT NULL
- source TEXT NOT NULL
- selected_level TEXT NULL
- created_utc TEXT NOT NULL
- updated_utc TEXT NOT NULL
- updated_by TEXT NOT NULL
- last_verified_utc TEXT NULL
- last_result TEXT NULL
- consecutive_failures INTEGER NOT NULL DEFAULT 0
- suspended_reason TEXT NULL

reconciliation_runs
- id INTEGER PRIMARY KEY
- correlation_id TEXT NOT NULL UNIQUE
- trigger TEXT NOT NULL
- started_utc TEXT NOT NULL
- completed_utc TEXT NULL
- os_build_before TEXT NULL
- os_build_after TEXT NULL
- status TEXT NOT NULL
- checked_count INTEGER NOT NULL
- drift_count INTEGER NOT NULL
- repaired_count INTEGER NOT NULL
- failed_count INTEGER NOT NULL
- detail_json TEXT NULL

drift_records
- id INTEGER PRIMARY KEY
- reconciliation_id INTEGER NOT NULL
- target_id TEXT NOT NULL
- detected_utc TEXT NOT NULL
- previous_verified_json TEXT NULL
- observed_json TEXT NULL
- desired_json TEXT NOT NULL
- action TEXT NOT NULL
- result TEXT NOT NULL
- detail TEXT NULL
- acknowledged_utc TEXT NULL
- acknowledged_by TEXT NULL

operations
- operation_id TEXT PRIMARY KEY
- operation_type TEXT NOT NULL
- requested_by TEXT NOT NULL
- requested_utc TEXT NOT NULL
- state TEXT NOT NULL
- request_json TEXT NOT NULL
- plan_json TEXT NULL
- result_json TEXT NULL
- started_utc TEXT NULL
- completed_utc TEXT NULL
```

Do not let multiple store classes independently improvise schema versioning. Introduce one database migrator responsible for the shared SQLite schema.

Migration requirements:

- Transactional.
- Forward-only.
- Tested from every released schema version.
- Database backup before destructive migration.
- Failure prevents privileged mutation and reports degraded read-only state.
- Corruption must not silently clear desired state or widen access.

### 3.3 Service startup behavior

The installed service must eventually use Automatic startup for protected mode.

Do not perform a long reconciliation inside service initialization before reporting Running. The Windows Service Control Manager expects service startup to complete promptly.

Required architecture:

```text
StorageInitializer
NamedPipeServer
ReconciliationCoordinator : BackgroundService
RegistryDriftWatcher : BackgroundService
OperationRecoveryService : BackgroundService
```

Startup sequence:

1. Open and migrate storage.
2. Validate desired-state database integrity.
3. Start IPC.
4. Report service running.
5. Queue `ServiceStart` reconciliation.
6. Wait for a short configurable local stabilization delay.
7. Reconcile machine-wide desired state.
8. Persist results.
9. Queue user notifications for the next interactive session.

Install/update scripts must:

- set service startup to Automatic when enforcement is enabled,
- configure service recovery/restart behavior,
- preserve a documented safe-mode/emergency disable path,
- not create an allow-all networking interval.

The service’s own binary and configuration must be protected from non-admin modification.

### 3.4 Reconciliation triggers

Implement trigger abstraction:

```text
ReconciliationTrigger
- ServiceStart
- Boot
- UserLogon
- Resume
- UpdateWindowClosed
- WindowsBuildChanged
- PackageInventoryChanged
- RegistryWatcher
- ScheduledLocalCheck
- Manual
- Recovery
```

Initial required triggers:

1. Service start after reboot.
2. Manual **Check now**.
3. Update Window completion.
4. Detection that Windows build/revision changed since last verified state.
5. Low-frequency local fallback check.

Later triggers:

- registry watcher,
- service/task watcher,
- package inventory watcher,
- user-logon helper for HKCU state.

No trigger may contact the internet merely to perform reconciliation.

### 3.5 Reconciliation algorithm

For each run:

1. Acquire a single reconciliation lease.
2. Record trigger and baseline metadata.
3. Load enabled desired-state entries.
4. Resolve current implementation/version.
5. Detect current state.
6. If Compliant:
   - update last verified time/result.
7. If Unsupported or Unknown:
   - record drift/uncertainty,
   - do not repair,
   - notify according to mode.
8. If NonCompliant:
   - create a drift record before mutation.
9. If mode is Observe:
   - record only.
10. If mode is Notify:
   - queue notification.
11. If mode is Enforce:
   - verify the target remains safe for automatic repair,
   - apply the smallest repair,
   - independently verify,
   - update the drift record,
   - queue a notification.
12. Update failure counters.
13. Release lease and finalize summary.

Never infer compliance from a successful write or exit code.

### 3.6 Repair-loop protection

Windows or another policy engine may continuously rewrite a value.

Implement:

- per-target debounce,
- exponential backoff,
- maximum automatic repairs per boot and per 24 hours,
- consecutive-failure counter,
- circuit breaker,
- `Contested` state,
- one consolidated notification rather than a toast storm.

Example:

> Windows changed “Search web results” three times after Sovereign repaired it. Automatic repair has been paused. Review the conflict.

Do not fight Group Policy, MDM, domain management, or an administrator indefinitely.

### 3.7 Registry watch support

Use documented registry-change notifications where appropriate.

Rules:

- Watch only the registry roots required by active desired-state entries.
- Use `RegNotifyChangeKeyValue` or a reviewed equivalent.
- Re-arm the watch after each signaled change.
- Debounce bursts.
- Treat a watch as a hint, then run normal Detect logic.
- Do not assume the changed value is the managed value.
- Do not rely exclusively on watchers; retain service-start and periodic reconciliation.
- Handle key deletion/recreation.
- Avoid leaking handles or registering duplicate waits.

Official research starting point:

- `https://learn.microsoft.com/windows/win32/api/winreg/nf-winreg-regnotifychangekeyvalue`

Create a dated research document and a fake watcher for deterministic tests.

### 3.8 Update-aware baseline

Before a Sovereign-controlled Windows Update Window:

1. Persist active desired state.
2. Record current Windows build/revision.
3. Record managed package inventory.
4. Record relevant services, scheduled tasks, Appx provisioning, firewall rules, and feature states.
5. Mark an update operation in progress.

After update/reboot:

1. Detect incomplete update operation.
2. record new build/revision and update identifiers when available.
3. Run full reconciliation.
4. Identify:
   - settings reset,
   - policies removed,
   - services/tasks re-enabled,
   - Appx packages reprovisioned,
   - new packages,
   - firewall changes,
   - network-enforcement changes.
5. Repair only pre-authorized safe targets.
6. Produce one update drift report.
7. Notify the user.

Example report:

```text
Windows update changed 7 managed items.

Repaired automatically:
- Web search in Start
- Advertising ID
- Delivery Optimization
- Spotlight suggestions

Needs review:
- Copilot package was provisioned again
- New scheduled task: ...
- Defender cloud setting is now managed by policy
```

Failure to restore locked network mode after an update remains release-blocking.

### 3.9 Notifications

The service cannot depend on the main UI being open.

Implement a queued notification model:

```text
notifications
- id
- type
- severity
- title
- body
- related_target_id
- related_operation_id
- created_utc
- delivered_utc
- acknowledged_utc
- action_json
```

Initial delivery may occur when the main UI next connects.

For real-time notifications with the UI closed, design a separate unelevated per-user `Sovereign.Agent` process launched at user logon. Do not auto-launch the full control panel.

`Sovereign.Agent` responsibilities:

- authenticate to the service,
- display local notifications,
- open the relevant control-panel page,
- handle per-user/HKCU reconciliation requests,
- contain no privileged mutation logic,
- perform no network access.

Add an ADR before introducing the user agent.

### 3.10 Drift UI

Add:

- Dashboard card: **System drift**
- Last verification time.
- Current reconciliation status.
- Number repaired automatically.
- Number needing review.
- **Check now**.
- **View report**.
- Per-policy enforcement-mode selector.
- Global mode:
  - Monitor only
  - Recommended enforcement
  - Strict enforcement

Avoid the word “immutable” as an unconditional guarantee. Suggested wording:

> Sovereign keeps your approved configuration enforced and tells you whenever Windows changes it.

### 3.11 Drift CLI

Add:

```text
sov desired list
sov desired show <id>
sov desired set-mode <id> observe|notify|enforce|disabled
sov reconcile run
sov reconcile status
sov reconcile history
sov drift list
sov drift acknowledge <id>
```

CLI and UI must use the same service contracts.

### 3.12 Drift tests

Unit:

- Desired-state serialization.
- Mode evaluation.
- Reconciliation decisions.
- Unknown/Unsupported never repair.
- Debounce.
- Circuit breaker.
- Idempotent compliant run.
- Changed implementation version.
- Duplicate trigger coalescing.

Integration:

- Persist desired state, restart service/store, reconcile.
- Simulate a registry value reverting after apply.
- Enforce repairs and verifies.
- Notify records but does not repair.
- Audit failure prevents an irreversible action.
- Database migration.
- Interrupted reconciliation recovery.
- Update operation survives restart.

Failure injection:

- Database locked.
- Database corrupt.
- Provider read failure.
- Provider write failure.
- Verification failure.
- Service killed during repair.
- Reboot between record and apply.
- Watcher handle failure.
- Repeated external rewrite.
- Notification consumer unavailable.

System/VM:

- Apply profile.
- Reboot.
- Verify automatic service startup.
- Verify local reconciliation.
- Manually revert managed settings.
- Reboot.
- Verify drift detected and repaired/notified according to mode.
- Simulate or install a cumulative update in the lab.
- Verify post-update report.
- Verify no network access occurred during local reconciliation.

---

## 4. Software Center Product Decision

Add a friendly **Software** area that replaces the need for the Microsoft Store for ordinary desktop software while teaching users that WinGet, Scoop, and Chocolatey exist.

This is not a new package repository.

Sovereign is a broker, UI, consent layer, audit layer, and network gate over package providers.

### 4.1 Navigation

Replace or evolve **Apps & debloat** into **Software** with these sections:

- Discover
- Installed
- Updates
- Sources
- History
- Debloat

Do not remove the focused debloat workflow; make it a filtered management view.

### 4.2 Provider rollout

Strict order:

1. WinGet
2. Scoop
3. Chocolatey
4. Optional developer providers later

Do not implement all providers at once.

WinGet is the default because it provides a broad community catalog, structured manifests, hashes, versions, and official Windows integration.

Scoop and Chocolatey are opt-in because their execution and trust models differ.

Do not mix pip/npm/Bun/.NET tools into the default consumer software catalog. Put them under a later **Developer packages** feature.

### 4.3 New project boundary

Create:

```text
src/Sovereign.Packages/
tests/Sovereign.Packages.UnitTests/
tests/Sovereign.Packages.IntegrationTests/
```

Proposed contract:

```text
IPackageProvider
- ProviderId
- DisplayName
- DetectAvailabilityAsync
- GetCapabilitiesAsync
- ListSourcesAsync
- SearchAsync
- GetPackageDetailsAsync
- ListInstalledAsync
- ListUpdatesAsync
- PlanDownloadAsync
- DownloadAsync
- PlanInstallAsync
- InstallAsync
- UpgradeAsync
- UninstallAsync
- PinAsync
- CancelAsync
```

Provider-independent models must preserve source identity. Do not merge packages from multiple providers into one ambiguous package.

Identity:

```text
PackageIdentity
- ProviderId
- SourceId
- PackageId
- Version
- Channel
- Architecture
- Scope
```

### 4.4 WinGet integration

Prefer the supported Windows Package Manager API/COM API for:

- structured search,
- package metadata,
- install progress,
- cancellation,
- result codes.

Do not scrape localized CLI tables.

A reviewed CLI adapter may be used as a temporary fallback for capabilities unavailable through the API, but:

- use machine-readable output when available,
- pass arguments as structured arguments,
- never concatenate shell command strings,
- never use CLI text as the security boundary,
- record exact executable version and command,
- isolate parsing behind tests and fixtures.

Official research starting points:

- `https://learn.microsoft.com/windows/package-manager/winget/`
- `https://learn.microsoft.com/windows/package-manager/winget/source`
- `https://github.com/microsoft/winget-cli/blob/master/doc/specs/%23888%20-%20Com%20Api.md`

Create a dated research record before implementation.

### 4.5 Sources

Initial recommended state:

```text
winget       Enabled, explicit target for all Sovereign searches/installations
msstore      Disabled or explicit-only
winget-font  Disabled by default or explicit-only
```

Never run a source reset casually because WinGet reset restores default sources, including `msstore`.

Source UI must show:

- source name,
- type,
- endpoint,
- trust level,
- explicit/implicit state,
- last refresh,
- enabled state,
- owner/description,
- whether the provider can execute scripts.

No catalog refresh occurs merely because the user opens the page.

Search behavior:

1. User enters a query.
2. Sovereign explains which source will be contacted.
3. User initiates Search.
4. A temporary network grant is opened.
5. Search runs.
6. Grant closes.
7. Results remain cached locally with a visible timestamp.

### 4.6 “Free store” and curated discovery

Create a small, local, versioned **Sovereign Picks** catalog shipped with the application.

It contains references, not binaries:

```text
SovereignPick
- Id
- Title
- Description
- Category
- ProviderId
- SourceId
- PackageId
- LicenseClass
- Homepage
- WhyRecommended
- AlternativesTo
- ReviewedVersion
- ReviewedUtc
```

Goals:

- Highlight free and open-source software.
- Provide simple replacements for common Microsoft Store apps.
- Teach package-manager concepts through visible equivalent commands.
- Work without a Sovereign cloud service.

Example categories:

- Browsers
- Media
- Graphics
- Productivity
- Utilities
- Development
- Privacy
- Backup
- Compression
- Communication

Catalog update policy:

- Updated only through a Sovereign release or explicit signed catalog import.
- No silent remote catalog update.
- Never label an item “curated” without a local review record.
- Curated status does not bypass hash, signature, source, or network checks.

Each package page should show:

```text
Powered by WinGet

Equivalent command:
winget install --exact --id Vendor.Application --source winget
```

This is educational, not decorative. Users should leave understanding that these tools are accessible without Sovereign.

### 4.7 Package-card requirements

Show:

- Name.
- Description.
- Publisher.
- Provider.
- Source.
- Package ID.
- Available version.
- Installed version.
- License classification.
- Installer type.
- Architecture.
- Scope.
- Administrator requirement.
- Download domains/URLs when the provider exposes them.
- Manifest hash.
- Expected signer when known.
- Curated status.
- Last metadata refresh.
- Equivalent package-manager command.

Never load remote icons automatically. Use:

- provider-supplied local cached icon,
- locally shipped curated icon,
- generic glyph,
- explicit user-requested remote image retrieval.

### 4.8 Install transaction

Every install or update is a service-owned operation:

1. Resolve exact provider/source/package/version.
2. Create durable operation record.
3. Build a plan.
4. Display plan and consequences.
5. Require approval.
6. Open minimum temporary provider network grant.
7. Download to a private staging directory where possible.
8. Verify manifest hash.
9. Verify Authenticode where applicable.
10. Optionally scan locally with Defender if available and configured.
11. Close download-only access when possible.
12. Launch installer in the requesting user’s context.
13. Elevate only the exact installer when required.
14. Block or prompt on unexpected installer/child-process network requests.
15. Capture newly created services, tasks, startup entries, firewall rules, and packages.
16. Verify installed result.
17. Close all temporary grants.
18. Persist final result.
19. Offer network-rule review for the newly installed application.

Never run third-party installers as LocalSystem by default.

### 4.9 Bootstrap installers

Some installers download the application during execution.

Detect when possible and show:

> This is a web/bootstrap installer. It may contact destinations not declared in the package manifest.

Policy:

- Prefer offline installers.
- Do not grant arbitrary network access to every child process.
- Allow user-approved temporary scoped access.
- Record actual attempted destinations.
- Close grants when the installer exits or the operation expires.

### 4.10 Installed software inventory

Unify:

- Appx/MSIX.
- Win32 uninstall registry.
- WinGet correlations.
- Scoop.
- Chocolatey.
- Later developer providers.

Show:

- install source,
- version,
- update availability,
- uninstall mechanism,
- reversible status,
- background services,
- scheduled tasks,
- startup entries,
- firewall rules,
- outbound permission summary,
- whether Sovereign installed it,
- operation history.

Do not claim perfect source attribution when evidence is incomplete. Use `Unknown source`.

### 4.11 Updates

No background package update checks by default.

Flow:

1. User opens Updates.
2. Existing cached state is shown without networking.
3. User clicks **Check for updates**.
4. UI lists providers/sources that will be contacted.
5. Temporary grants open.
6. Providers scan.
7. Grants close.
8. Results are shown.
9. User selects versions.
10. User approves installation.
11. Each update follows the normal install transaction.
12. Drift reconciliation runs afterward.
13. One report shows software changes and Windows-setting drift.

Support:

- package pinning,
- ignore version,
- provider-specific constraints,
- per-package auto-update only as a future explicit opt-in capability.

Do not add a tray updater that polls silently.

### 4.12 Scoop

Scoop is phase two.

Requirements:

- Explicit user opt-in.
- Per-user install preferred.
- Buckets are separate sources.
- Show each bucket URL and trust status.
- Bucket add/remove is audited.
- Inspect manifest before installation.
- Preserve hash validation.
- Do not install Scoop automatically during initial setup without explicit approval.

### 4.13 Chocolatey

Chocolatey is phase three and higher-risk.

Requirements:

- Explicit user opt-in with explanation that packages may execute PowerShell install scripts.
- Show package source and script content before privileged install when available.
- Never silently approve prompts or license terms.
- Never treat a Chocolatey package identifier as equivalent to a WinGet package with the same display name.
- Apply strict operation journaling and temporary network grants.
- Consider a provider risk badge.

### 4.14 Software Center tests

Unit:

- Provider capability negotiation.
- Provider/source/package identity.
- Duplicate package disambiguation.
- Query normalization.
- Exact-version resolution.
- Plan invalidation.
- Equivalent command rendering.
- Curated-catalog signature/version.
- No remote icon request by default.

Integration with fake providers:

- Search.
- Detail.
- Install plan.
- Download.
- Hash mismatch.
- Signer mismatch.
- Cancellation.
- Installer failure.
- Reboot-required result.
- Source disabled.
- Provider absent.
- Duplicate operation key.
- Restart during operation.

Security:

- Package ID cannot inject arguments.
- Source URL cannot inject arguments.
- Malicious metadata cannot execute code or break XAML.
- Installer does not run as LocalSystem.
- Unexpected child-process traffic remains blocked.
- Operation journal failure prevents install.
- Source reset cannot silently re-enable `msstore`.
- Hash mismatch is release-blocking for the operation.
- Signature display is informational and does not bypass user consent.

System/VM:

- No network request when Software page opens.
- Search contacts only the selected source.
- Temporary grant closes after search.
- Install leaves no provider-wide permanent allow rule.
- Update check occurs only after explicit click.
- External packet capture confirms no provider traffic outside approved operations.

---

## 5. Network Enforcement Dependency

The complete Software Center depends on Milestones 3 and 4:

- default-deny outbound filtering,
- temporary scoped grants,
- connection events,
- executable/service identity,
- notifications,
- explicit rule decisions.

Allowed work before M3/M4:

- provider contracts,
- fake provider,
- local curated catalog,
- offline UI,
- operation journal,
- plan models,
- source configuration models.

Not allowed before M3/M4 gates pass:

- claiming that searches/installs are network-contained,
- automatic temporary network permissions,
- unrestricted provider execution,
- production installer orchestration presented as Sovereign-controlled.

---

## 6. Required Architecture Decisions and Research

Create before implementation:

```text
docs/decisions/0006-service-owned-batch-operations.md
docs/decisions/0007-persistent-desired-state-and-reconciliation.md
docs/decisions/0008-per-user-agent-and-notifications.md
docs/decisions/0009-package-provider-broker.md
docs/decisions/0010-installer-execution-boundary.md
```

Create dated research:

```text
docs/research/YYYY-MM-DD-windows-service-startup-and-recovery.md
docs/research/YYYY-MM-DD-registry-change-notification.md
docs/research/YYYY-MM-DD-windows-update-drift-signals.md
docs/research/YYYY-MM-DD-winget-api-and-source-control.md
docs/research/YYYY-MM-DD-scoop-security-model.md
docs/research/YYYY-MM-DD-chocolatey-security-model.md
docs/research/YYYY-MM-DD-user-context-installer-launch.md
```

Use primary sources first.

Document uncertainties rather than filling them with plausible-looking registry folklore.

---

## 7. Revised Implementation Order

Do not implement this as one giant agent run or one giant commit.

### Gate A: Current-code stabilization

Deliver:

- UI included in CI/verification.
- Wizard UI-thread fix.
- `try/finally` and cancellation correctness.
- First-run persistence.
- Exact review plan.
- Batch operation contract.
- Hardened/suspended Win32 uninstaller path.
- Wizard tests.

Stop and report.

### Gate B: Persistent desired state

Deliver:

- Central SQLite migration owner.
- Desired-state store.
- Reconciliation run/drift/operation records.
- Manual reconciliation.
- Service-start reconciliation.
- Observe/Notify/Enforce modes.
- Reconciliation UI and CLI.
- Tests.

Stop and report.

### Gate C: Boot and update resilience

Deliver:

- Automatic service configuration for enforcement mode.
- Startup operation recovery.
- Build-change/update-aware reconciliation.
- Update Window pre/post baseline hooks.
- Notification queue.
- Registry watcher with fallback scan.
- Loop protection.
- VM reboot and simulated-update tests.

Stop and report.

### Gate D: Network enforcement

Complete existing Milestones 3 and 4.

Stop and prove with external packet capture.

### Gate E: Software Center foundation

Deliver:

- `Sovereign.Packages`.
- Fake provider.
- Local curated catalog.
- Provider-independent models.
- Software navigation and offline pages.
- Operation plan UI.
- No real network provider yet.

Stop and report.

### Gate F: WinGet provider

Deliver:

- WinGet availability detection.
- Source management.
- Explicit source selection.
- Search/details.
- Installed/update inventory.
- User-initiated refresh only.
- Install/update transaction.
- Equivalent commands.
- Temporary network integration.
- Full tests.

Stop and report.

### Gate G: Optional providers

- Scoop.
- Chocolatey.
- Developer providers only after separate approval.

---

## 8. Documentation Updates

Update after each gate:

- `README.md`
- `SECURITY.md`
- `docs/architecture.md`
- `docs/threat-model.md`
- `docs/test-strategy.md`
- `docs/milestones.md`
- `docs/ui-design.md`
- `docs/setup-wizard-design.md`
- `docs/reversibility.md`

Correct stale claims immediately.

Specifically, stop claiming “one restore point” until a real batch restore point exists.

Document that Appx and Win32 removals are not equivalent to reversible registry policies.

---

## 9. Final Acceptance Criteria

This directive is complete only when all applicable statements are true:

### Wizard and operations

- The UI is built in CI.
- The wizard opens only on true first run unless manually launched.
- Review displays an exact service-generated plan.
- Apply is a durable service-owned operation.
- Reversible policies use one batch restore point.
- A reversible failure prevents irreversible removals.
- The UI may disconnect and reconnect without losing operation state.
- No UI control is accessed from a worker thread.
- No arbitrary vendor command executes as LocalSystem.

### Desired state

- Approved desired state persists across service and OS reboot.
- Service startup queues reconciliation without blocking service initialization.
- Drift is detected after reboot.
- Enforce mode repairs only pre-authorized supported targets.
- Notify mode never repairs.
- Unknown/Unsupported never repairs.
- Every repair is independently verified.
- Repeated external rewrites trigger a circuit breaker.
- The user receives a local drift report.
- Local reconciliation makes no internet connection.

### Updates

- Pre-update baseline is persisted.
- Post-update drift is detected.
- Safe approved settings are restored.
- Reintroduced apps/tasks/services are surfaced.
- Network locked mode is restored.
- One update report explains everything that changed.

### Software Center

- Opening Software makes no network request.
- Search/update checks are explicit.
- WinGet source is explicitly selected.
- `msstore` is not silently restored or queried.
- Package identity includes provider and source.
- Every install has a durable plan and operation record.
- Hash mismatch blocks installation.
- Third-party installers do not run as LocalSystem by default.
- Temporary network grants always close.
- Equivalent package-manager commands are visible.
- Curated recommendations are local and versioned.
- External packet capture verifies network claims.

---

## 10. Agent Execution Instruction

Start with **Gate A only**.

Before editing:

1. Re-read `agent_start.md`.
2. Re-read the files named in the review.
3. Run the current full build, including the UI.
4. Record all current failures.
5. Create the required ADR for service-owned batch operations.
6. Produce a concrete file-level plan.
7. Implement the smallest complete Gate A.
8. Add tests.
9. Run full verification.
10. Update documentation.
11. Stop and provide the required task report.

Do not begin desired-state reconciliation, WFP, or the package provider until Gate A is green.

When a product claim and implementation disagree, correct the implementation or correct the claim. Do not leave reassuring text attached to behavior the system does not provide.
