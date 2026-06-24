# 0001: V1 architecture and toolchain baseline

- **Status:** Accepted
- **Date:** 2026-06-24
- **Deciders:** Sovereign maintainer

## Context

Sovereign is being bootstrapped from [`agent_start.md`](../../agent_start.md) and
[`instructions.txt`](../../instructions.txt). Milestone 0 establishes the repository
foundation and must fix the baseline technical decisions so later, riskier work is controlled
and testable. This ADR records the choices that the rest of the V1 work depends on.

## Decision

1. **Target framework `net10.0`.** The managed components target .NET 10, pinned in
   `global.json` (10.0.x, `rollForward: latestFeature`). NuGet versions are centrally managed
   in `Directory.Packages.props`.
2. **Standalone git repository.** Sovereign is its own repository rooted at the project folder,
   independent of any parent directory, so the "clean clone builds" gate is meaningful.
3. **Block-then-ask (V1) network model.** The first enforcement implementation blocks unknown
   outbound traffic and then prompts, using the user-mode Windows Filtering Platform. This
   needs no kernel driver, works under Secure Boot, and never lets a blocked connection leave
   the machine.
4. **No kernel driver in V1.** A WFP kernel callout driver for pause-and-ask authorization is
   explicitly out of scope until the user-mode implementation is stable, tested, threat-modeled,
   and approved in a separate ADR.
5. **UI and native networking deferred in Milestone 0.** `Sovereign.UI` (WinUI 3) is introduced
   in Milestone 1 and `Sovereign.Network` (native WFP) in Milestone 3. In Milestone 0 they are
   documented placeholder folders so the dotnet-based build and the clean-clone gate stay
   reliable. The modern `.slnx` solution format (default in the .NET 10 SDK and supported by
   Visual Studio 2022 17.14) is used.

## Alternatives considered

- **Target `net9.0` now, migrate later.** Rejected: both governing documents mandate .NET 10,
  the SDK is available, and migrating later adds churn for no benefit.
- **Keep Sovereign inside a larger parent repository.** Rejected: it weakens the clean-clone
  gate and mixes unrelated history and ignore rules.
- **Start with a kernel callout driver for true pause-and-ask.** Rejected for V1: signing,
  Secure Boot, and stability risk; block-then-ask is sufficient and far safer to develop.
- **Scaffold full WinUI 3 and native C++ projects in Milestone 0.** Rejected: expands the
  build surface and toolchain requirements without delivering Milestone 0 value, and risks the
  clean-clone gate.

## Security implications

- Default-deny, fail-closed, and privilege-separated design are preserved. Deferring the UI and
  native components does not weaken any boundary because no enforcement exists yet in
  Milestone 0.
- Avoiding a kernel driver in V1 reduces attack surface and the blast radius of defects.

## Privacy implications

- None introduced. The toolchain choices keep the product local-first; the only network use is
  developer/CI NuGet restore.

## Operational implications

- Contributors need the .NET 10 SDK (`winget install --exact --id Microsoft.DotNet.SDK.10`).
- `.slnx` requires the .NET 10 SDK / VS 17.14+ tooling.
- WinUI 3 and native C++ build prerequisites are introduced with their milestones, not now.

## Test requirements

- Milestone 0: non-privileged unit tests prove fail-closed contract defaults; CI builds the
  managed solution with warnings-as-errors and runs the unit tests.
- Later milestones attach their own integration/system/security/failure-injection coverage per
  `docs/test-strategy.md`.

## Rollback strategy

- Toolchain/structure decisions are reversible by editing `global.json`, the solution, and the
  build-configuration files; no system or persistent-data state is affected by this ADR.
