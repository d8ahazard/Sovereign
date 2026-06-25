# 2026-06-24: Named-pipe IPC security on .NET 10

## Question

How does Sovereign create a local named-pipe server that an unelevated UI/CLI can call, while
guaranteeing an unauthorized local process cannot invoke privileged operations?

## Target Windows editions and builds

- Windows 11 24H2+, .NET 10. Service runs as LocalSystem; clients run as the interactive user.

## Primary sources

- NamedPipeServerStreamAcl.Create - https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstreamacl.create?view=net-10.0 (accessed 2026-06-24)
- Windows Exploitation Tricks: Spoofing Named Pipe Client PID (Google Project Zero) - https://projectzero.google/2019/09/windows-exploitation-tricks-spoofing.html (accessed 2026-06-24)
- Granting named-pipe ACLs for IPC in .NET (PipeSecurity / AuthenticatedUserSid pattern) - https://zenn.dev/suusanex/articles/b198c56217877b (accessed 2026-06-24)
- Windows Services, Named Pipes and UnauthorizedAccessException (Core ctor cannot take PipeSecurity; SetAccessControl no-op) - https://sjm.io/blog/named-pipe-acl/ (accessed 2026-06-24)

## Confirmed facts

- On .NET Core/5+/10 the `NamedPipeServerStream` constructor does not accept a `PipeSecurity`, and
  calling `SetAccessControl` after creation does not apply. The supported way to set the ACL at
  creation is `NamedPipeServerStreamAcl.Create(...)`.
- `PipeAccessRights.CreateNewInstance` is a powerful right: its holder can stand up further
  instances of the pipe and effectively act as the server. It must be withheld from clients.
- `GetNamedPipeClientProcessId` returns the connected client's PID but the PID is spoofable; using
  it for security (e.g. open process -> check signature) is exploitable. Do not use it as an
  enforcement mechanism.
- The robust model is OS access control: ACL the pipe to the intended principals, and use the
  authenticated Windows identity of the caller (impersonation) for auditing/authorization.

## Planned ACL (PipeSecurity)

- LocalSystem (service identity): FullControl.
- BUILTIN\Administrators: FullControl.
- Interactive logged-on user (or a tightly scoped group): `ReadWrite` only (no
  `CreateNewInstance`).
- No rule for Everyone/Anonymous.

## Assumptions (verify in a VM)

- The exact SID to grant for "the interactive user" on a single-user workstation (current
  interactive user SID vs a built-in group). Start with the interactive user SID; confirm
  behavior for fast-user-switching and RDP sessions.
- Behavior of impersonation (`RunAsClient`) under LocalSystem for capturing caller identity.

## Conflicting documentation

- Community samples sometimes grant `AuthenticatedUserSid`, which is broader than necessary. Prefer
  the narrowest principal that still lets the legitimate UI/CLI connect; revisit when multi-user
  scenarios are designed.

## Local reproduction steps (planned)

1. Host the pipe server in a LocalSystem service; connect from an interactive-user client; verify
   Hello/Health succeed.
2. Attempt to connect from a context outside the ACL; verify access is denied at the OS level.
3. Attempt an operation not in the authorization allow-list; verify rejection and audit entry.
4. Send malformed/oversized/unknown-version frames; verify rejection and connection close.

## Observed results

- Not yet executed; to be recorded during Milestone 1 implementation and a later VM/system test.

## Remaining uncertainty

- Full cross-user denial cannot be proven by in-process unit tests; it requires a multi-account
  VM/system test (tracked for a later milestone). Unit/integration tests cover the ACL builder,
  authorization allow-list, framing, and version negotiation.

## Impact on architecture and tests

- Drives ADR 0002. `Sovereign.Service` targets `net10.0-windows` for pipe-ACL APIs. Authorization
  is allow-list based and PID-independent. Security tests assert denial of out-of-allow-list
  operations and rejection of malformed/oversized/unknown-version messages.
