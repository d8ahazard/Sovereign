#requires -Version 5.1
<#
.SYNOPSIS
    Aggregate verification: format check, build, and the non-privileged test tiers.

.DESCRIPTION
    Runs the full non-privileged verification used by the milestone gates and CI:
      1. dotnet format --verify-no-changes (formatting/style check)
      2. dotnet build (warnings-as-errors in production projects)
      3. unit, integration, and security tests (in-process; no elevation required)
    Performs no privileged action and changes no system state. Cross-user pipe-ACL denial is
    validated separately by a VM/system test (see docs/test-strategy.md).

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

$testProjects = @(
    'tests\Sovereign.UnitTests\Sovereign.UnitTests.csproj',
    'tests\Sovereign.IntegrationTests\Sovereign.IntegrationTests.csproj',
    'tests\Sovereign.SecurityTests\Sovereign.SecurityTests.csproj'
)

foreach ($project in $testProjects) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($project)
    Write-Host ("Running {0} ({1})..." -f $name, $Configuration) -ForegroundColor Cyan
    dotnet test (Join-Path $repoRoot $project) -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw "$name failed with exit code $LASTEXITCODE." }
    Write-Host ("{0} OK." -f $name) -ForegroundColor Green
}

Write-Host 'Verification succeeded.' -ForegroundColor Green
