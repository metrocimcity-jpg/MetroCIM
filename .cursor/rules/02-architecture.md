# Architecture

Keep types small: `VisibilityService`, `ColorResolver`, `GeometryBuilder`, `GltfExporter`, `XktConverter`, `ViewExporter`. Do not merge XKT conversion into the glTF writer.

## Commands
- `ExportGltfCommand` — validate `View3D`, pick `.glb`/`.gltf`, build meshes, save glTF
- `ExportXktCommand` — same mesh pipeline, require xeokit-convert first, pick `.xkt`, save sibling `.glb`, convert

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
2. Element then category **surface** overrides
3. Color fill scheme, else MEP system type **FillColor** then `LineColor`
   (`RBS_PIPING_SYSTEM_TYPE_PARAM` / `RBS_DUCT_SYSTEM_TYPE_PARAM`, then `MEPCurve.MEPSystem`)
4. Non-black materials, else a neutral default

`PipeInsulation` / other `InsulationLiningBase` elements inherit appearance from `HostElementId` so insulated pipes match system-type colors.

## Geometry and glTF
- `element.get_Geometry(new Options { View = view })`
- Triangulate solids/meshes, feet → meters, Z-up → glTF Y-up
- Group by resolved color; `SceneBuilder` → `SaveGLB` / `SaveGLTF`

## XKT
Resolve `node` + `convert2xkt.js` (or `xeokit-convert` on PATH). Run:

```text
node --max-old-space-size=8192 convert2xkt.js -s "<glb>" -o "<xkt>"
```

Detect the CLI before conversion. Missing tool message: `npm install -g @xeokit/xeokit-convert`.
