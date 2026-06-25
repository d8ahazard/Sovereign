#requires -Version 5.1
<#
.SYNOPSIS
    Builds the managed Sovereign solution.

.DESCRIPTION
    Builds Sovereign.slnx with warnings treated as errors in production projects. Performs no
    privileged action and changes no system state.

    The WinUI 3 UI (Sovereign.UI) is intentionally excluded from the default solution build so the
    gate stays fast and reliable (ADR 0003). Pass -Full to also build it (requires a runtime
    identifier; defaults to win-x64).

.PARAMETER Configuration
    The build configuration. Defaults to Release.

.PARAMETER Full
    Also build the self-contained WinUI 3 UI shell.

.PARAMETER RuntimeIdentifier
    The runtime identifier for the UI build when -Full is set. Defaults to win-x64.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Full,
    [string]$RuntimeIdentifier = 'win-x64'
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

if ($Full) {
    Write-Host ("Building Sovereign.UI ({0}, {1})..." -f $Configuration, $RuntimeIdentifier) -ForegroundColor Cyan
    dotnet build "$repoRoot\src\Sovereign.UI\Sovereign.UI.csproj" -c $Configuration -r $RuntimeIdentifier
    if ($LASTEXITCODE -ne 0) { throw "UI build failed with exit code $LASTEXITCODE." }
}

Write-Host 'Build succeeded.' -ForegroundColor Green
