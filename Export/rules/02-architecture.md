# Architecture

## Layout
```text
Export/
  Core/         MetroCIM.Core — host-free writers
  Revit/        MetroCIM — Revit add-in
  Navisworks/   MetroCIM.Navisworks — Manage/Simulate add-in
  Tests/        MetroCIM.Tests
  installer/    per-user NSIS EXE builders
  docs/         images and documentation assets
  rules/        project rules and plans
  README.md     MetroCIM.slnx  Directory.Build.props  LICENSE
```

Do not merge XKT conversion into the glTF writer. Do not put Revit or Navisworks API types in Core.

## Revit
Keep types small: `VisibilityService`, `ColorResolver`, `MepSystemTypeColor`, `IfcColorPriority`, `GeometryBuilder`, `ViewExporter`, `MetadataExporter`, `IfcExporter`, `IfcAppearanceApplier`, `IfcColorPatcher`, `IfcPortGeometryStripper`. Shared writers live in Core (`GltfSceneAccumulator`, `XktConverter`).

### Commands
- `ExportGltfCommand` / `ExportXktCommand` — `View3D`; pick `CoordinateMode` then save; mesh via `CoordinateFrame` + `GeometryBuilder`
- `ExportIfcCommand` — Revit IFC setup + `Document.Export` + patchers (no MetroCIM coordinate prompt)
- `ExportExcelCommand` — visible instances → `ExcelParameterExporter` → Core `ExcelTableWriter` (Revit Id, IFC GUID via `ExportUtils.GetExportId` + `IfcGuid`, Instance.* / Type.* parameters)

### Visibility / color / IFC
Unchanged Revit rules: view visibility; surface colors (filters, overrides, MEP system type); IFC first-match filter → override → MEP → material; STEP GlobalId patch; strip distribution ports.

### Coordinates (glTF / XKT)
- `CoordinateMode` + `CoordinateModePreference` in Core (`last-coordinate-mode.txt`)
- `CoordinateFrame` — Local = Project Base Point inverse; Shared = `ActiveProjectLocation.GetTotalTransform().Inverse`

## Navisworks
- `MetroCIMRibbon` (`CommandHandlerPlugin`) — tab **MetroCIM**, four export commands including Excel
- `VisibilityService` — `Models.RootItems` → `DescendantsAndSelf`; skip hidden / no geometry
- `ColorResolver` — COM fragment appearance (diffuse), then color-like properties
- `GeometryBuilder` — COM `GenerateSimplePrimitives`; units → meters; Z-up → glTF Y-up; Local subtracts primary model `Transform.Translation`
- `DocumentExporter` — visible items → Core glTF / IFC writers (`CoordinateMode`)
- `MetadataExporter` — xeokit `metadata.json` from item properties
- `ExcelParameterExporter` — visible items → Core `ExcelTableWriter` (Revit Id / IFC GUID from properties or InstanceGuid)
- IFC: `IfcMeshExporter` (IFC4 tessellation + `IfcStyledItem` colors). No Revit setup UI.

## Packaging
- Revit: `%AppData%\Autodesk\Revit\Addins\<year>\`
- Navisworks: `%AppData%\Autodesk\ApplicationPlugins\MetroCIM.Navisworks.bundle\` with `PackageContents.xml` (Nw22/Nw23/Nw24)
- Separate per-user NSIS EXE installers (no admin): `Export/installer/Build-RevitInstaller.ps1` → `dist/MetroCIM-Revit-*.exe`; `Export/installer/Build-NavisworksInstaller.ps1` → `dist/MetroCIM-Navisworks-*.exe`

## XKT
```text
node --max-old-space-size=16384 convert2xkt.js -s "<glb>" -o "<xkt>" -m "<metadata.json>" -e 1
```
Missing tool message: `npm install -g @xeokit/xeokit-convert`.
