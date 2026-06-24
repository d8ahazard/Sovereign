# Sovereign.UI (deferred to Milestone 1)

This folder reserves the architecture boundary for the **unelevated .NET 10 WinUI 3** control
panel described in [`agent_start.md`](../../agent_start.md) section 3.

## Why this is a placeholder in Milestone 0

Milestone 0 delivers only the repository foundation and must keep the "clean clone builds"
gate reliable using the plain `dotnet` toolchain. A WinUI 3 project requires the Windows App
SDK and additional build components that are introduced deliberately in Milestone 1
("Service, UI, and IPC skeleton"). Adding it now would expand the build surface without
delivering Milestone 0 value.

The friendly-but-powerful design (badges, clickable feature/app cards, profiles, the live
connection timeline, restore points) is specified in [`docs/ui-design.md`](../../docs/ui-design.md).

## Responsibilities (when implemented)

- Dashboard, connection prompts, settings, profiles, searchable event history.
- Notifications, update selection, rule editing, drift reports.
- Must run **unelevated**. It must never directly mutate privileged machine state; it asks
  the service over authenticated local IPC.

## Constraints

- Windows App SDK notifications do not work from elevated processes, which is one reason the
  UI and privileged controller are separate processes.
- Do not let the UI project reference privileged implementation projects directly
  (`agent_start.md` section 4).

This README is intentionally the only content here until Milestone 1.
