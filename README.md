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

**Milestone 0: Repository foundation.** This is scaffolding only. There is no enforcement,
no privileged behavior, no Windows service installation, and no network code yet. See
[`docs/milestones.md`](docs/milestones.md) for the roadmap and [`docs/architecture.md`](docs/architecture.md)
for the intended design.

What exists today:

- Solution and project boundaries for the managed components.
- Strict build configuration (nullable, analyzers, warnings-as-errors in production).
- A non-privileged unit test suite proving fail-closed contract defaults.
- Build/test/verify scripts and CI.
- Foundational documentation, threat-model draft, ADR and research templates.

## Supported systems

- **Target framework:** .NET 10 (`net10.0`).
- **Intended platform:** Windows 11 (supported editions/builds to be enumerated as policies
  are verified; unsupported builds are reported, never guessed).
- The UI (`Sovereign.UI`, WinUI 3) and native networking component (`Sovereign.Network`, WFP)
  are deferred to later milestones and exist only as documented placeholders.

## Known limitations (Milestone 0)

- No outbound filtering, notifications, policies, or drift detection are implemented.
- The Windows service host builds and idles; it does not install or enforce anything.
- The CLI prints version/help only.

## Prerequisites

- Windows 11.
- [.NET 10 SDK](https://dotnet.microsoft.com/) (pinned in [`global.json`](global.json)).
  Install via winget: `winget install --exact --id Microsoft.DotNet.SDK.10`.
- Git.

## Build and test

```powershell
# From the repository root:
./scripts/bootstrap.ps1   # verify prerequisites + restore
./scripts/build.ps1       # build the managed solution (Release)
./scripts/test.ps1        # run non-privileged unit tests
./scripts/verify.ps1      # format check + build + unit tests (the Milestone 0 gate)
```

Or directly with the .NET CLI:

```powershell
dotnet build Sovereign.slnx -c Release
dotnet test tests/Sovereign.UnitTests/Sovereign.UnitTests.csproj -c Release
```

## Layout

```text
src/        Managed components (Contracts, Policy, Storage, Service, CLI) + UI/Network placeholders
tests/      Unit (active) + integration/system/security/failure-injection (scaffolds)
docs/       Architecture, threat model, test strategy, milestones, decisions, research, runbooks
scripts/    bootstrap / build / test / verify / restore-network
tools/      Lab, packet-capture, and policy-fixture placeholders
```

## Security

See [`SECURITY.md`](SECURITY.md) for the security model and non-goals. Sovereign is not a
debloat script; it is a desired-state manager, application firewall, update gate, audit
system, and user-consent boundary for Windows.
