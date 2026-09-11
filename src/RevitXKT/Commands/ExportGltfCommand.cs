using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitXKT.Models;
using RevitXKT.Services;

namespace RevitXKT.Commands;

[Transaction(TransactionMode.ReadOnly)]
[Regeneration(RegenerationOption.Manual)]
public sealed class ExportGltfCommand : IExternalCommand
{
    public const string Title = "Export glTF";

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (!CommandUi.TryGetActive3DView(commandData, Title, out Document document, out View3D view))
            return Result.Cancelled;

        string? outputPath = CommandUi.PickSavePath(
            Title,
            "glTF Binary (*.glb)|*.glb|glTF JSON (*.gltf)|*.gltf",
            CommandUi.DefaultBaseName(view, document));
        if (outputPath is null)
            return Result.Cancelled;

        try
        {
            var exporter = new ViewExporter();
            Dictionary<ResolvedAppearance, TriangleMesh>? meshes = exporter.BuildMeshes(document, view, out int elementCount);
            if (meshes is null)
            {
                CommandUi.Show(Title, "No visible 3D geometry was found in the active view.");
                return Result.Failed;
            }

            exporter.WriteGltf(outputPath, meshes);
            CommandUi.Show(Title, $"glTF saved\n{outputPath}\nElements with geometry: {elementCount}");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            CommandUi.Show(Title, $"glTF export failed.\n{ex.Message}");
            return Result.Failed;
        }
    }
}
