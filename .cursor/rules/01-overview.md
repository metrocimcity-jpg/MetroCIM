# Revit 2027 Add-in: View-Accurate glTF and XKT Export

## Goal
Export the **active 3D view** so the file matches what is on screen:

- Only currently visible elements (view visibility, temporary hide/isolate, category/workset)
- View filter colors, patterns, and transparency
- Element and category graphic overrides
- MEP system-type / color-fill colors when those are active

## Stack
- Revit 2027 API, C#, **.NET 10** (`net10.0-windows`)
- `IExternalApplication` ribbon tab **RevitXKT** with two commands
- glTF via **SharpGLTF.Toolkit** (NuGet)
- XKT via **xeokit-convert** (Node.js CLI): glTF → `.xkt`
  - Install: `npm install -g @xeokit/xeokit-convert`
  - The XKT command calls this CLI with `Process.Start`
  - If Node.js or xeokit-convert is missing, XKT export stops and shows the install hint

## Ribbon
- **Export glTF** — writes `.glb` or `.gltf` only
- **Export XKT** — writes intermediate `.glb` next to the chosen `.xkt`, then converts

## Non-goals
- No import
- No native C# XKT writer
- No 2D views or sheets
