using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MetroCIM.Services;

namespace MetroCIM.Commands;

[Transaction(TransactionMode.ReadOnly)]
[Regeneration(RegenerationOption.Manual)]
public sealed class ExportExcelCommand : IExternalCommand
{
    public const string Title = "Export Excel";

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (!CommandUi.TryGetActive3DView(commandData, Title, out Document document, out View3D view))
            return Result.Cancelled;

        string? outputPath = CommandUi.PickSavePath(
            Title,
            "Excel Workbook (*.xlsx)|*.xlsx",
            CommandUi.DefaultBaseName(view, document) + "-parameters");
        if (outputPath is null)
            return Result.Cancelled;

        try
        {
            int count;
            using (var progress = new ExportProgress(Title, 1))
            {
                progress.Report(0, "Collecting visible elements");
                count = new ExcelParameterExporter().Export(
                    document,
                    view,
                    outputPath,
                    (current, total, status) => progress.Report(current, status, total));
            }

            if (count == 0)
            {
                CommandUi.Show(Title, "No visible model instances were found in the active view.");
                return Result.Failed;
            }

            CommandUi.Show(Title, $"Excel saved\n{outputPath}\n\nInstances: {count}");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            CommandUi.Show(Title, $"Excel export failed.\n{ex.Message}");
            return Result.Failed;
        }
    }
}
