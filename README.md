# Sovereign

Sovereign is a local-first Windows control plane that makes Windows 11 behave like an
explicitly controlled application host rather than an advertising, telemetry, cloud, and AI
delivery platform.

> **Core promise:** Windows and installed applications cannot initiate an outbound connection
> unless an explicit local rule permits it. Unknown connections are blocked before
> transmission, recorded, and presented for approval.

Everything is stored locally. No account, no cloud service, no telemetry from Sovereign
itself, and no automatic update checks from Sovereign itself.

The binding specification for this repository is [`agent_start.md`](agent_start.md). The
original product brief is [`instructions.txt`](instructions.txt).

## Status

**Working privacy/performance tool with a real registry-backed policy engine and app debloat.**
Network enforcement (WFP, M3) and firewall review are still ahead, but the core is real: a LocalSystem
service applies documented, machine-wide Windows policy tweaks to the live registry (fully reversible
via capture-before-change restore points) and removes installed Appx/MSIX bloat for all users, driven
by a Fluent WinUI 3 dashboard with Lite/Normal/Pro hardening levels. See
[`docs/milestones.md`](docs/milestones.md) for the roadmap and
[`docs/architecture.md`](docs/architecture.md) for the design.

What exists today:

- A Windows service (LocalSystem) hosting an ACL'd named-pipe IPC endpoint with protocol-version
  negotiation and a fail-closed allow-list. See [ADR 0002](docs/decisions/0002-local-ipc-over-secured-named-pipes.md).
- A declarative policy engine (`Sovereign.Policy`) with detect / plan / apply / verify / rollback,
  transactional capture-before-change, idempotent apply, and `Unknown`-never-compliant semantics,
  backed by a **real Windows registry provider**. Apply/rollback are mutating IPC operations audited
  with the caller identity. See [ADR 0004](docs/decisions/0004-declarative-setting-based-policy-engine.md).
- A catalog of 13 machine-wide, reversible Windows policies (telemetry/CEIP, advertising ID, web
  search, Copilot + Recall + Edge AI sidebar, Cortana, activity history, delivery optimization,
  Spotlight, Game DVR, location, OneDrive sync, Edge taming) tagged with risk and a Lite/Normal/Pro
  hardening level (ADR 0005).
- App debloat: the service enumerates installed Appx/MSIX packages for all users, recommends known
  bloat, refuses protected system packages, and removes (plus deprovisions) the selected apps via the
  in-box `Appx` module. Removal is audited and surfaced honestly as non-reversible.
- A multi-page WinUI 3 dashboard ([ADR 0003](docs/decisions/0003-winui3-unpackaged-self-contained-v1.md)):
  Dashboard (health + compliance), Cleanup & hardening (level presets + per-policy tiles + Apply),
  Apps & debloat (review + remove installed packages), Activity (audit log), and Restore points
  (one-click Revert).
- A shared IPC client (`Sovereign.Ipc`) used by both the CLI and the UI.
- A local, versioned, append-only SQLite event store plus restore-point storage; both persist across
  restarts.
- `sov` CLI: `status`, `health`, `events`, `version`, and `policy list|detect|plan|apply|rollback`.
- Reversible service install/uninstall scripts.
- Strict build configuration, a fail-closed contract test suite, and unit/integration/security
  tiers, plus build/test/verify scripts and CI.

## Supported systems

- **Target framework:** .NET 10 (`net10.0`).
- **Intended platform:** Windows 11 (supported editions/builds to be enumerated as policies
  are verified; unsupported builds are reported, never guessed).
- The UI (`Sovereign.UI`, WinUI 3) is built via the `-Full` switch; the native networking component
  (`Sovereign.Network`, WFP) is deferred to Milestone 3 and exists only as a documented placeholder.

## Known limitations

- No outbound filtering, notifications, or drift detection are implemented yet (WFP lands in M3).
- Policies currently cover machine-wide (`HKLM`) registry tweaks; app debloat covers Appx/MSIX
  packages. Firewall review and per-user (`HKCU`) Win11 UX restorations (e.g. classic context menu)
  are designed (see `docs/`) but not yet wired. Win32 apps like OneDrive/Edge are tamed via registry
  policy rather than fully uninstalled (Edge uninstall is unsupported by Microsoft).
- Policies are applied only when explicitly requested; there is no background enforcement loop yet.
- Cross-user pipe-ACL denial is validated by design and unit tests but not yet by a multi-account
  VM system test.

## Prerequisites

- Windows 11.
- [.NET 10 SDK](https://dotnet.microsoft.com/) (pinned in [`global.json`](global.json)).
  Install via winget: `winget install --exact --id Microsoft.DotNet.SDK.10`.
- Git.

## Build and test

```powershell
# From the repository root:
./scripts/bootstrap.ps1        # verify prerequisites + restore
./scripts/build.ps1            # build the managed solution (Release)
./scripts/build.ps1 -Full      # also build the self-contained WinUI 3 UI (win-x64)
./scripts/test.ps1             # run non-privileged unit tests
./scripts/verify.ps1           # format check + build + unit/integration/security tests (the gate)
```

Run the service and talk to it:

```powershell
# Development (foreground, no install):
dotnet run --project src/Sovereign.Service
# In another terminal:
dotnet run --project src/Sovereign.CLI -- status
dotnet run --project src/Sovereign.CLI -- policy list
dotnet run --project src/Sovereign.CLI -- policy plan win.advertising-id-off
dotnet run --project src/Sovereign.CLI -- policy apply win.advertising-id-off
dotnet run --project src/Sovereign.CLI -- policy rollback win.advertising-id-off

# Or install as a Windows service that starts with Windows (elevated):
./scripts/install-service.ps1
./scripts/uninstall-service.ps1
```

> Applying a policy writes a real, machine-wide registry value. Every apply captures the original
> first, so anything you apply (from the CLI or the dashboard's Cleanup page) can be reverted from
> the Restore points page or `policy rollback`.

Launch the dashboard (after `./scripts/build.ps1 -Full`):

```powershell
Start-Process src\Sovereign.UI\bin\Release\net10.0-windows10.0.19041.0\win-x64\Sovereign.UI.exe
```

Or directly with the .NET CLI:

```powershell
dotnet build Sovereign.slnx -c Release
dotnet test tests/Sovereign.UnitTests/Sovereign.UnitTests.csproj -c Release
```

## Layout

```text
src/        Managed components (Contracts, Ipc, Policy, Storage, Service, CLI, UI) + Network placeholder
tests/      Unit/integration/security (active) + system/failure-injection (scaffolds)
docs/       Architecture, threat model, test strategy, milestones, decisions, research, runbooks
scripts/    bootstrap / build / test / verify / install-service / uninstall-service / restore-network
tools/      Lab, packet-capture, and policy-fixture placeholders
```

## Design and research

Design and research groundwork (guides current and later milestones):

- [`docs/ui-design.md`](docs/ui-design.md) - friendly-but-powerful control panel spec.
- [`docs/debloat-catalog.md`](docs/debloat-catalog.md) - comprehensive candidate list of apps,
  AI/cloud components, and features to manage, with risk and restore method.
- [`docs/reversibility.md`](docs/reversibility.md) - how every change is captured and undone.
- [`docs/research/`](docs/research/) - primary-source records for Appx debloat and privacy/Copilot
  controls.

## Security

See [`SECURITY.md`](SECURITY.md) for the security model and non-goals. Sovereign is not a
debloat script; it is a desired-state manager, application firewall, update gate, audit
system, and user-consent boundary for Windows.
