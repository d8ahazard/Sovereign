#requires -Version 5.1
<#
.SYNOPSIS
    Installs the Sovereign Windows service (Milestone 1).

.DESCRIPTION
    Registers Sovereign.Service as a Windows service running as LocalSystem so it can create the
    ACL'd IPC pipe and manage local state. This is a reversible operation: it refuses to clobber an
    existing service, records nothing destructive, and is fully undone by uninstall-service.ps1.

    The service performs NO network, registry, or policy enforcement in Milestone 1. It hosts the
    local event store and the authenticated named-pipe IPC endpoint only.

    Requires an elevated (Administrator) PowerShell session.

.PARAMETER BinaryPath
    Full path to Sovereign.Service.exe. Defaults to the Release build output in this repo.

.PARAMETER ServiceName
    The Windows service name. Defaults to 'Sovereign'.

.PARAMETER StartupType
    Service start type. Defaults to 'Manual' so installation alone changes nothing at boot.

.EXAMPLE
    # Run an elevated PowerShell, then:
    .\scripts\install-service.ps1

.NOTES
    To run the service in the foreground for development without installing it (no elevation of a
    service, but the console must be able to create the pipe), use:
        dotnet run --project src\Sovereign.Service
#>
[CmdletBinding()]
param(
    [string] $BinaryPath,
    [string] $ServiceName = 'Sovereign',
    [ValidateSet('Manual', 'Automatic', 'Disabled')]
    [string] $StartupType = 'Manual'
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

if (-not $BinaryPath) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $BinaryPath = Join-Path $repoRoot 'src\Sovereign.Service\bin\Release\net10.0-windows\Sovereign.Service.exe'
}

if (-not (Test-Path -LiteralPath $BinaryPath)) {
    Write-Error "Service binary not found at '$BinaryPath'. Build first: scripts\build.ps1 -Configuration Release"
    exit 1
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Service '$ServiceName' already exists. Not modifying it." -ForegroundColor Yellow
    Write-Host 'To reinstall, run scripts\uninstall-service.ps1 first.'
    exit 0
}

Write-Host "Installing service '$ServiceName'" -ForegroundColor Cyan
Write-Host "  Binary : $BinaryPath"
Write-Host "  Start  : $StartupType (installation alone does not start it)"

New-Service -Name $ServiceName `
    -BinaryPathName "`"$BinaryPath`"" `
    -DisplayName 'Sovereign' `
    -Description 'Sovereign local control plane (Milestone 1: local IPC + event store only).' `
    -StartupType $StartupType | Out-Null

Write-Host "Service '$ServiceName' installed." -ForegroundColor Green
Write-Host "Start it with:  Start-Service $ServiceName"
Write-Host "Verify with:    sov status"
Write-Host "Remove it with: scripts\uninstall-service.ps1"
exit 0
