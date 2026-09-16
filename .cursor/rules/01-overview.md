# Revit 2025 / 2026 / 2027 Add-in: MetroCIM

## Goal
Export the **active 3D view** so glTF/XKT match what is on screen, and export **IFC** using a Revit IFC setup.

glTF / XKT:
- Only currently visible elements (view visibility, temporary hide/isolate, category/workset)
- On-screen colors (view filters, graphic overrides, MEP system type / color fill)

IFC color (first match wins, only on that element):
1. View filter that actually includes this element (its category and rules)
2. Graphic override on this element
3. MEP system type / color fill
4. Material of the element

## Stack
- Revit **2025 / 2026 / 2027** API, C# (`net8.0-windows` for 2025–2026, `net10.0-windows` for 2027)
- `IExternalApplication` ribbon tab **MetroCIM**
- glTF via **SharpGLTF.Toolkit** (NuGet)
- XKT via **xeokit-convert** (Node.js CLI): glTF → `.xkt`
  - Install: `npm install -g @xeokit/xeokit-convert`
  - The XKT command calls this CLI with `Process.Start`
  - If Node.js or xeokit-convert is missing, XKT export stops and shows the install hint
- IFC via Revit's `Document.Export` and the official IFC export setups (in-session, built-in, and saved in the model)

## Ribbon
Tab **MetroCIM**, panel **Export**:
- **Export glTF** — writes `.glb` or `.gltf` only
- **Export XKT** — writes intermediate `.glb` and `metadata.json` next to the chosen `.xkt`, then converts
- **Export IFC** — writes `.ifc` using the selected Revit IFC setup

## Non-goals
- No import
- No native C# XKT writer
- No 2D views or sheets for glTF/XKT
