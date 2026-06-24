# 2026-06-24: Privacy, policy, and Copilot controls

## Question

What are the authoritative, reversible mechanisms (policy/registry/commands) for disabling
Windows telemetry, consumer/cloud content, advertising, Spotlight, and AI (Copilot/Recall),
and what original state must Sovereign capture to roll each back?

## Target Windows editions and builds

- Windows 11 24H2 (26100) primary. Several policies are **Enterprise/Education-scoped** and are
  reported as `Unsupported` on Home/Pro rather than guessed.

## Primary sources

- Manage connections from Windows components to Microsoft services (master privacy reference) - https://learn.microsoft.com/en-us/windows/privacy/manage-connections-from-windows-operating-system-components-to-microsoft-services (accessed 2026-06-24)
- Policy CSP - System (AllowTelemetry) - https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-system (accessed 2026-06-24)
- Policy CSP - Experience (AllowWindowsConsumerFeatures / DisableWindowsConsumerFeatures, DisableSoftLanding) - https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-experience (accessed 2026-06-24)
- Updated Windows and Microsoft 365 Copilot Chat experience (Copilot management / AppLocker) - https://learn.microsoft.com/en-us/windows/client-management/manage-windows-copilot (accessed 2026-06-24)
- Using AppLocker to create custom Intune policies (Application Identity service requirement) - https://techcommunity.microsoft.com/t5/intune-customer-success/support-tip-using-applocker-to-create-custom-intune-policies-for/ba-p/364981 (accessed 2026-06-24)

## Confirmed facts and identifiers

Registry policy values live under `HKLM\SOFTWARE\Policies\...` (machine) or `HKCU\SOFTWARE\Policies\...`
(user). Group Policy enforces and reapplies these, which is more reliable than one-off tweaks.

| Control | Registry | Value | GPO / ADMX | Notes |
|---------|----------|-------|------------|-------|
| Diagnostic data (telemetry) | `HKLM\Software\Policies\Microsoft\Windows\DataCollection` | `AllowTelemetry` (DWORD): 0=Security (Ent/Edu only), 1=Basic (default), 3=Full | Data Collection and Preview Builds\Allow Telemetry; `DataCollection.admx` | On Home/Pro, 0 behaves as 1. |
| Microsoft consumer experiences | `HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent` | `DisableWindowsConsumerFeatures`=1 | Cloud Content\Turn off Microsoft consumer experiences; `CloudContent.admx` | Enterprise/Education scope. Stops auto-installed promoted apps and Start suggestions. |
| Windows tips | `HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent` | `DisableSoftLanding`=1 | Cloud Content\Do not show Windows tips | |
| Windows Spotlight (Action Center / Settings) | `...\CloudContent` | `DisableWindowsSpotlightOnActionCenter`=1, `DisableWindowsSpotlightOnSettings`=1 | Cloud Content\* | Multiple Spotlight sub-policies exist; confirm each in VM. |
| Copilot (legacy, deprecated) | `HKLM`/`HKCU` `\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot` | `TurnOffWindowsCopilot`=1 | Windows Components\Windows Copilot | **Hides taskbar button only**; Microsoft has deprecated this. Do not rely on it. |
| Copilot (recommended) | n/a (AppLocker) | Packaged-app **deny** rule: Publisher `CN=MICROSOFT CORPORATION, O=MICROSOFT CORPORATION, L=REDMOND, S=WASHINGTON, C=US`, Package name `MICROSOFT.COPILOT`, version `*` | Local Security Policy / AppLocker; exportable XML | Prevents install and launch. Requires the **Application Identity** service (`AppIDSvc`) running. |

## Assumptions (verify in VM)

- Advertising ID, Activity history, Cloud clipboard, Location, Maps, Phone Link, OneDrive, and
  Recall each have their own policy/registry surface not fully enumerated here; each gets its own
  research record before implementation. Do not implement from memory.
- Exact applicability (which SKU honors which policy on 24H2 vs 25H2) must be confirmed per policy;
  unsupported combinations are reported as `Unsupported`, never silently skipped or guessed.

## Conflicting documentation

- Copilot guidance has churned: legacy GPO/CSP (`TurnOffWindowsCopilot`) still appears in tooling
  but is deprecated and only hides the button. AppLocker (or App Control for Business) is the
  current supported control. Sovereign should implement the AppLocker path and treat the legacy
  policy as cosmetic only.

## Local reproduction steps (planned)

1. Snapshot VM. Export current values of each target key (or record "absent").
2. Apply one policy; `gpupdate /force` where applicable; verify the effective behavior, not just
   the registry write.
3. Roll back by restoring the exact recorded prior value (or deleting a value that was absent).
4. Confirm the system returns to the snapshot baseline.

## Observed results

- Not yet executed. To be recorded here per policy during Milestones 5.

## Remaining uncertainty

- Whether some consumer-feature behaviors require both the policy and an Appx removal to fully
  suppress (e.g. promoted app reinstalls after updates). Expected to interact with drift repair.

## Impact on architecture and tests

- Every policy captures the prior value (including "value absent") before writing, enabling exact
  rollback. See `docs/reversibility.md`.
- AppLocker-based Copilot control adds a dependency on the Application Identity service; the policy
  must verify the service state and report `RequiresUserAction` if it is disabled, rather than
  silently failing open.
- `Unknown`/`Unsupported` results must never be reported as compliant (agent_start.md section 8).
