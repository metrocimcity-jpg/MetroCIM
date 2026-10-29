#Requires -Version 5.1
param(
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$AddinsRoot = Join-Path $Root "Addins"

function Test-HostRunning {
    @(Get-Process -Name "Revit" -ErrorAction SilentlyContinue).Count -gt 0
}

if (Test-HostRunning) {
    throw "Close Revit before installing MetroCIM."
}

$years = Get-ChildItem -Path $AddinsRoot -Directory -ErrorAction Stop
if ($years.Count -eq 0) {
    throw "No Addins\<year> folders found next to Install.ps1."
}

foreach ($yearDir in $years) {
    $year = $yearDir.Name
    $dest = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$year"
    $destPlugin = Join-Path $dest "MetroCIM"
    New-Item -ItemType Directory -Force -Path $destPlugin | Out-Null

    $addin = Join-Path $yearDir.FullName "MetroCIM.addin"
    if (-not (Test-Path $addin)) {
        throw "Missing $addin"
    }

    Copy-Item -Path $addin -Destination $dest -Force
    Copy-Item -Path (Join-Path $yearDir.FullName "MetroCIM\*") -Destination $destPlugin -Recurse -Force

    # Remove legacy name if present
    $legacy = Join-Path $dest "RevitXKT.addin"
    if (Test-Path $legacy) {
        Remove-Item $legacy -Force
    }

    if (-not $Quiet) {
        Write-Host "Installed Revit $year -> $dest"
    }
}

if (-not $Quiet) {
    Write-Host ""
    Write-Host "MetroCIM for Revit installed. Restart Revit and open the MetroCIM ribbon tab."
}
