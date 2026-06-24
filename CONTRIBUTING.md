# Contributing to Sovereign

[`agent_start.md`](agent_start.md) is the binding instruction set for this repository. Read it
before contributing. This file summarizes the working agreement; where they differ,
`agent_start.md` wins.

## Per-task workflow (agent_start.md section 5)

1. Read `agent_start.md` and any repository-specific rules.
2. Inspect the relevant code, tests, docs, and recent decisions.
3. State a short implementation plan and its security, networking, privilege, persistence,
   and rollback implications.
4. Make the smallest complete change that satisfies the requirement.
5. Add or update tests **before** claiming completion.
6. Run the relevant test suites, static analysis, and formatting.
7. Verify behavior, not merely compilation.
8. Update documentation and decisions where architecture or behavior changed.
9. Report exactly what changed, what passed, what failed, and what remains uncertain
   (use the report format in `agent_start.md` section 19).

## Hard rules

- Do not weaken a security boundary to simplify implementation.
- Do not introduce hidden fallback behavior or swallow exceptions in security-sensitive paths.
- Do not disable or skip tests to obtain a green build.
- Do not invent undocumented Windows APIs, policy names, registry keys, event IDs, or service
  behavior. Research first (section 6) and record findings under `docs/research/`.
- Do not convert an enforcement error into an allow decision.
- Do not modify unrelated files merely to make a diff look cleaner.
- Do not change public contracts or persistent data formats silently.

## When an ADR is required (agent_start.md section 17)

Create an ADR under `docs/decisions/NNNN-title.md` (copy
[`docs/decisions/0000-adr-template.md`](docs/decisions/0000-adr-template.md)) before, for
example: adding a kernel driver, changing the privilege model, changing default-deny
semantics, adding any cloud or remote-update dependency, changing the IPC mechanism, changing
persistent rule identity, or weakening rollback requirements.

## Build, test, and formatting

```powershell
./scripts/bootstrap.ps1
./scripts/verify.ps1   # format check + build (warnings as errors) + unit tests
```

- Target framework is `net10.0` (pinned in [`global.json`](global.json)).
- NuGet versions are centrally pinned in
  [`Directory.Packages.props`](Directory.Packages.props); do not float versions.
- Production projects build with nullable reference types on and warnings treated as errors.
- Run `dotnet format Sovereign.slnx` before committing.

## Tests

- New behavior requires tests appropriate to its risk (see
  [`docs/test-strategy.md`](docs/test-strategy.md)).
- Unit tests must not require network access or elevation.
- Security-sensitive behavior needs normal, failure, and rollback path coverage.

## Commits

- Keep commits focused and descriptive.
- Never commit secrets or local runtime data (`*.sovereign.db`, captures, `local-data/`).
- Do not commit generated build output (`bin/`, `obj/`).
