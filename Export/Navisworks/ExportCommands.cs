using Autodesk.Navisworks.Api;
using MetroCIM.Models;
using MetroCIM.Navisworks.Services;
using MetroCIM.Services;
using System.Windows.Forms;
using NwApp = Autodesk.Navisworks.Api.Application;

namespace MetroCIM.Navisworks;

internal static class ExportCommands
{
    public static int ExportGltf()
    {
        if (!TryGetDocument(out Document document))
            return 0;

        CoordinateMode? coordinateMode = CommandUi.PickCoordinateMode("Export glTF");
        if (coordinateMode is null)
            return 0;

        string? path = CommandUi.PickSavePath(
            "Export glTF",
            "glTF Binary (*.glb)|*.glb|glTF JSON (*.gltf)|*.gltf",
            CommandUi.DefaultBaseName(document));
        if (path is null)
            return 0;

        try
        {
            int count;
            using (var progress = new ExportProgress("Export glTF", 1))
            {
                progress.Report(0, "Collecting visible items");
                count = new DocumentExporter().ExportColorBatched(
                    document,
                    path,
                    (current, total, status) => progress.Report(current, status, total),
                    coordinateMode: coordinateMode.Value);
            }

            if (count == 0)
            {
                CommandUi.Show("Export glTF", "No visible 3D geometry was found in the model.");
                return 0;
            }

            CommandUi.Show("Export glTF", $"glTF saved\n{path}\n\nItems with geometry: {count}");
            return 0;
        }
        catch (Exception ex)
        {
            CommandUi.Show("Export glTF", $"glTF export failed.\n{ex.Message}");
            return 0;
        }
    }

    public static int ExportXkt()
    {
        if (!TryGetDocument(out Document document))
            return 0;

        var converter = new XktConverter();
        XktToolStatus toolStatus = converter.Detect();
        if (toolStatus != XktToolStatus.Available)
        {
            string hint = toolStatus == XktToolStatus.MissingNode
                ? "Node.js was not found on PATH. Install Node.js, then run:\n" + XktConverter.InstallHint
                : "@xeokit/xeokit-convert was not found. Install it with:\n" + XktConverter.InstallHint;
            CommandUi.Show("Export XKT", $"XKT export requires xeokit-convert.\n{hint}");
            return 0;
        }

        CoordinateMode? coordinateMode = CommandUi.PickCoordinateMode("Export XKT");
        if (coordinateMode is null)
            return 0;

        string? xktPath = CommandUi.PickSavePath(
            "Export XKT",
            "xeokit XKT (*.xkt)|*.xkt",
            CommandUi.DefaultBaseName(document));
        if (xktPath is null)
            return 0;

        string directory = Path.GetDirectoryName(xktPath)!;
        string gltfPath = Path.ChangeExtension(xktPath, ".glb");
        string metadataPath = Path.Combine(directory, "metadata.json");

        try
        {
            var exporter = new DocumentExporter();
            var metadata = new MetadataExporter();
            var exported = new List<ExportedItem>();
            int count;
            XktConversionResult conversion;

            using (var progress = new ExportProgress("Export XKT", 1))
            {
                progress.Report(0, "Collecting visible items");
                count = exporter.ExportColorBatched(
                    document,
                    gltfPath,
                    (current, total, status) => progress.Report(current, status, total),
                    exported,
                    coordinateMode.Value);

                if (count == 0)
                {
                    CommandUi.Show("Export XKT", "No visible 3D geometry was found in the model.");
                    return 0;
                }

                progress.Report(count, "Writing metadata.json", count);
                metadata.Write(metadataPath, document, exported);
                progress.UseMarquee("Converting to XKT");
                conversion = converter.Convert(gltfPath, xktPath, metadataPath, progress.Pump);
            }

            if (conversion.Success)
            {
                CommandUi.Show(
                    "Export XKT",
                    $"XKT saved\n{conversion.Path}\n\nmetadata.json\n{metadataPath}\n\nIntermediate glTF\n{gltfPath}\nItems with geometry: {count}");
                return 0;
            }

            CommandUi.Show(
                "Export XKT",
                $"XKT conversion failed.\n{conversion.Error}\n\nmetadata.json was saved:\n{metadataPath}\nIntermediate glTF:\n{gltfPath}");
            return 0;
        }
        catch (Exception ex)
        {
            CommandUi.Show("Export XKT", $"XKT export failed.\n{ex.Message}");
            return 0;
        }
    }

    public static int ExportIfc()
    {
        if (!TryGetDocument(out Document document))
            return 0;

        CoordinateMode? coordinateMode = CommandUi.PickCoordinateMode("Export IFC");
        if (coordinateMode is null)
            return 0;

        string? path = CommandUi.PickSavePath(
            "Export IFC",
            "IFC (*.ifc)|*.ifc",
            CommandUi.DefaultBaseName(document));
        if (path is null)
            return 0;

        try
        {
            int count;
            using (var progress = new ExportProgress("Export IFC", 1))
            {
                progress.Report(0, "Collecting visible items");
                count = new DocumentExporter().ExportIfc(
                    document,
                    path,
                    (current, total, status) => progress.Report(current, status, total),
                    coordinateMode.Value);
            }

            if (count == 0)
            {
                CommandUi.Show("Export IFC", "No visible 3D geometry was found in the model.");
                return 0;
            }

            CommandUi.Show("Export IFC", $"IFC saved\n{path}\n\nItems with geometry: {count}\n\nIFC4 mesh export (MetroCIM)");
            return 0;
        }
        catch (Exception ex)
        {
            CommandUi.Show("Export IFC", $"IFC export failed.\n{ex.Message}");
            return 0;
        }
    }

    public static int ExportExcel()
    {
        if (!TryGetDocument(out Document document))
            return 0;

        string? path = CommandUi.PickSavePath(
            "Export Excel",
            "Excel Workbook (*.xlsx)|*.xlsx",
            CommandUi.DefaultBaseName(document) + "-parameters");
        if (path is null)
            return 0;

        try
        {
            int count;
            using (var progress = new ExportProgress("Export Excel", 1))
            {
                progress.Report(0, "Collecting visible items");
                count = new ExcelParameterExporter().Export(
                    document,
                    path,
                    (current, total, status) => progress.Report(current, status, total));
            }

            if (count == 0)
            {
                CommandUi.Show("Export Excel", "No visible model instances were found.");
                return 0;
            }

            CommandUi.Show("Export Excel", $"Excel saved\n{path}\n\nInstances: {count}");
            return 0;
        }
        catch (Exception ex)
        {
            CommandUi.Show("Export Excel", $"Excel export failed.\n{ex.Message}");
            return 0;
        }
    }

    private static bool TryGetDocument(out Document document)
    {
        document = null!;
        Document? active = NwApp.ActiveDocument;
        if (active is null || active.IsClear)
        {
            CommandUi.Show("MetroCIM", "Open a Navisworks model before exporting.");
            return false;
        }

        document = active;
        return true;
    }
}

internal static class CommandUi
{
    private static string PreferenceDirectory =>
        Path.GetDirectoryName(typeof(CommandUi).Assembly.Location)!;

    public static string DefaultBaseName(Document document)
    {
        string title = string.IsNullOrWhiteSpace(document.Title) ? "Model" : document.Title;
        foreach (char c in Path.GetInvalidFileNameChars())
            title = title.Replace(c, '_');
        return title;
    }

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

    public static CoordinateMode? PickCoordinateMode(string title)
    {
        CoordinateMode preferred = CoordinateModePreference.Read(PreferenceDirectory);

        using var form = new Form
        {
            Text = title,
            Width = 440,
            Height = 200,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false
        };

        var label = new Label
        {
            Left = 16,
            Top = 16,
            Width = 390,
            Text = "Coordinate system"
        };

        var local = new RadioButton
        {
            Left = 16,
            Top = 48,
            Width = 390,
            Text = "Local location (primary model origin)",
            Checked = preferred == CoordinateMode.Local
        };

        var shared = new RadioButton
        {
            Left = 16,
            Top = 78,
            Width = 390,
            Text = "Shared Coordination (world / federated)",
            Checked = preferred == CoordinateMode.Shared
        };

        var ok = new Button
        {
            Text = "Continue",
            DialogResult = DialogResult.OK,
            Left = 220,
            Top = 118,
            Width = 90
        };
        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Left = 316,
            Top = 118,
            Width = 90
        };

        form.Controls.Add(label);
        form.Controls.Add(local);
        form.Controls.Add(shared);
        form.Controls.Add(ok);
        form.Controls.Add(cancel);
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        if (form.ShowDialog() != DialogResult.OK)
            return null;

        CoordinateMode mode = shared.Checked ? CoordinateMode.Shared : CoordinateMode.Local;
        CoordinateModePreference.Write(PreferenceDirectory, mode);
        return mode;
    }

    public static void Show(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
}
