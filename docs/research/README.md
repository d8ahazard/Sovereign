# Research records

Windows internals, security policy, update behavior, WFP, AppLocker, WinUI, service isolation,
driver signing, and enterprise policy all change over time. Treat memory as untrusted
([`agent_start.md`](../../agent_start.md) section 6).

For any material behavior based on external research, create a record here named:

```text
docs/research/YYYY-MM-DD-topic.md
```

## Source hierarchy (use in this order)

1. Microsoft Learn and official Windows documentation.
2. Windows SDK/WDK headers, samples, and source comments.
3. Official .NET and Windows App SDK documentation.
4. Published protocol specifications.
5. Reproducible local experiments on supported Windows builds.
6. Maintained, reputable open-source implementations.
7. Secondary technical analysis only when primary sources are incomplete.

Never use a blog post as the only authority for security-sensitive behavior. Never hard-code
Microsoft endpoints from an unverified third-party list.

## Verification bar

A researched claim is not implementation-ready until at least one of these is true: confirmed
by current official docs and matching SDK definitions; reproduced in a disposable Windows VM;
or verified by an automated integration/system test. For undocumented behavior, require a
reproducible experiment and mark the dependency as fragile.

## Record template

Copy the block below into a new dated file.

```markdown
# YYYY-MM-DD: <topic>

## Question
<the specific question being answered>

## Target Windows editions and builds
<editions and build numbers in scope>

## Primary sources
- <title> - <url> (accessed YYYY-MM-DD)

## Relevant identifiers
<APIs, policies, registry keys, services, packages, event IDs>

## Confirmed facts
<what is established and how>

## Assumptions
<what is assumed pending verification>

## Conflicting documentation
<contradictions found, with sources>

## Local reproduction steps
<exact steps to reproduce>

## Observed results
<what actually happened>

## Remaining uncertainty
<what is still unknown or fragile>

## Impact on architecture and tests
<what this changes; required tests>
```

## Index

- [2026-06-24: Named-pipe IPC security on .NET 10](2026-06-24-named-pipe-ipc-security.md)
- [2026-06-24: Appx/provisioned-package debloat and its reversibility](2026-06-24-appx-debloat-and-reversibility.md)
- [2026-06-24: Privacy, policy, and Copilot controls](2026-06-24-privacy-policy-and-copilot-controls.md)
- [2026-06-24: Firewall rule review and Windows 11 UX restorations](2026-06-24-firewall-review-and-win11-ux.md)

These records are research groundwork for the de-cloud/debloat policy pack (Milestone 5). They
do not authorize implementation; each claim must still meet the verification bar above (current
official docs + a disposable-VM reproduction) before code is written.
