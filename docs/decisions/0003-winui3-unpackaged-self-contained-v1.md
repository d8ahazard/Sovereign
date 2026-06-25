# 0003: WinUI 3 unpackaged, self-contained for the V1 UI

- **Status:** Accepted
- **Date:** 2026-06-24
- **Deciders:** Sovereign maintainer

## Context

`Sovereign.UI` is a .NET 10 WinUI 3 app (agent_start.md section 3). It must run unelevated. App
notifications (a Milestone 4 feature) require package identity, but Milestone 1 only needs a
dashboard and event view. Research ([Distribute an unpackaged WinUI 3 app](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app))
confirms an unpackaged, self-contained shape that is simple to build and xcopy-deploy.

## Decision

Build the V1 UI as **unpackaged and self-contained**: `WindowsPackageType=None`,
`WindowsAppSDKSelfContained=true`, `UseWinUI=true`, explicit runtime identifiers (`x64;ARM64`),
referencing `Microsoft.WindowsAppSDK`. The UI references only `Sovereign.Ipc` (and
`Sovereign.Contracts` transitively), never privileged projects. The default CI/clean-clone build
excludes the UI (built via a `-Full` switch) so the Milestone 0 build gate stays reliable.

## Alternatives considered

- **Packaged (MSIX) with identity from the start.** Notifications-ready, but adds MSIX tooling and
  packaging to the build/CI now, for a feature not needed until Milestone 4. Rejected for V1.

## Security implications

- Unelevated UI; no privileged state mutation; communicates only via the authenticated IPC client
  (ADR 0002). Unpackaged vs packaged does not change the trust boundary.

## Privacy implications

- None. Local-only UI.

## Operational implications

- Self-contained output bundles the Windows App SDK + .NET runtime next to the exe (xcopy-able).
- Adding app notifications in Milestone 4 will require **package identity** (sparse/MSIX or
  external-location package); that migration will be recorded in its own ADR at that time.

## Test requirements

- The UI is thin: logic lives behind the IPC client and is covered by 1a tests. UI smoke
  build/run is validated via the `-Full` build and manual launch in Milestone 1b.

## Rollback strategy

- Packaging shape is a project-configuration choice; switching to packaged later is additive and
  reversible, gated by the Milestone 4 ADR.
