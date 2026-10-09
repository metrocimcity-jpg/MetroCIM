# MetroCIM

Export **visible 3D geometry** to **glTF**, **XKT**, and **IFC** with the colors you see on screen.

Hosts:

- **Autodesk Revit 2025 / 2026 / 2027**
- **Autodesk Navisworks Manage / Simulate 2025 / 2026 / 2027**

![Mechanical room export with piping system colors](docs/images/sample-mechanical-room.png)

*Sample export of a mechanical room. Pipes, fittings, and accessories keep system colors.*

## What it exports

Both hosts use a **MetroCIM** ribbon tab with an **Export** panel:

| Command | Output |
| --- | --- |
| **Export glTF** | `.glb` / `.gltf` |
| **Export XKT** | `.xkt` (sibling `.glb` + `metadata.json`, then xeokit-convert) |
| **Export IFC** | `.ifc` |

### Revit
- glTF / XKT: active **3D view** visibility and colors (filters, overrides, MEP system types)
- glTF / XKT: choose **Local location** (Project Base Point) or **Shared Coordination** before save
- IFC: selected Revit IFC setup, then MetroCIM color apply + STEP patch (coordinates come from the IFC setup, not the Local/Shared prompt)

### Navisworks
- glTF / XKT / IFC: all **visible** model items (not hidden)
- Choose **Local location** (primary model origin) or **Shared Coordination** (world / federated) before save
- Colors: fragment appearance, then color-like properties
- IFC: MetroCIM IFC4 mesh writer (`IfcTriangulatedFaceSet`) — Navisworks has no Revit IFC setup picker

## Requirements

- Revit **or** Navisworks Manage/Simulate for the matching year
- For **Export XKT** only:
  - [Node.js](https://nodejs.org/)
  - `npm install -g @xeokit/xeokit-convert`

## Installers (separate per-user EXE)

Nullsoft (NSIS) installers — **no administrator permission**. They write only under `%AppData%`.

Prerequisite on the build machine: [NSIS](https://nsis.sourceforge.io/Download) (`winget install --id NSIS.NSIS -e`).

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File installer\Build-RevitInstaller.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File installer\Build-NavisworksInstaller.ps1
```

Outputs:

- `dist\MetroCIM-Revit-1.0.0.exe` — quit Revit, run the EXE
- `dist\MetroCIM-Navisworks-1.0.0.exe` — quit Navisworks, run the EXE

Uninstall from Windows **Settings → Apps** (per-user entry) or run the uninstaller under `%AppData%\MetroCIM\...`.

### Developer deploy (optional)

Copy straight into AppData from a local build:

```bat
dotnet build src/MetroCIM/MetroCIM.csproj -p:DeployAddin=true
dotnet build src/MetroCIM/MetroCIM.csproj -p:RevitVersion=2026 -p:DeployAddin=true
dotnet build src/MetroCIM/MetroCIM.csproj -p:RevitVersion=2025 -p:DeployAddin=true

dotnet build src/MetroCIM.Navisworks/MetroCIM.Navisworks.csproj -p:DeployAddin=true
dotnet build src/MetroCIM.Navisworks/MetroCIM.Navisworks.csproj -p:NavisworksVersion=2026 -p:DeployAddin=true
dotnet build src/MetroCIM.Navisworks/MetroCIM.Navisworks.csproj -p:NavisworksVersion=2025 -p:DeployAddin=true
```

Install locations:

- Revit: `%AppData%\Autodesk\Revit\Addins\<year>\`
- Navisworks: `%AppData%\Autodesk\ApplicationPlugins\MetroCIM.Navisworks.bundle\`

## Build

```bat
dotnet build src/MetroCIM.Core/MetroCIM.Core.csproj
dotnet build src/MetroCIM/MetroCIM.csproj
dotnet build src/MetroCIM.Navisworks/MetroCIM.Navisworks.csproj
dotnet test src/MetroCIM.Tests/MetroCIM.Tests.csproj
```

| Host | TFM | API |
| --- | --- | --- |
| Shared Core | `netstandard2.0` | SharpGLTF + writers |
| Revit 2027 | `net10.0-windows` | Nice3point `2027.*` |
| Revit 2025/2026 | `net8.0-windows` | Nice3point year packages |
| Navisworks 2025–2027 | `net48` | Speckle.Navisworks.API |

## Repository

https://github.com/metrocimcity-jpg/MetroCIM
