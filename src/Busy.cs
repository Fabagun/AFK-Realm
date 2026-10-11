using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace CoAInstaller
{
    /// <summary>
    /// Work that takes a while (reading the game client, building the client patch, writing race archives) runs in the
    /// background while a small window says what is going on, with a moving bar, so that AFK Realm never looks frozen.
    /// </summary>
    static class Busy
    {
        class Window : AfkForm
        {
            public readonly Label Text_ = new Label { AutoSize = false, Dock = DockStyle.Top, Height = 46, Font = Ui.Base, ForeColor = Ui.Text, TextAlign = ContentAlignment.MiddleLeft };
            public readonly ProgressBar Bar = new ProgressBar { Dock = DockStyle.Top, Height = 14, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };
            public bool Done;
            public Window(string title, string text)
            {
                Text = title; FormBorderStyle = FormBorderStyle.FixedDialog; ControlBox = false; MaximizeBox = MinimizeBox = false;
                StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(460, 104); ShowInTaskbar = false;
                var pad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 16, 20, 16), BackColor = Ui.Surface };
                Text_.Text = text;
                pad.Controls.Add(Bar); pad.Controls.Add(Text_);
                Controls.Add(pad);
                FormClosing += (s, e) => { if (!Done) e.Cancel = true; };
            }
            /// <summary>A step out of a number of steps: the bar fills; without a number it keeps moving.</summary>
            public void Show(string text, int step, int steps)
            {
                Text_.Text = text;
                if (steps > 0)
                {
                    Bar.Style = ProgressBarStyle.Continuous;
                    Bar.Maximum = steps;
                    Bar.Value = Math.Max(0, Math.Min(steps, step));
                }
                else Bar.Style = ProgressBarStyle.Marquee;
            }
        }

        /// <summary>A report of the work: a text, and optionally the step it is at out of how many (0: unknown).</summary>
        public delegate void Report(string text, int step = 0, int steps = 0);

        public static T Run<T>(IWin32Window owner, string text, Func<Report, T> work)
        {
            T result = default(T);
            Exception error = null;
            using (var w = new Window(Product.Name, text))
            {
                Report report = (t, step, steps) =>
                {
                    try { if (w.IsHandleCreated) w.BeginInvoke((Action)(() => w.Show(t, step, steps))); } catch { }
                };
                w.Shown += (s, e) => ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { result = work(report); }
                    catch (Exception ex) { error = ex; }
                    try { w.BeginInvoke((Action)(() => { w.Done = true; w.Close(); })); } catch { }
                });
                w.ShowDialog(owner);
            }
            if (error != null)
            {
                if (error is InvalidOperationException) throw new InvalidOperationException(error.Message, error);
                throw new Exception(error.Message, error);
            }
            return result;
        }

        public static void Run(IWin32Window owner, string text, Action<Report> work)
        {
            Run<int>(owner, text, r => { work(r); return 0; });
        }
    }
}
