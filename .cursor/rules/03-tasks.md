# Tasks

Work in this order. Ask before skipping.

## Done
- [x] Scaffold Revit add-in for **2025 / 2026 / 2027** (Nice3point API refs, `.addin`)
- [x] Shared `MetroCIM.Core` (glTF / XKT / IFC mesh writers)
- [x] Navisworks Manage/Simulate add-in for **2025 / 2026 / 2027** (bundle + ribbon)
- [x] `ExportGltfCommand`, `ExportXktCommand`, and `ExportIfcCommand` (Revit)
- [x] Navisworks Export glTF / XKT / IFC (mesh IFC4)
- [x] Ribbon tab **MetroCIM** with **three** buttons on both hosts
- [x] Detect missing Node.js / xeokit-convert before XKT conversion
- [x] Project/assembly/add-in name **MetroCIM**

## Remaining
- [ ] 1. Keep glTF, XKT, and IFC as separate ribbon commands; do not recombine them
- [x] 2. Install XKT runtime: Node.js + `npm install -g @xeokit/xeokit-convert`
- [x] 3. After code changes: deploy Revit (`-p:RevitVersion=`) and/or Navisworks (`-p:NavisworksVersion=`), then restart the host
- [ ] 4. Test a Revit 3D view with a filter, an element override, and MEP system-type coloring
- [ ] 5. Confirm `.glb` opens in a glTF viewer and `.xkt` opens in a xeokit viewer
- [ ] 6. Confirm Revit IFC export uses a saved/built-in Revit IFC setup
- [ ] 7. Confirm Navisworks Export IFC opens in a viewer with on-screen colors

## Constraints
- No telemetry and no unprompted network calls (only the local xeokit-convert process)
- Revit 2025–2027 and Navisworks 2025–2027 (same repo, separate projects)
- `XktConverter` stays swappable if a native writer appears later
- No new GitHub repo for Navisworks — stay in MetroCIM
