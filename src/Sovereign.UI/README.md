# Sovereign.UI

The unelevated .NET 10 **WinUI 3** control panel (agent_start.md section 3, ADR 0003).

## Status (Milestone 1b)

A minimal shell is implemented: a dashboard that connects to the service over the authenticated
local IPC client and shows service health plus recent activity, with a friendly offline state when
the service is not running. It is **unpackaged and self-contained** (ADR 0003).

The broader friendly-but-powerful design (clickable feature/app cards, profiles, the live
connection timeline, restore points) is specified in [`docs/ui-design.md`](../../docs/ui-design.md)
and lands in later milestones.

## Building

The UI is intentionally excluded from the default solution build and CI gate so that gate stays
fast and reliable. Build it explicitly (a runtime identifier is required for self-contained WinUI):

```powershell
scripts\build.ps1 -Full                 # builds the solution + UI (win-x64)
# or directly:
dotnet build src\Sovereign.UI\Sovereign.UI.csproj -c Release -r win-x64
```

## Constraints

- Runs **unelevated** (`asInvoker`). It never mutates privileged machine state directly; it asks
  the service over authenticated local IPC (`Sovereign.Ipc`).
- References only `Sovereign.Ipc` (and `Sovereign.Contracts` transitively), never privileged
  implementation projects (agent_start.md section 4).
- App notifications (Milestone 4) require package identity; that migration will be recorded in its
  own ADR at that time.
