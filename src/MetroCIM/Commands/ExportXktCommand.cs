using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MetroCIM.Models;
using MetroCIM.Services;

namespace MetroCIM.Commands;

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

        string directory = Path.GetDirectoryName(xktPath)!;
        string gltfPath = Path.ChangeExtension(xktPath, ".glb");
        string metadataPath = Path.Combine(directory, "metadata.json");

        try
        {
            var exporter = new ViewExporter();
            var metadata = new MetadataExporter();
            var exported = new List<ExportedElement>();
            int count;
            XktConversionResult conversion;

            using (var progress = new ExportProgress(Title, 1))
            {
                progress.Report(0, "Collecting visible elements");
                count = exporter.ExportColorBatched(
                    document,
                    view,
                    gltfPath,
                    (current, total, status) => progress.Report(current, status, total),
                    exported);

                if (count == 0)
                {
                    CommandUi.Show(Title, "No visible 3D geometry was found in the active view.");
                    return Result.Failed;
                }

                progress.Report(count, "Writing metadata.json", count);
                metadata.Write(metadataPath, document, view, exported);
                progress.UseMarquee("Converting to XKT — Revit stays responsive");
                conversion = converter.Convert(
                    gltfPath,
                    xktPath,
                    metadataPath,
                    progress.Pump);
            }
            if (conversion.Success)
            {
                CommandUi.Show(
                    Title,
                    $"XKT saved\n{conversion.Path}\n\nmetadata.json\n{metadataPath}\n\nIntermediate glTF\n{gltfPath}\nElements with geometry: {count}");
                return Result.Succeeded;
            }

            CommandUi.Show(
                Title,
                $"XKT conversion failed.\n{conversion.Error}\n\nmetadata.json was saved:\n{metadataPath}\nIntermediate glTF:\n{gltfPath}");
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
