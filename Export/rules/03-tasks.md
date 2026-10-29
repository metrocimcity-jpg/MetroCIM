# Tasks

Work in this order. Ask before skipping.

Historical plan (Navisworks add-in, completed): [navisworks_metrocim_add-in.plan.md](navisworks_metrocim_add-in.plan.md).

## Done
- [x] Scaffold Revit add-in for **2025 / 2026 / 2027** (Nice3point API refs, `.addin`)
- [x] Shared `MetroCIM.Core` (glTF / XKT / IFC mesh writers)
- [x] Navisworks Manage/Simulate add-in for **2025 / 2026 / 2027** (bundle + ribbon)
- [x] `ExportGltfCommand`, `ExportXktCommand`, and `ExportIfcCommand` (Revit)
- [x] Navisworks Export glTF / XKT / IFC (mesh IFC4)
- [x] Ribbon tab **MetroCIM** with **four** buttons on both hosts (glTF, XKT, IFC, Excel)
- [x] Detect missing Node.js / xeokit-convert before XKT conversion
- [x] Project/assembly/add-in name **MetroCIM**

## Done (coordinates)
- [x] Local / Shared Coordination choice for Revit glTF/XKT and all Navisworks mesh exporters
- [x] Revit IFC coordinates remain entirely in the Revit IFC setup

## Done (Excel)
- [x] Core `ParameterTable` + `ExcelTableWriter` (OpenXML)
- [x] Export Excel on Revit and Navisworks (visible instances, Revit Id + IFC GUID + all parameters/properties)
- [x] Unit tests for `ExcelTableWriter`

## Done (packaging / layout)
- [x] Separate per-user NSIS EXE installers for Revit and Navisworks (no admin)
- [x] Move product under `Export/` (`Core`, `Revit`, `Navisworks`, `Tests`, `installer`, `docs`, `rules`)
- [x] Point root README and `.cursor/rules/00-export-home.mdc` at `Export/rules`

## Remaining
- [ ] 1. Keep glTF, XKT, IFC, and Excel as separate ribbon commands; do not recombine them
- [x] 2. Install XKT runtime: Node.js + `npm install -g @xeokit/xeokit-convert`
- [x] 3. After code changes: deploy Revit (`-p:RevitVersion=`) and/or Navisworks (`-p:NavisworksVersion=`), then restart the host
- [ ] 4. Test a Revit 3D view with a filter, an element override, and MEP system-type coloring
- [ ] 5. Confirm `.glb` opens in a glTF viewer and `.xkt` opens in a xeokit viewer
- [ ] 6. Confirm Revit IFC export uses a saved/built-in Revit IFC setup
- [ ] 7. Confirm Navisworks Export IFC opens in a viewer with on-screen colors
- [ ] 8. Confirm Local vs Shared placement for a coordinated model (Revit glTF + Navisworks glTF)
- [ ] 9. Confirm Export Excel on both hosts (Id / IFC GUID columns, instance + type or NW properties)

## Constraints
- No telemetry and no unprompted network calls (only the local xeokit-convert process)
- Revit 2025–2027 and Navisworks 2025–2027 (same repo, separate projects under `Export/`)
- `XktConverter` stays swappable if a native writer appears later
- No new GitHub repo for Navisworks — stay in MetroCIM
- Agent prompts and product docs live in `Export/rules/` and `Export/README.md`
