#requires -Version 5.1
<#
.SYNOPSIS
    Uninstalls the Sovereign Windows service (Milestone 1).

.DESCRIPTION
    Stops (if running) and removes the Sovereign Windows service. This fully reverses
    install-service.ps1. It does not delete the local event-store database; pass -PurgeData to also
    remove the data directory under ProgramData.

    Requires an elevated (Administrator) PowerShell session.

.PARAMETER ServiceName
    The Windows service name. Defaults to 'Sovereign'.

.PARAMETER PurgeData
    Also delete the local data directory (event store) under %ProgramData%\Sovereign.

.EXAMPLE
    .\scripts\uninstall-service.ps1
#>
[CmdletBinding()]
param(
    [string] $ServiceName = 'Sovereign',
    [switch] $PurgeData
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
}

if (-not (Test-Admin)) {
    Write-Error 'This script must be run from an elevated (Administrator) PowerShell session.'
    exit 1
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existing) {
    Write-Host "Service '$ServiceName' is not installed. Nothing to do." -ForegroundColor Yellow
}
else {
    if ($existing.Status -ne 'Stopped') {
        Write-Host "Stopping service '$ServiceName'" -ForegroundColor Cyan
        Stop-Service -Name $ServiceName -Force
    }

    Write-Host "Removing service '$ServiceName'" -ForegroundColor Cyan
    Remove-Service -Name $ServiceName
    Write-Host "Service '$ServiceName' removed." -ForegroundColor Green
}

if ($PurgeData) {
    $dataDir = Join-Path $env:ProgramData 'Sovereign'
    if (Test-Path -LiteralPath $dataDir) {
        Write-Host "Removing data directory '$dataDir'" -ForegroundColor Cyan
        Remove-Item -LiteralPath $dataDir -Recurse -Force
        Write-Host 'Data directory removed.' -ForegroundColor Green
    }
    else {
        Write-Host "No data directory at '$dataDir'." -ForegroundColor Yellow
    }
}

exit 0
