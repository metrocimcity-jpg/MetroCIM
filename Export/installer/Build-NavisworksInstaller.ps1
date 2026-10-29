#Requires -Version 5.1
<#
.SYNOPSIS
  Builds MetroCIM for Navisworks 2025–2027 and packs a per-user NSIS .exe installer (no admin).
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
$Project = Join-Path $RepoRoot "Export\Navisworks\MetroCIM.Navisworks.csproj"
$Nsi = Join-Path $PSScriptRoot "navisworks\MetroCIM-Navisworks.nsi"
$Staging = Join-Path $RepoRoot "artifacts\staging\MetroCIM-Navisworks"
$BundleStage = Join-Path $Staging "MetroCIM.Navisworks.bundle"
$Dist = Join-Path $RepoRoot "dist"
$ExePath = Join-Path $Dist "MetroCIM-Navisworks-$Version.exe"
$PackageContents = Join-Path $RepoRoot "Export\Navisworks\PackageContents.xml"

Write-Host "Building Navisworks NSIS installer ($Configuration) years: $($Years -join ', ')"
Write-Host "makensis: $Makensis"

if (Test-Path $Staging) {
    Remove-Item $Staging -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $Staging | Out-Null
New-Item -ItemType Directory -Force -Path $Dist | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $BundleStage "Contents") | Out-Null
Copy-Item $PackageContents (Join-Path $BundleStage "PackageContents.xml") -Force

foreach ($year in $Years) {
    Write-Host "  Building Navisworks $year..."
    & dotnet build $Project -c $Configuration -p:NavisworksVersion=$year --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for Navisworks $year (exit $LASTEXITCODE)."
    }

    $out = Join-Path $RepoRoot "Export\Navisworks\bin\$year\$Configuration"
    if (-not (Test-Path (Join-Path $out "MetroCIM.Navisworks.dll"))) {
        throw "Missing MetroCIM.Navisworks.dll under $out"
    }

    $yearStage = Join-Path $BundleStage "Contents\$year"
    New-Item -ItemType Directory -Force -Path $yearStage | Out-Null

    Get-ChildItem $out -File | Where-Object {
        $_.Extension -in ".dll", ".pdb", ".xaml"
    } | ForEach-Object {
        Copy-Item $_.FullName $yearStage -Force
    }

    $ribbon = Join-Path $out "Ribbon"
    if (Test-Path $ribbon) {
        Copy-Item $ribbon $yearStage -Recurse -Force
    }
}

if (Test-Path $ExePath) {
    Remove-Item $ExePath -Force
}

# Relative paths from repo root avoid NSIS /D drive-letter colon issues.
$stagingRel = "artifacts\staging\MetroCIM-Navisworks"
$outRel = "dist\MetroCIM-Navisworks-$Version.exe"

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
Write-Host "Navisworks installer ready (per-user, no admin):"
Write-Host "  $ExePath"
