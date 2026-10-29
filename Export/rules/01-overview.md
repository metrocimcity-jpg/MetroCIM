# MetroCIM — Revit + Navisworks

## Goal
Export **visible 3D geometry** to **glTF**, **XKT**, and **IFC** with on-screen colors.

## Hosts
- **Revit 2025 / 2026 / 2027** — active 3D view; IFC via Revit IFC setups + STEP color patch
- **Navisworks Manage/Simulate 2025 / 2026 / 2027** — visible model items; IFC via MetroCIM IFC4 mesh writer

## Layout
`Export/Core`, `Export/Revit`, `Export/Navisworks`, `Export/Tests`, `Export/installer`, `Export/docs`, `Export/rules`

## Stack
- Shared `MetroCIM.Core` (`netstandard2.0`): `ResolvedAppearance`, `TriangleMesh`, `GltfSceneAccumulator`, `XktConverter`, `IfcMeshExporter`, `IfcGuid`, `ExcelTableWriter`
- Revit: Nice3point Revit API (`net8` / `net10`), `.addin` deploy
- Navisworks: Speckle.Navisworks.API (`net48`), ApplicationPlugins `.bundle`
- glTF via **SharpGLTF.Toolkit**
- XKT via **xeokit-convert** (`npm install -g @xeokit/xeokit-convert`)
- Excel via **DocumentFormat.OpenXml**

## Ribbon (both hosts)
Tab **MetroCIM**, panel **Export**:
- **Export glTF**
- **Export XKT**
- **Export IFC**
- **Export Excel** — visible instances → `.xlsx` with Revit Id, IFC GUID, and all instance/type (or NW property) values

## Coordinates
- **Local location** / **Shared Coordination** prompt before glTF and XKT (Revit) and before all three Navisworks exports
- Revit Local = Project Base Point; Shared = Shared Coordinates
- Navisworks Local = primary model origin; Shared = world / federated
- **Revit Export IFC** does not use this prompt — site/coords stay in the selected Revit IFC setup

## Non-goals
- No import
- No native C# XKT writer
- No Revit IFC setup picker inside Navisworks
- No Navisworks Freedom/Viewer plugins
- No MetroCIM override of Revit IFC setup coordinate options
