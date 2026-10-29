using MetroCIM.Services;
using WinForms = System.Windows.Forms;

namespace MetroCIM.Commands;

internal static class IfcSetupDialog
{
    public static string? Pick(string title, IReadOnlyList<string> names, string? preferred)
    {
        if (names.Count == 0)
            return null;

        using var form = new WinForms.Form
        {
            Text = title,
            Width = 520,
            Height = 180,
            FormBorderStyle = WinForms.FormBorderStyle.FixedDialog,
            StartPosition = WinForms.FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false
        };

        var label = new WinForms.Label
        {
            Left = 16,
            Top = 16,
            Width = 470,
            Text = "Revit IFC setup"
        };

        var combo = new WinForms.ComboBox
        {
            Left = 16,
            Top = 42,
            Width = 470,
            DropDownStyle = WinForms.ComboBoxStyle.DropDownList
        };
        foreach (string name in names)
            combo.Items.Add(name);

        combo.SelectedItem = IfcSetupName.Resolve(names, preferred) ?? names[0];

        var export = new WinForms.Button
        {
            Text = "Export",
            DialogResult = WinForms.DialogResult.OK,
            Left = 300,
            Top = 92,
            Width = 90
        };
        var cancel = new WinForms.Button
        {
            Text = "Cancel",
            DialogResult = WinForms.DialogResult.Cancel,
            Left = 396,
            Top = 92,
            Width = 90
        };

        form.Controls.Add(label);
        form.Controls.Add(combo);
        form.Controls.Add(export);
        form.Controls.Add(cancel);
        form.AcceptButton = export;
        form.CancelButton = cancel;

        return form.ShowDialog() == WinForms.DialogResult.OK && combo.SelectedItem is string selected
            ? selected
            : null;
    }
}
