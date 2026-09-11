using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Forms;
using RevitTaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace RevitXKT.Commands;

internal static class CommandUi
{
    public static bool TryGetActive3DView(
        ExternalCommandData commandData,
        string title,
        out Document document,
        out View3D view)
    {
        UIDocument? uiDoc = commandData.Application.ActiveUIDocument;
        if (uiDoc is null)
        {
            Show(title, "Open a project and activate a 3D view before exporting.");
            document = null!;
            view = null!;
            return false;
        }

        if (uiDoc.ActiveView is not View3D view3D || view3D.IsTemplate)
        {
            Show(title, "The active view must be a 3D view (not a template).");
            document = null!;
            view = null!;
            return false;
        }

        document = uiDoc.Document;
        view = view3D;
        return true;
    }

    public static string DefaultBaseName(View3D view, Document document) =>
        SanitizeFileName(string.IsNullOrWhiteSpace(view.Name) ? document.Title : view.Name);

    public static string? PickSavePath(string title, string filter, string fileName)
    {
        using var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = fileName,
            AddExtension = true,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
    }

    public static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        name = name.Trim();
        return string.IsNullOrWhiteSpace(name) ? "view" : name;
    }

    public static void Show(string title, string body)
    {
        var dialog = new RevitTaskDialog(title)
        {
            MainInstruction = title,
            MainContent = body,
            CommonButtons = TaskDialogCommonButtons.Ok
        };
        dialog.Show();
    }
}
