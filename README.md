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

**Milestone 2: Declarative policy engine.** There is still no network enforcement (M3) and no real
registry/Appx changes yet (real policy providers land in M5). What works is the privileged-service
backbone plus a transactional, reversible policy engine acting on a harmless in-memory sandbox. See
[`docs/milestones.md`](docs/milestones.md) for the roadmap and
[`docs/architecture.md`](docs/architecture.md) for the design.

What exists today:

- A Windows service hosting an ACL'd named-pipe IPC endpoint with protocol-version negotiation and a
  fail-closed allow-list. See [ADR 0002](docs/decisions/0002-local-ipc-over-secured-named-pipes.md).
- A declarative policy engine (`Sovereign.Policy`) with detect / plan / apply / verify / rollback,
  transactional capture-before-change, idempotent apply, and `Unknown`-never-compliant semantics.
  Apply/rollback are the first mutating IPC operations and are audited with the caller identity. See
  [ADR 0004](docs/decisions/0004-declarative-setting-based-policy-engine.md). In M2 policies act only
  on an in-memory sandbox; real registry/Appx providers arrive in M5 behind the same seam.
- A shared IPC client (`Sovereign.Ipc`) used by both the CLI and the UI.
- A local, versioned, append-only SQLite event store plus restore-point storage; both persist across
  restarts.
- `sov` CLI: `status`, `health`, `events`, `version`, and `policy list|detect|plan|apply|rollback`.
- An unelevated, unpackaged, self-contained WinUI 3 dashboard ([ADR 0003](docs/decisions/0003-winui3-unpackaged-self-contained-v1.md)).
- Reversible service install/uninstall scripts.
- Strict build configuration, a fail-closed contract test suite, and unit/integration/security
  tiers, plus build/test/verify scripts and CI.

## Supported systems

- **Target framework:** .NET 10 (`net10.0`).
- **Intended platform:** Windows 11 (supported editions/builds to be enumerated as policies
  are verified; unsupported builds are reported, never guessed).
- The UI (`Sovereign.UI`, WinUI 3) is a minimal shell built via the `-Full` switch; the native
  networking component (`Sovereign.Network`, WFP) is deferred to Milestone 3 and exists only as a
  documented placeholder.

## Known limitations (Milestone 2)

- No outbound filtering, notifications, or drift detection are implemented.
- Policies act only on an in-memory sandbox provider; the service makes no real registry, service,
  task, Appx, or network change yet (real providers land in M5).
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
dotnet run --project src/Sovereign.CLI -- policy plan demo.telemetry-off
dotnet run --project src/Sovereign.CLI -- policy apply demo.telemetry-off
dotnet run --project src/Sovereign.CLI -- policy rollback demo.telemetry-off

# Or install as a Windows service (elevated):
./scripts/install-service.ps1
./scripts/uninstall-service.ps1
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
