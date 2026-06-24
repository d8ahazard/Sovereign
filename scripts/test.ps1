#requires -Version 5.1
<#
.SYNOPSIS
    Runs the non-privileged unit test suite (the Milestone 0 gate test set).

.DESCRIPTION
    Runs Sovereign.UnitTests only. These tests require no network, no elevation, and no system
    state. The other test tiers (integration, system, security, failure-injection) require
    service/IPC, lab VMs, or fault injection and are intentionally not run here; they are
    scaffolds until their corresponding milestones.

.PARAMETER Configuration
    The build configuration. Defaults to Release.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

$unitTests = Join-Path $repoRoot 'tests\Sovereign.UnitTests\Sovereign.UnitTests.csproj'

Write-Host ("Running unit tests ({0})..." -f $Configuration) -ForegroundColor Cyan
dotnet test $unitTests -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Unit tests failed with exit code $LASTEXITCODE." }

Write-Host 'Unit tests passed.' -ForegroundColor Green
