#requires -Version 5.1
<#
.SYNOPSIS
    Aggregate Milestone 0 verification: format check, build, and unit tests.

.DESCRIPTION
    Runs the full non-privileged verification used by the Milestone 0 gate and CI:
      1. dotnet format --verify-no-changes (formatting/style check)
      2. dotnet build (warnings-as-errors in production projects)
      3. unit tests
    Performs no privileged action and changes no system state.

.PARAMETER Configuration
    The build configuration. Defaults to Release.

.PARAMETER SkipFormat
    Skip the formatting verification step.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipFormat
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

$solution = Join-Path $repoRoot 'Sovereign.slnx'

if (-not $SkipFormat) {
    Write-Host 'Verifying formatting (dotnet format --verify-no-changes)...' -ForegroundColor Cyan
    dotnet format $solution --verify-no-changes
    if ($LASTEXITCODE -ne 0) {
        throw "Formatting verification failed. Run 'dotnet format $solution' to fix."
    }
    Write-Host 'Formatting OK.' -ForegroundColor Green
}

Write-Host ("Building ({0})..." -f $Configuration) -ForegroundColor Cyan
dotnet build $solution -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
Write-Host 'Build OK.' -ForegroundColor Green

Write-Host ("Running unit tests ({0})..." -f $Configuration) -ForegroundColor Cyan
dotnet test (Join-Path $repoRoot 'tests\Sovereign.UnitTests\Sovereign.UnitTests.csproj') -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Unit tests failed with exit code $LASTEXITCODE." }
Write-Host 'Unit tests OK.' -ForegroundColor Green

Write-Host 'Milestone 0 verification succeeded.' -ForegroundColor Green
