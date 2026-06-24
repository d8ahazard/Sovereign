#requires -Version 5.1
<#
.SYNOPSIS
    Verifies Milestone 0 prerequisites and restores NuGet packages.

.DESCRIPTION
    Checks that the required .NET SDK (per global.json) and git are available, then runs a
    restore of the managed solution. This script performs no privileged action, changes no
    system state, and contacts the network only to restore NuGet packages (the standard,
    documented one-time restore step). It does NOT touch the firewall, registry, or services.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

Write-Host 'Sovereign bootstrap (Milestone 0)' -ForegroundColor Cyan

# --- git ---
$git = Get-Command git -ErrorAction SilentlyContinue
if (-not $git) {
    throw 'git was not found on PATH. Install Git for Windows and re-run.'
}
Write-Host ("  git:    {0}" -f (git --version))

# --- .NET SDK ---
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw 'dotnet was not found on PATH. Install the .NET 10 SDK (winget install --exact --id Microsoft.DotNet.SDK.10).'
}
$sdkVersion = (dotnet --version)
Write-Host ("  dotnet: {0}" -f $sdkVersion)
if ($sdkVersion -notlike '10.*') {
    throw "The .NET 10 SDK is required (global.json pins 10.0.x). Active SDK is $sdkVersion."
}

# --- restore ---
Write-Host 'Restoring packages...' -ForegroundColor Cyan
dotnet restore "$repoRoot\Sovereign.slnx"
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE." }

Write-Host 'Bootstrap complete.' -ForegroundColor Green
