# Tasks

Work in this order. Ask before skipping.

## Done
- [x] Scaffold Revit 2027 add-in (**.NET 10**, Nice3point Revit 2027 API refs, `.addin`)
- [x] Add `SharpGLTF.Toolkit`
- [x] `ExportGltfCommand`, `ExportXktCommand`, and `ExportIfcCommand`
- [x] Ribbon tab **MetroCIM** with **three** buttons: Export glTF, Export XKT, Export IFC
- [x] `VisibilityService`, `ColorResolver`, `GeometryBuilder`, `GltfExporter`, `XktConverter`, `IfcExporter`
- [x] Detect missing Node.js / xeokit-convert before XKT conversion
- [x] Project/assembly/add-in name **MetroCIM**

## Remaining
- [ ] 1. Keep glTF, XKT, and IFC as separate ribbon commands; do not recombine them
- [x] 2. Install XKT runtime: Node.js + `npm install -g @xeokit/xeokit-convert`
- [x] 3. After code changes: `dotnet build -p:DeployAddin=true`, then restart Revit
- [ ] 4. Test a 3D view with a filter, an element override, and MEP system-type coloring
- [ ] 5. Confirm `.glb` opens in a glTF viewer and `.xkt` opens in a xeokit viewer
- [ ] 6. Confirm IFC export uses a saved/built-in Revit IFC setup

## Constraints
- No telemetry and no unprompted network calls (only the local xeokit-convert process)
- Revit 2027 API only
- `XktConverter` stays swappable if a native writer appears later
