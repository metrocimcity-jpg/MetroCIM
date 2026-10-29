#Requires -Version 5.1
param(
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

function Test-HostRunning {
    @(Get-Process -Name "Revit" -ErrorAction SilentlyContinue).Count -gt 0
}

if (Test-HostRunning) {
    throw "Close Revit before uninstalling MetroCIM."
}

$years = @("2025", "2026", "2027")
foreach ($year in $years) {
    $dest = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$year"
    $addin = Join-Path $dest "MetroCIM.addin"
    $plugin = Join-Path $dest "MetroCIM"

    if (Test-Path $addin) {
        Remove-Item $addin -Force
        if (-not $Quiet) { Write-Host "Removed $addin" }
    }

    if (Test-Path $plugin) {
        Remove-Item $plugin -Recurse -Force
        if (-not $Quiet) { Write-Host "Removed $plugin" }
    }
}

if (-not $Quiet) {
    Write-Host ""
    Write-Host "MetroCIM for Revit uninstalled."
}
