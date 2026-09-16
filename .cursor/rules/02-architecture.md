# Architecture

Keep types small: `VisibilityService`, `ColorResolver`, `MepSystemTypeColor`, `IfcColorPriority`, `GeometryBuilder`, `GltfExporter`, `XktConverter`, `ViewExporter`, `MetadataExporter`, `IfcExporter`, `IfcAppearanceApplier`, `IfcColorPatcher`, `IfcPortGeometryStripper`. Do not merge XKT conversion into the glTF writer.

## Commands
- `ExportGltfCommand` — validate `View3D`, pick `.glb`/`.gltf`, build meshes, save glTF
- `ExportXktCommand` — same mesh pipeline, require xeokit-convert first, pick `.xkt`, write sibling `.glb` and `metadata.json`, convert with `-m`
- `ExportIfcCommand` — any open view, pick a Revit IFC setup, pick `.ifc`, `Document.Export` with that setup's options

Shared UI lives in `CommandUi`. Shared mesh + glTF write lives in `ViewExporter`.

## Visibility
`FilteredElementCollector(doc, view.Id)` + `VisibleInViewFilter`. Then skip:
- Hidden elements (`element.IsHidden(view)`)
- Hidden categories (`view.GetCategoryHidden`)
- Temporary hide/isolate (`view.IsTemporaryHideIsolateActive()` and `IsElementVisibleInTemporaryViewMode`)
- Hidden worksets
- Non-model categories, types, and `RevitLinkInstance`

## Color (3D shaded, last matching filter wins)
Use **surface / fill** colors, never projection-line color, as mesh albedo.

1. View filter surface overrides — `GetOrderedFilters()`, `GetFilterOverrides`
2. Element **surface** overrides (category overrides are skipped for pipes so they cannot hide system-type material)
3. Color fill scheme **unless** it is driven by pipe physical material
4. **`MepSystemTypeColor`**: `PipingSystemType.MaterialId` (never pipe/`PipeType` material), then system FillColor, then LineColor
5. Never pipe segment material, pipe type material, `RBS_PIPE_MATERIAL_PARAM`, or `element.GetMaterialIds()` on pipes
6. Other categories: non-black element materials, else a neutral default

`PipeInsulation` / other `InsulationLiningBase` elements inherit appearance from `HostElementId` so insulated pipes match system-type colors. Pipe fittings and accessories use the connected pipe’s system type when they have no system parameter of their own. If an element has mixed MEP system classifications or system types, ignore electrical (power, data, fire alarm, cable tray/conduit, and similar) and keep the piping or HVAC system type.

## Color (IFC, first match wins)
1. View filter **only if this element passes that filter** (same category and rules as the view). Do not copy a host, insulation, or nested-family filter onto other elements. Surface/cut override colors only — not projection-line color.
2. Color fill / `MepSystemTypeColor` for elements that had no filter color. Mechanical equipment uses its own system-type parameter only; it does not inherit color from connected ducts or pipes.
3. Material of this element

Never write a filter color onto `MEPSystemType.MaterialId` — that would recolor every element on that system. Resolve every visible element's color before pushing materials.

## Geometry and glTF
- `element.get_Geometry(new Options { View = view })`
- Triangulate solids/meshes (Coarse 0.50, Medium 0.55, Fine 0.65), feet → meters, Z-up → glTF Y-up
- Group by resolved color; split large color batches so xeokit-convert can process them; `SceneBuilder` → `SaveGLB` / `SaveGLTF`

## XKT
Resolve `node` + `convert2xkt.js` (or `xeokit-convert` on PATH). Write the same color-batched glTF as Export glTF, plus xeokit `metadata.json` (metaObjects + propertySets) next to the `.xkt`. Run:

```text
node --max-old-space-size=16384 convert2xkt.js -s "<glb>" -o "<xkt>" -m "<metadata.json>" -e 1
```

Detect the CLI before conversion. Missing tool message: `npm install -g @xeokit/xeokit-convert`.

## IFC
Load Revit's IFC setups from `Autodesk.IFC.Export.UI` or `Revit.IFC.Export` (in-session, built-in, and document-saved when those can be read). Missing saved setups must not block the built-in list. Before `document.Export`, apply `ColorResolver` in **IFC** mode: view filter, then MEP system type / color fill, then the element's material (including pipe materials). Graphic overrides are not used for IFC. After export, patch `IfcStyledItem` / `IfcColourRgb` by IFC GlobalId (22-char) or Revit UniqueId, never by Mark/ElementId. Shared fitting `IfcMappedItem` geometry is cloned per color so elbows on different systems do not keep the family metal style. Strip `IfcDistributionPort` entities entirely after export (viewers draw flow-direction cones from the port even when Representation is empty). Also drop `IfcRelNests` / `IfcRelConnectsPort*` that only exist for those ports. Snapshot the IFC, then roll back temporary materials. Export with the file name **without** extension, `ActiveViewId` as the numeric view id, tessellation **0.8**, and retry without `FilterViewId` if the file is empty.
