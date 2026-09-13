using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MetroCIM.Services;

namespace MetroCIM.Commands;

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
            int count;
            using (var progress = new ExportProgress(Title, 1))
            {
                progress.Report(0, "Collecting visible elements");
                count = exporter.ExportColorBatched(
                    document,
                    view,
                    outputPath,
                    (current, total, status) =>
                    {
                        progress.Report(current, status, total);
                    });
            }

            if (count == 0)
            {
                CommandUi.Show(Title, "No visible 3D geometry was found in the active view.");
                return Result.Failed;
            }

            CommandUi.Show(Title, $"glTF saved\n{outputPath}\nElements with geometry: {count}");
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
