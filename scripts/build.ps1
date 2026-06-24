#requires -Version 5.1
<#
.SYNOPSIS
    Builds the managed Sovereign solution.

.DESCRIPTION
    Builds Sovereign.slnx with warnings treated as errors in production projects. Performs no
    privileged action and changes no system state.

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

Write-Host ("Building Sovereign.slnx ({0})..." -f $Configuration) -ForegroundColor Cyan
dotnet build "$repoRoot\Sovereign.slnx" -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }

Write-Host 'Build succeeded.' -ForegroundColor Green
