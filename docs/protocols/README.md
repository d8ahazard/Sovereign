# Protocols

This folder will hold the versioned specifications for Sovereign's internal protocols,
primarily the **authenticated local IPC contract** between `Sovereign.UI` / `Sovereign.CLI`
and `Sovereign.Service` ([`agent_start.md`](../../agent_start.md) sections 3 and 15.2).

Each protocol specification must define: message/version negotiation, authentication and
authorization model, the full request/response schema, handling of unknown fields and
versions, replay protection where relevant, and error semantics. Changing the IPC mechanism
requires an ADR (section 17).

_No protocols are specified yet. The IPC contract is introduced in Milestone 1._
