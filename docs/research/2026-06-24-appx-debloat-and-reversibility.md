# 2026-06-24: Appx/provisioned-package debloat and its reversibility

## Question

How does Sovereign enumerate, remove, and **restore** preinstalled Windows apps (Store/Appx and
provisioned packages) reliably and reversibly, without inventing package names or commands?

## Target Windows editions and builds

- Windows 11, version 24H2 (build 26100) primary; 23H2 and the 25H2 servicing line noted where
  behavior differs. Editions: Home/Pro vs Enterprise/Education (policy-based removal differs).

## Primary sources

- Overview of apps on Windows client devices - https://learn.microsoft.com/en-us/windows/application-management/overview-windows-apps (accessed 2026-06-24)
- Get-AppxProvisionedPackage (DISM) - https://learn.microsoft.com/en-us/powershell/module/dism/get-appxprovisionedpackage (accessed 2026-06-24)
- Remove-AppxProvisionedPackage (DISM) - https://learn.microsoft.com/en-us/powershell/module/dism/remove-appxprovisionedpackage (accessed 2026-06-24)
- Add-AppxProvisionedPackage (DISM) - https://learn.microsoft.com/en-us/powershell/module/dism/add-appxprovisionedpackage (accessed 2026-06-24)
- Preinstall apps using DISM - https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/preinstall-apps-using-dism (accessed 2026-06-24)
- PackageManager.ProvisionPackageForAllUsersAsync - https://learn.microsoft.com/en-us/uwp/api/windows.management.deployment.packagemanager.provisionpackageforallusersasync (accessed 2026-06-24)
- Policy-based removal of pre-installed Microsoft Store apps (Windows IT Pro Blog) - https://techcommunity.microsoft.com/blog/windows-itpro-blog/policy-based-removal-of-pre-installed-microsoft-store-apps/4463835 (accessed 2026-06-24)
- Inbox Microsoft Store apps update in Windows media (Windows IT Pro Blog) - https://techcommunity.microsoft.com/blog/windows-itpro-blog/inbox-microsoft-store-apps-update-in-windows-media/4433131 (accessed 2026-06-24)

Tool surveys (for catalog coverage only, NOT as authority for mechanism): Raphire/Win11Debloat
(MIT), ChrisTitusTech/winutil (MIT).

## Relevant identifiers, APIs, and commands

There are two distinct scopes; conflating them is a common bug.

- **Provisioned package** (machine image): installs for *new* user profiles.
  - Enumerate: `Get-AppxProvisionedPackage -Online | Format-Table DisplayName, PackageName`
  - Remove: `Remove-AppxProvisionedPackage -Online -PackageName <PackageName>`
  - Restore: `Add-AppxProvisionedPackage -Online -PackagePath <.appx/.msix> -DependencyPackagePath <...> -LicensePath <license.xml>`
  - WinRT equivalent: `PackageManager.ProvisionPackageForAllUsersAsync(...)` (admin; package must be staged on system volume; a re-provision is a "clean" reprovision).
- **Installed package** (per user): the app registered into a user's profile.
  - Enumerate: `Get-AppxPackage [-AllUsers]`
  - Remove (current user): `Remove-AppxPackage <PackageFullName>`
  - Restore: `Add-AppxPackage` (register staged package) or reinstall from the Microsoft Store.
- **Enterprise/Education policy (25H2+):** "Remove default Microsoft Store packages from the
  system" under `Administrative Templates\Windows Components\App Package Deployment`; CSP
  `RemoveDefaultMicrosoftStorePackages`; registry under
  `HKLM\SOFTWARE\Policies\Microsoft\Windows\Appx\RemoveDefaultMicrosoftStorePackages`. Off by
  default; selects from a defined list. Not available on Home/Pro.

## Confirmed facts

- `Remove-AppxProvisionedPackage` stops installation for *new* users but does **not** remove the
  app from existing user accounts. Per-user removal requires `Remove-AppxPackage`. So fully
  removing an app for a single-user machine generally needs **both** operations.
- Removing a provisioned package removes the package, its license, and custom data files from the
  image. Microsoft's own restore guidance requires re-adding with the original package +
  dependencies + license file.
- A "clean" reprovision via `ProvisionPackageForAllUsersAsync` re-offers the app to users who had
  removed it but does not affect users who currently have it installed.
- Windows 11 24H2 media ships ~36 inbox apps (full list captured in the debloat catalog).

## Assumptions (must verify in a disposable VM before implementation)

- Exact `PackageName`/`PackageFamilyName` strings drift across builds and must be read live from
  the target machine, never hard-coded. The catalog stores *family-name patterns* and friendly
  identities, resolved at runtime.
- Some packages (e.g. certain platform/runtime packages, `MicrosoftWindows.Client.*`) may be
  protected, non-removable, or required by the shell; removal may fail or be reverted by servicing.
- Store reinstall availability varies; a few removed packages are not offered in the Store and can
  only be restored from retained original files or a Windows feature/servicing operation.

## Conflicting documentation

- Community tools claim "almost all apps can be reinstalled from the Store," while Microsoft's DISM
  guidance implies offline restore needs the original package files. Both are true for different
  packages; Sovereign must not assume Store reinstall is universally available.

## Local reproduction steps (planned, Milestone 5)

1. In a disposable 24H2 VM, snapshot. `Get-AppxProvisionedPackage -Online` and `Get-AppxPackage
   -AllUsers`; export to JSON as the baseline.
2. For a low-risk target (e.g. Solitaire), capture the provisioned package files/license if
   retrievable, then `Remove-AppxProvisionedPackage` + `Remove-AppxPackage`.
3. Verify removal (enumerate again; launch attempt fails).
4. Restore via the recorded path (Store reinstall and/or `Add-AppxProvisionedPackage`).
5. Verify restore and compare against the baseline snapshot.

## Observed results

- Not yet executed. To be recorded here when Milestone 5 lab runs occur.

## Remaining uncertainty

- Which specific packages are non-restorable from the Store on 24H2/25H2.
- Whether Sovereign can/should retain a local copy of original package files for guaranteed
  offline restore (storage + licensing implications). Candidate for an ADR.

## Impact on architecture and tests

- The Appx policy must perform a **two-scope** operation (provisioned + per-user) and record both
  for rollback. See `docs/reversibility.md`.
- Removal is `RequiresUserAction`/`Applied` only after independent verification; servicing can
  re-provision packages, which is exactly the drift Sovereign must detect (Milestones 5-6).
- Tests: integration tests for enumerate/remove/restore against a VM; failure-injection for
  protected/non-removable packages; drift tests after a simulated cumulative update.
