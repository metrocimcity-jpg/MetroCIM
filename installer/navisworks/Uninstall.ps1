#Requires -Version 5.1
param(
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"
$BundleDest = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\MetroCIM.Navisworks.bundle"

function Test-HostRunning {
    @(Get-Process -Name "Roamer" -ErrorAction SilentlyContinue).Count -gt 0
}

if (Test-HostRunning) {
    throw "Close Navisworks before uninstalling MetroCIM."
}

if (Test-Path $BundleDest) {
    Remove-Item $BundleDest -Recurse -Force
    if (-not $Quiet) {
        Write-Host "Removed $BundleDest"
    }
}
elseif (-not $Quiet) {
    Write-Host "MetroCIM Navisworks bundle was not installed."
}

if (-not $Quiet) {
    Write-Host ""
    Write-Host "MetroCIM for Navisworks uninstalled."
}
