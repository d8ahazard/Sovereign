#requires -Version 5.1
<#
.SYNOPSIS
    Emergency network-restore tool (Milestone 0 stub).

.DESCRIPTION
    In later milestones this is the documented, local emergency-recovery mechanism that
    removes Sovereign's WFP enforcement so the machine returns to normal networking
    (agent_start.md sections 2.1 and 18 Milestone 3). Per the recovery invariants, it must
    not create a permanent bypass and every use must be auditable.

    In Milestone 0 NO enforcement exists, so this stub intentionally makes NO system change.
    It only reports that there is nothing to restore. It does not modify the firewall, WFP,
    registry, or services.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Write-Host 'Sovereign emergency network-restore (Milestone 0 stub)' -ForegroundColor Yellow
Write-Host 'No Sovereign network enforcement is installed in Milestone 0.'
Write-Host 'There is nothing to restore. No system changes were made.'
exit 0
