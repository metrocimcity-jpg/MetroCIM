# MetroCIM — Revit + Navisworks

## Goal
Export **visible 3D geometry** to **glTF**, **XKT**, and **IFC** with on-screen colors, and export **visible instance parameters** to **Excel** (`.xlsx`).

## Hosts
- **Revit 2025 / 2026 / 2027** — active 3D view; IFC via Revit IFC setups + STEP color patch
- **Navisworks Manage/Simulate 2025 / 2026 / 2027** — visible model items; IFC via MetroCIM IFC4 mesh writer

## Repo layout
Product root is **`Export/`** (code, docs, installers, rules). Git root keeps a short README and `.cursor/rules/00-export-home.mdc` that points here.

```text
Export/
  Core/  Revit/  Navisworks/  Tests/  installer/  docs/  rules/
  README.md  MetroCIM.slnx  Directory.Build.props  LICENSE  global.json
```

## Stack
- Shared `MetroCIM.Core` (`netstandard2.0`): `ResolvedAppearance`, `TriangleMesh`, `GltfSceneAccumulator`, `XktConverter`, `IfcMeshExporter`, `IfcGuid`, `ParameterTable`, `ExcelTableWriter`
- Revit: Nice3point Revit API (`net8` / `net10`), `.addin` deploy
- Navisworks: Speckle.Navisworks.API (`net48`), ApplicationPlugins `.bundle`
- glTF via **SharpGLTF.Toolkit**
- XKT via **xeokit-convert** (`npm install -g @xeokit/xeokit-convert`)
- Excel via **DocumentFormat.OpenXml** (+ `System.IO.Packaging` for net48)

## Ribbon (both hosts)
Tab **MetroCIM**, panel **Export** — **four** commands:
- **Export glTF**
- **Export XKT**
- **Export IFC**
- **Export Excel** — visible instances → `.xlsx` with Revit Id, IFC GUID, and all instance/type (or NW property) values

## Coordinates
- **Local location** / **Shared Coordination** prompt before glTF and XKT (Revit) and before all three Navisworks mesh exports
- Revit Local = Project Base Point; Shared = Shared Coordinates
- Navisworks Local = primary model origin; Shared = world / federated
- **Revit Export IFC** does not use this prompt — site/coords stay in the selected Revit IFC setup
- **Export Excel** does not use this prompt (tabular data only)

## Install
- Separate per-user NSIS EXEs (no admin): `Export/installer/Build-RevitInstaller.ps1`, `Export/installer/Build-NavisworksInstaller.ps1` → `dist/`
- Developer deploy: `dotnet build … -p:DeployAddin=true` (and `-p:RevitVersion=` / `-p:NavisworksVersion=`)

## Non-goals
- No import
- No native C# XKT writer
- No Revit IFC setup picker inside Navisworks
- No Navisworks Freedom/Viewer plugins
- No MetroCIM override of Revit IFC setup coordinate options
