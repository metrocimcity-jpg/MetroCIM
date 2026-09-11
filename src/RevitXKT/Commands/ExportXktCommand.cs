using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitXKT.Models;
using RevitXKT.Services;

namespace RevitXKT.Commands;

[Transaction(TransactionMode.ReadOnly)]
[Regeneration(RegenerationOption.Manual)]
public sealed class ExportXktCommand : IExternalCommand
{
    public const string Title = "Export XKT";

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (!CommandUi.TryGetActive3DView(commandData, Title, out Document document, out View3D view))
            return Result.Cancelled;

        var converter = new XktConverter();
        XktToolStatus toolStatus = converter.Detect();
        if (toolStatus != XktToolStatus.Available)
        {
            string hint = toolStatus == XktToolStatus.MissingNode
                ? "Node.js was not found on PATH. Install Node.js, then run:\n" + XktConverter.InstallHint
                : "@xeokit/xeokit-convert was not found. Install it with:\n" + XktConverter.InstallHint;
            CommandUi.Show(Title, $"XKT export requires xeokit-convert.\n{hint}");
            return Result.Cancelled;
        }

        string? xktPath = CommandUi.PickSavePath(
            Title,
            "xeokit XKT (*.xkt)|*.xkt",
            CommandUi.DefaultBaseName(view, document));
        if (xktPath is null)
            return Result.Cancelled;

        string gltfPath = Path.ChangeExtension(xktPath, ".glb");

        try
        {
            var exporter = new ViewExporter();
            Dictionary<ResolvedAppearance, TriangleMesh>? meshes = exporter.BuildMeshes(document, view, out int elementCount);
            if (meshes is null)
            {
                CommandUi.Show(Title, "No visible 3D geometry was found in the active view.");
                return Result.Failed;
            }

            exporter.WriteGltf(gltfPath, meshes);
            XktConversionResult conversion = converter.Convert(gltfPath, xktPath);
            if (conversion.Success)
            {
                CommandUi.Show(
                    Title,
                    $"XKT saved\n{conversion.Path}\n\nIntermediate glTF\n{gltfPath}\nElements with geometry: {elementCount}");
                return Result.Succeeded;
            }

            CommandUi.Show(
                Title,
                $"XKT conversion failed.\n{conversion.Error}\n\nIntermediate glTF was saved:\n{gltfPath}");
            return Result.Failed;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            CommandUi.Show(Title, $"XKT export failed.\n{ex.Message}");
            return Result.Failed;
        }
    }
}
