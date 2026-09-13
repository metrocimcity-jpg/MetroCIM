using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Forms;
using RevitTaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace MetroCIM.Commands;

internal static class CommandUi
{
    public static bool TryGetActiveDocument(
        ExternalCommandData commandData,
        string title,
        out Document document,
        out Autodesk.Revit.DB.View view)
    {
        UIDocument? uiDoc = commandData.Application.ActiveUIDocument;
        if (uiDoc is null || uiDoc.Document is null)
        {
            Show(title, "Open a project before exporting.");
            document = null!;
            view = null!;
            return false;
        }

        if (uiDoc.ActiveView is null)
        {
            Show(title, "Activate a view before exporting.");
            document = null!;
            view = null!;
            return false;
        }

        document = uiDoc.Document;
        view = uiDoc.ActiveView;
        return true;
    }

    public static bool TryGetActive3DView(
        ExternalCommandData commandData,
        string title,
        out Document document,
        out View3D view)
    {
        if (!TryGetActiveDocument(commandData, title, out document, out Autodesk.Revit.DB.View activeView))
        {
            view = null!;
            return false;
        }

        if (activeView is not View3D view3D || view3D.IsTemplate)
        {
            Show(title, "The active view must be a 3D view (not a template).");
            view = null!;
            return false;
        }

        view = view3D;
        return true;
    }

    public static string DefaultBaseName(Autodesk.Revit.DB.View view, Document document) =>
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

    public static bool Confirm(string title, string body)
    {
        var dialog = new RevitTaskDialog(title)
        {
            MainInstruction = title,
            MainContent = body,
            CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No
        };
        return dialog.Show() == TaskDialogResult.Yes;
    }
}
