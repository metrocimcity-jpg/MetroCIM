# MetroCIM — Revit + Navisworks

## Goal
Export **visible 3D geometry** to **glTF**, **XKT**, and **IFC** with on-screen colors.

## Hosts
- **Revit 2025 / 2026 / 2027** — active 3D view; IFC via Revit IFC setups + STEP color patch
- **Navisworks Manage/Simulate 2025 / 2026 / 2027** — visible model items; IFC via MetroCIM IFC4 mesh writer

## Stack
- Shared `MetroCIM.Core` (`netstandard2.0`): `ResolvedAppearance`, `TriangleMesh`, `GltfSceneAccumulator`, `XktConverter`, `IfcMeshExporter`, `IfcGuid`
- Revit: Nice3point Revit API (`net8` / `net10`), `.addin` deploy
- Navisworks: Speckle.Navisworks.API (`net48`), ApplicationPlugins `.bundle`
- glTF via **SharpGLTF.Toolkit**
- XKT via **xeokit-convert** (`npm install -g @xeokit/xeokit-convert`)

## Ribbon (both hosts)
Tab **MetroCIM**, panel **Export**:
- **Export glTF**
- **Export XKT**
- **Export IFC**

## Non-goals
- No import
- No native C# XKT writer
- No Revit IFC setup picker inside Navisworks
- No Navisworks Freedom/Viewer plugins
