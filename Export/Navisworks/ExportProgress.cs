using System.Windows.Forms;
using WinFormsApp = System.Windows.Forms.Application;

namespace MetroCIM.Navisworks;

internal sealed class ExportProgress : IDisposable
{
    private readonly Form _form;
    private readonly ProgressBar _bar;
    private readonly Label _status;
    private readonly int _minimumTotal;
    private bool _marquee;

    public ExportProgress(string title, int minimumTotal)
    {
        _minimumTotal = Math.Max(1, minimumTotal);
        _form = new Form
        {
            Text = title,
            Width = 420,
            Height = 120,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterScreen,
            ShowInTaskbar = false,
            TopMost = true
        };
        _status = new Label
        {
            Left = 12,
            Top = 12,
            Width = 380,
            Height = 20,
            Text = "Starting…"
        };
        _bar = new ProgressBar
        {
            Left = 12,
            Top = 40,
            Width = 380,
            Height = 22,
            Minimum = 0,
            Maximum = _minimumTotal,
            Style = ProgressBarStyle.Continuous
        };
        _form.Controls.Add(_status);
        _form.Controls.Add(_bar);
        _form.Show();
        WinFormsApp.DoEvents();
    }

    public void Report(int current, string status, int? total = null)
    {
        if (_marquee)
            return;

        int max = total is > 0 ? total.Value : _minimumTotal;
        if (_bar.Maximum != max)
            _bar.Maximum = Math.Max(1, max);
        _bar.Value = Math.Max(0, Math.Min(_bar.Maximum, current));
        _status.Text = status;
        Pump();
    }

    public void UseMarquee(string status)
    {
        _marquee = true;
        _bar.Style = ProgressBarStyle.Marquee;
        _status.Text = status;
        Pump();
    }

    public void Pump() => WinFormsApp.DoEvents();

    public void Dispose()
    {
        _form.Close();
        _form.Dispose();
    }
}
