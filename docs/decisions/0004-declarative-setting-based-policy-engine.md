# 0004: Declarative setting-based policy engine

- **Status:** Accepted
- **Date:** 2026-06-24
- **Deciders:** Sovereign maintainer

## Context

Milestone 2 requires a policy engine where every policy supports Detect, Plan, Apply, Verify,
Repair, and Rollback; execution is transactional; partial failures roll back safely; repeated apply
is idempotent; and `Unknown` is never treated as compliant ([`agent_start.md`](../../agent_start.md)
section 8, Milestone 2 gate). The reversibility model ([`reversibility.md`](../reversibility.md))
mandates capturing original state before any change.

We must deliver this without touching the real machine yet (Milestone 2 ships only "initial
harmless test policies"; real registry/Appx policies arrive in Milestone 5).

## Decision

Model policies **declaratively** as a set of desired settings over an abstract
**`ISettingProvider`**, and put the transactional Detect/Plan/Apply/Verify/Rollback orchestration
in a single, generic **`PolicyEngine`**.

- A policy exposes metadata (id, version, title, description, risk, scope, reboot/logoff needs), a
  supportability check, and a list of `DesiredSetting`s (key + desired value, where a value may be
  "absent"). Concrete policies are pure data + metadata; they perform no I/O themselves.
- `ISettingProvider` is the seam between the engine and the underlying system. Milestone 2 ships
  an in-memory provider (a harmless sandbox). Milestone 5 adds registry/Appx-backed providers
  behind the same interface, so the engine, transactionality, and tests are reused unchanged.
- The engine derives behavior generically:
  - **Detect:** compare current vs desired per setting. All match -> `Compliant`; any differ ->
    `NonCompliant`; provider failure -> `Unknown`; unsupported -> `Unsupported`. `Unknown` and
    `Unsupported` are never compliant.
  - **Plan:** the concrete list of changes (key, from, to, explanation); empty plan means already
    compliant.
  - **Apply (transactional, capture-before-change):** capture each target's current value, persist
    a restore point, apply each change, verify each change and then the whole policy. On any
    failure, restore every captured value and verify the restore. Success -> `Applied`; a verify
    failure that was rolled back -> `VerificationFailed`; a failed rollback -> `RollbackFailed`.
    Never `Compliant` on a failed apply.
  - **Repair:** identical to Apply when drifted (Apply is idempotent: an empty plan is a no-op that
    reports `Compliant`).
  - **Rollback:** load the latest persisted restore point for the policy and restore the captured
    values, then verify.
- A **restore point** is the captured pre-apply state, persisted in local storage
  (`IRestorePointStore`), enabling user-initiated rollback after a successful apply.
- Every operation is audited to the local event store with a per-execution correlation id.

## Alternatives considered

- **Imperative policies** (each policy implements Detect/Apply/Rollback by hand). Rejected for V1:
  it duplicates transactional/rollback logic per policy and is far easier to get wrong (the exact
  failure mode agent_start.md section 8 warns about). The declarative model makes idempotency and
  reversibility structural rather than per-policy discipline.
- **Bind the engine directly to the registry now.** Rejected: Milestone 2 must stay harmless and
  fully testable; the provider seam defers real system mutation to Milestone 5 while exercising the
  full engine.

## Security implications

- Apply and Rollback are the first **mutating** privileged operations exposed over IPC. They remain
  behind the authenticated, ACL'd pipe and the authorization allow-list (ADR 0002), and every
  invocation is audited with the caller identity and a correlation id. In Milestone 2 they mutate
  only the in-memory sandbox provider, so no real machine state changes yet.
- Verification reads back real state rather than trusting a success return, per agent_start.md
  section 8.

## Privacy implications

- Restore points and audit stay local. Captured values are setting keys/values, not secrets;
  policies must not place secrets in setting values.

## Operational implications

- `Sovereign.Storage` gains a `restore_points` table (created idempotently).
- Adding a real provider (Milestone 5) is additive and does not change the engine or its tests.

## Test requirements

- Idempotent apply (apply twice -> second is a no-op `Compliant`).
- Partial-failure rollback (a provider that fails mid-apply leaves the system restored and never
  reports compliant).
- `Unknown`/`Unsupported` never compliant.
- Verify-failure path reports `VerificationFailed` after a successful rollback.
- Restore-point persistence and user-initiated rollback.
- Mutating IPC operations are authorized and audited.

## Rollback strategy

- The engine and provider interface are internal contracts; concrete providers can be swapped. The
  `restore_points` table is additive. No released data format depends on this yet.
