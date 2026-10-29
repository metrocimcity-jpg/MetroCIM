#Requires -Version 5.1
<#
.SYNOPSIS
  Builds MetroCIM for Revit 2025–2027 and packs a per-user NSIS .exe installer (no admin).
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0",
    [string[]]$Years = @("2025", "2026", "2027")
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Resolve-Makensis.ps1")
$Makensis = Resolve-Makensis

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$Project = Join-Path $RepoRoot "Export\Revit\MetroCIM.csproj"
$Nsi = Join-Path $PSScriptRoot "revit\MetroCIM-Revit.nsi"
$Staging = Join-Path $RepoRoot "artifacts\staging\MetroCIM-Revit"
$Dist = Join-Path $RepoRoot "dist"
$ExePath = Join-Path $Dist "MetroCIM-Revit-$Version.exe"

Write-Host "Building Revit NSIS installer ($Configuration) years: $($Years -join ', ')"
Write-Host "makensis: $Makensis"

if (Test-Path $Staging) {
    Remove-Item $Staging -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $Staging | Out-Null
New-Item -ItemType Directory -Force -Path $Dist | Out-Null

foreach ($year in $Years) {
    Write-Host "  Building Revit $year..."
    & dotnet build $Project -c $Configuration -p:RevitVersion=$year --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for Revit $year (exit $LASTEXITCODE)."
    }

    $out = Join-Path $RepoRoot "Export\Revit\bin\$year\$Configuration"
    if (-not (Test-Path (Join-Path $out "MetroCIM.dll"))) {
        throw "Missing MetroCIM.dll under $out"
    }

    $yearStage = Join-Path $Staging "Addins\$year"
    $pluginStage = Join-Path $yearStage "MetroCIM"
    New-Item -ItemType Directory -Force -Path $pluginStage | Out-Null

    Copy-Item (Join-Path $out "MetroCIM.addin") $yearStage -Force
    Get-ChildItem $out -File | Where-Object {
        $_.Extension -in ".dll", ".pdb", ".json" -and $_.Name -ne "MetroCIM.addin"
    } | ForEach-Object {
        Copy-Item $_.FullName $pluginStage -Force
    }
}

if (Test-Path $ExePath) {
    Remove-Item $ExePath -Force
}

# Relative paths from repo root avoid NSIS /D drive-letter colon issues.
$stagingRel = "artifacts\staging\MetroCIM-Revit"
$outRel = "dist\MetroCIM-Revit-$Version.exe"

Push-Location $RepoRoot
try {
    & $Makensis /NOCD `
        "/DPRODUCT_VERSION=$Version" `
        "/DSTAGING_DIR=$stagingRel" `
        "/DOUT_FILE=$outRel" `
        $Nsi
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $ExePath)) {
        throw "NSIS failed to produce $ExePath"
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Revit installer ready (per-user, no admin):"
Write-Host "  $ExePath"
