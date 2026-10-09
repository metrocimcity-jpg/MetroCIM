#Requires -Version 5.1
param(
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$BundleSource = Join-Path $Root "MetroCIM.Navisworks.bundle"
$BundleDest = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\MetroCIM.Navisworks.bundle"

function Test-HostRunning {
    @(Get-Process -Name "Roamer" -ErrorAction SilentlyContinue).Count -gt 0
}

if (Test-HostRunning) {
    throw "Close Navisworks before installing MetroCIM."
}

if (-not (Test-Path (Join-Path $BundleSource "PackageContents.xml"))) {
    throw "Missing PackageContents.xml under MetroCIM.Navisworks.bundle."
}

if (Test-Path $BundleDest) {
    Remove-Item $BundleDest -Recurse -Force
}

New-Item -ItemType Directory -Force -Path (Split-Path $BundleDest -Parent) | Out-Null
Copy-Item -Path $BundleSource -Destination $BundleDest -Recurse -Force

if (-not $Quiet) {
    Write-Host "Installed -> $BundleDest"
    Write-Host ""
    Write-Host "MetroCIM for Navisworks installed. Restart Manage/Simulate and open the MetroCIM ribbon tab."
}
