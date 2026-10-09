#Requires -Version 5.1
function Resolve-Makensis {
    $candidates = @(
        (Get-Command makensis.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
        "${env:ProgramFiles(x86)}\NSIS\makensis.exe",
        "$env:ProgramFiles\NSIS\makensis.exe",
        "$env:LOCALAPPDATA\Programs\NSIS\makensis.exe"
    ) | Where-Object { $_ -and (Test-Path $_) }

    $found = $candidates | Select-Object -First 1
    if (-not $found) {
        throw @"
NSIS (makensis.exe) was not found. Install Nullsoft Scriptable Install System, then rebuild:

  winget install --id NSIS.NSIS -e

Or download from https://nsis.sourceforge.io/Download
"@
    }

    return $found
}
