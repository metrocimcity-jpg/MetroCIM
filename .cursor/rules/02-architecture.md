# Architecture

## Projects
- `MetroCIM.Core` — host-free mesh models + glTF / XKT / IFC mesh writers
- `MetroCIM` — Revit add-in
- `MetroCIM.Navisworks` — Navisworks Manage/Simulate add-in
- `MetroCIM.Tests` — unit tests

Do not merge XKT conversion into the glTF writer. Do not put Revit or Navisworks API types in Core.

## Revit
Keep types small: `VisibilityService`, `ColorResolver`, `MepSystemTypeColor`, `IfcColorPriority`, `GeometryBuilder`, `ViewExporter`, `MetadataExporter`, `IfcExporter`, `IfcAppearanceApplier`, `IfcColorPatcher`, `IfcPortGeometryStripper`. Shared writers live in Core (`GltfSceneAccumulator`, `XktConverter`).

### Commands
- `ExportGltfCommand` — `View3D`, `.glb`/`.gltf`
- `ExportXktCommand` — same mesh pipeline + xeokit-convert
- `ExportIfcCommand` — Revit IFC setup + `Document.Export` + patchers

### Visibility / color / IFC
Unchanged Revit rules: view visibility; surface colors (filters, overrides, MEP system type); IFC first-match filter → override → MEP → material; STEP GlobalId patch; strip distribution ports.

## Navisworks
- `MetroCIMRibbon` (`CommandHandlerPlugin`) — tab **MetroCIM**, three export commands
- `VisibilityService` — `Models.RootItems` → `DescendantsAndSelf`; skip hidden / no geometry
- `ColorResolver` — COM fragment appearance (diffuse), then color-like properties
- `GeometryBuilder` — COM `GenerateSimplePrimitives`; units → meters; Z-up → glTF Y-up
- `DocumentExporter` — visible items → Core glTF / IFC writers
- `MetadataExporter` — xeokit `metadata.json` from item properties
- IFC: `IfcMeshExporter` (IFC4 tessellation + `IfcStyledItem` colors). No Revit setup UI.

## Packaging
- Revit: `%AppData%\Autodesk\Revit\Addins\<year>\`
- Navisworks: `%AppData%\Autodesk\ApplicationPlugins\MetroCIM.Navisworks.bundle\` with `PackageContents.xml` (Nw22/Nw23/Nw24)

## XKT
```text
node --max-old-space-size=16384 convert2xkt.js -s "<glb>" -o "<xkt>" -m "<metadata.json>" -e 1
```
Missing tool message: `npm install -g @xeokit/xeokit-convert`.
