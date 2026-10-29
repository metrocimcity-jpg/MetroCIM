using System.Windows.Forms;

namespace MetroCIM.Commands;

internal sealed class ExportProgress : IDisposable
{
    private readonly Form _form;
    private readonly ProgressBar _bar;
    private readonly Label _label;
    private int _lastPumpTick;

    public ExportProgress(string title, int total)
    {
        _form = new Form
        {
            Text = title,
            Width = 460,
            Height = 120,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            TopMost = true
        };

        _label = new Label
        {
            Dock = DockStyle.Top,
            Height = 36,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 8, 12, 0),
            Text = "Starting export..."
        };

        _bar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = Math.Max(total, 1),
            Style = ProgressBarStyle.Continuous
        };

        _form.Controls.Add(_bar);
        _form.Controls.Add(_label);
        _form.Show();
        _form.Refresh();
        Application.DoEvents();
    }

    public void UseMarquee(string status)
    {
        _bar.Style = ProgressBarStyle.Marquee;
        _bar.MarqueeAnimationSpeed = 40;
        _label.Text = status;
        _form.Refresh();
        Application.DoEvents();
    }

    public void Pump()
    {
        _form.Refresh();
        Application.DoEvents();
    }

    public void Report(int current, string status, int total = 0)
    {
        if (total > 0 && _bar.Maximum != Math.Max(total, 1))
            _bar.Maximum = Math.Max(total, 1);

        _bar.Value = Math.Clamp(current, 0, _bar.Maximum);
        _label.Text = total > 0
            ? $"{status}  ({current}/{total})"
            : status;

        int now = Environment.TickCount;
        if (unchecked(now - _lastPumpTick) >= 250 || current == 0 || (total > 0 && current >= total))
        {
            _lastPumpTick = now;
            _form.Refresh();
            Application.DoEvents();
        }
    }

    public void Dispose()
    {
        _form.Close();
        _form.Dispose();
    }
}
