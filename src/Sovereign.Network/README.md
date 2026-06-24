# Sovereign.Network (deferred to Milestone 3)

This folder reserves the architecture boundary for the **native Windows networking
component** described in [`agent_start.md`](../../agent_start.md) section 3.

## Why this is a placeholder in Milestone 0

The bootstrap instructions (`agent_start.md` section 20) are explicit:

> Do not begin implementing WFP filters immediately.

Milestone 0 must not modify firewall state, install a service, or perform any privileged or
network action. A native C++ project (`.vcxproj`) also does not build under the plain
`dotnet` toolchain used for the Milestone 0 "clean clone builds" gate, so it is added in
**Milestone 3 ("Network enforcement prototype")** with its own research record and ADR.

## Responsibilities (when implemented)

- V1 uses the supported **Windows Filtering Platform (WFP)** and event APIs from user mode,
  with **no custom kernel driver**.
- V1 behavior is **block first, notify second**: outbound IPv4 and IPv6 are denied by
  default; loopback and explicitly configured LAN/DHCP/DNS are allowed; approved applications
  are allowed; unknown applications are blocked; drop events are surfaced to the service.

## Out of scope until a future, separately approved milestone

- A WFP **kernel callout driver** for pause-and-ask connection authorization. This requires
  Microsoft driver signing and a dedicated ADR and threat model before any work begins.

This README is intentionally the only content here until Milestone 3.
