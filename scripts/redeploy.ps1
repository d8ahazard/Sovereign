#requires -Version 5.1
<#
.SYNOPSIS
    Rebuilds and redeploys the running Sovereign service.

.DESCRIPTION
    Stops the Sovereign service (so its binaries unlock), rebuilds the solution in the requested
    configuration, and starts the service again. Requires an elevated PowerShell session. Writes a
    transcript to artifacts\redeploy.log so the result can be inspected.

.PARAMETER Configuration
    The build configuration. Defaults to Release (the configuration install-service.ps1 points at).
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$logDir = Join-Path $repoRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
Start-Transcript -Path (Join-Path $logDir 'redeploy.log') -Force | Out-Null

try {
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'

    $svc = Get-Service Sovereign -ErrorAction SilentlyContinue
    if ($svc -and $svc.Status -ne 'Stopped') {
        Write-Host 'Stopping Sovereign service...'
        Stop-Service Sovereign -Force
        (Get-Service Sovereign).WaitForStatus('Stopped', '00:00:30')
    }

    Write-Host "Building Sovereign.slnx ($Configuration)..."
    dotnet build "$repoRoot\Sovereign.slnx" -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }

    if ($svc) {
        Write-Host 'Starting Sovereign service...'
        Start-Service Sovereign
        (Get-Service Sovereign).WaitForStatus('Running', '00:00:30')
        Write-Host 'Service is Running.'
    } else {
        Write-Host 'Service not installed; build only.'
    }

    Write-Host 'REDEPLOY_OK'
}
finally {
    Stop-Transcript | Out-Null
}
