using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace CoAInstaller
{
    /// <summary>Window frame: header, page area and footer navigation.</summary>
    class MainForm : Form
    {
        readonly Panel content = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Surface };
        readonly Label headerTitle = new Label { ForeColor = Color.White, Font = Ui.Face(17f, true), AutoSize = true, Location = new Point(28, 10) };
        readonly Label headerTagline = new Label { ForeColor = Color.FromArgb(214, 206, 245), Font = Ui.Face(10.5f, false, FontStyle.Italic), AutoSize = true, Location = new Point(160, 20) };
        readonly Label headerSub = new Label { ForeColor = Color.FromArgb(150, 145, 175), Font = Ui.Face(8.5f), AutoSize = true, Location = new Point(30, 50) };
        readonly Button back = Ui.Secondary(L.T("Back"));
        readonly Button next = Ui.Primary(L.T("Next"));
        readonly Button close = Ui.Secondary(L.T("Close"));
        readonly List<Page> history = new List<Page>();
        readonly ThemeButton themeButton = new ThemeButton();
        readonly System.Windows.Forms.Timer themeFollow = new System.Windows.Forms.Timer { Interval = 4000 };
        Panel footer;

        // Wizard state
        public Install Target;
        public string DbPassword;
        public string RealmName;    // server name chosen for a new installation
        public int DbPort = 3307;
        public string EngineArgs;   // extra engine arguments of the current run (e.g. the backup to restore)
        public bool CreateShortcut = true;

        public MainForm()
        {
            Text = Product.WindowTitle;
            Font = Ui.Base;
            var area = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(Math.Min(1120, area.Width - 40), Math.Min(760, area.Height - 60));
            MinimumSize = new Size(Math.Min(980, area.Width - 40), Math.Min(640, area.Height - 40));
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Ui.Surface;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var header = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = Ui.Header };
            header.Controls.Add(headerTitle); header.Controls.Add(headerTagline); header.Controls.Add(headerSub);
            // Logo left of the title (embedded resource; the window works without it).
            try
            {
                var logoStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("CoAInstaller.logo.png");
                if (logoStream != null)
                {
                    header.Controls.Add(new PictureBox { Image = Image.FromStream(logoStream), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(52, 52), Location = new Point(18, 11), BackColor = Ui.Header });
                    headerTitle.Location = new Point(82, 10);
                    headerSub.Location = new Point(84, 50);
                }
            }
            catch { }
            // Product name first, tagline beside it, the project it builds in small print below.
            headerTitle.Text = Product.Name;
            headerTagline.Text = L.T("build, run and tweak your own server the lazy way");
            headerSub.Text = Product.BuildsFor;
            headerTitle.SizeChanged += (s, e) => headerTagline.Left = headerTitle.Right + 10;
            headerTagline.Left = headerTitle.Right + 10;

            // The language of the window and its look (light or dark), at the right of the header on every page.
            var language = new LanguageButton { Anchor = AnchorStyles.Top | AnchorStyles.Right };
            header.Controls.Add(language);
            header.Controls.Add(themeButton);
            Action placeHeaderButtons = () =>
            {
                language.Location = new Point(header.Width - language.Width - 22, 22);
                themeButton.Location = new Point(language.Left - themeButton.Width - 8, 22);
            };
            header.Resize += (s, e) => placeHeaderButtons();
            placeHeaderButtons();
            language.Click += (s, e) => LanguageMenu(language);
            themeButton.Click += (s, e) => ThemeMenu(themeButton);
            var themeTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 300 };
            themeTip.SetToolTip(themeButton, L.T("Theme"));

            // Follows Windows while the look is "automatic", without a new start.
            themeFollow.Tick += (s, e) => Theme.FollowWindows();
            themeFollow.Start();
            FormClosed += (s, e) => themeFollow.Stop();

            footer = new Panel { Dock = DockStyle.Bottom, Height = 62, BackColor = Ui.Panel };

            var right = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(0, 12, 20, 0), WrapContents = false, BackColor = Ui.Panel };
            right.Controls.Add(close); right.Controls.Add(next); right.Controls.Add(back);
            footer.Controls.Add(right);
            var line = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Ui.Line };
            footer.Controls.Add(line);
            footer.Controls.SetChildIndex(line, footer.Controls.Count - 1);

            Controls.Add(content); Controls.Add(footer); Controls.Add(header);
            back.Click += (s, e) => GoBack();
            next.Click += (s, e) => { var p = Current; if (p != null && p.OnNext()) { } };
            close.Click += (s, e) => Close();
            FormClosing += (s, e) =>
            {
                var pp = Current as ProgressPage;
                if (pp != null && pp.Running && !pp.ConfirmCancel()) e.Cancel = true;
            };

            string last = Settings.LastInstall;
            if (!string.IsNullOrEmpty(last) && new Install(last).IsInstalled) { Target = new Install(last); Navigate(new ManagerPage(this)); }
            else Navigate(new WelcomePage(this));
        }

        public Page Current { get { return history.Count > 0 ? history[history.Count - 1] : null; } }

        /// <summary>The languages there are, "automatic" (the one of Windows) and adding a language of one's own.</summary>
        void LanguageMenu(Control at)
        {
            var menu = new ContextMenuStrip { Font = Ui.Base, ShowImageMargin = false, BackColor = Ui.Surface, ForeColor = Ui.Text };
            string choice = L.Choice;
            var languages = L.Languages();
            var windows = languages.FirstOrDefault(l => l.Code == L.FromWindows());
            var auto = new ToolStripMenuItem((choice == L.Auto ? "✓  " : "     ") + L.F("Automatic (as Windows: {0})", windows != null ? windows.Name : "English"));
            auto.Click += (s, e) => SwitchLanguage(L.Auto);
            menu.Items.Add(auto);
            menu.Items.Add(new ToolStripSeparator());
            foreach (var lang in languages)
            {
                string code = lang.Code;
                string label = lang.Name + (lang.English != null && lang.English.Length > 0 && lang.English != lang.Name ? "  (" + lang.English + ")" : "");
                var item = new ToolStripMenuItem((choice == code ? "✓  " : "     ") + label);
                item.Click += (s, e) => SwitchLanguage(code);
                menu.Items.Add(item);
            }
            menu.Items.Add(new ToolStripSeparator());
            var add = new ToolStripMenuItem("     " + L.T("Add a language …"));
            add.Click += (s, e) => AddLanguage();
            menu.Items.Add(add);
            foreach (ToolStripItem item in menu.Items) item.ForeColor = Ui.Text;
            menu.Show(at, new Point(0, at.Height));
        }

        /// <summary>Light, dark, or following Windows. The window changes its look at once.</summary>
        void ThemeMenu(Control at)
        {
            var menu = new ContextMenuStrip { Font = Ui.Base, ShowImageMargin = false, BackColor = Ui.Surface, ForeColor = Ui.Text };
            string choice = Theme.Choice;
            string windows = Theme.IsDark(Theme.Automatic) ? L.T("Dark") : L.T("Light");
            var auto = new ToolStripMenuItem((choice == Theme.Automatic ? "✓  " : "     ") +
                L.F("Automatic (as Windows: {0})", windows));
            auto.Click += (s, e) => SwitchTheme(Theme.Automatic);
            menu.Items.Add(auto);
            menu.Items.Add(new ToolStripSeparator());
            var light = new ToolStripMenuItem((choice == Theme.Light ? "✓  " : "     ") + L.T("Light"));
            light.Click += (s, e) => SwitchTheme(Theme.Light);
            menu.Items.Add(light);
            var dark = new ToolStripMenuItem((choice == Theme.Dark ? "✓  " : "     ") + L.T("Dark"));
            dark.Click += (s, e) => SwitchTheme(Theme.Dark);
            menu.Items.Add(dark);
            foreach (ToolStripItem item in menu.Items) item.ForeColor = Ui.Text;
            menu.Show(at, new Point(0, at.Height));
        }

        void SwitchTheme(string choice)
        {
            if (choice == Theme.Choice) return;
            Theme.Set(choice);
        }

        void SwitchLanguage(string choice)
        {
            if (choice == L.Choice) return;
            var pp = Current as ProgressPage;
            if (pp != null && pp.Running) { Ui.Error(this, L.T("The language can be changed once the current task is finished.")); return; }
            L.Choice = choice;
            string now = choice == L.Auto ? L.FromWindows() : choice;
            if (now == L.Code) return;
            // Fonts and every text are made when a window opens: AFK Realm opens again in the new language.
            // The servers are processes of their own and keep running.
            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, Program.RestartArgument) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath) });
                Environment.Exit(0);
            }
            catch (Exception ex) { Ui.Error(this, ex.Message); }
        }

        /// <summary>A file with every text of AFK Realm for a new language, in the user's language folder.</summary>
        void AddLanguage()
        {
            string code = Prompt.Ask(this, L.T("Add a language"), L.T("Code of the language (for example ja, ko, it, pl, tr):"), "");
            if (code == null) return;
            code = code.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})?$")) { Ui.Error(this, L.T("Please use a language code such as ja, ko or pt-PT.")); return; }
            if (L.Languages().Any(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase) && l.BuiltIn)) { Ui.Error(this, L.T("AFK Realm has this language already.")); return; }
            string file;
            try { file = L.WriteTemplate(code); }
            catch (Exception ex) { Ui.Error(this, ex.Message); return; }
            MessageBox.Show(this, L.F("A file for the new language is ready:\n{0}\n\nWrite the translation of each English text after its colon, save the file, " +
                "then choose the language here. Texts left empty stay English. Sharing the file on AFK Realm's GitHub page lets everybody use it.", file),
                Product.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            try { Process.Start("explorer.exe", "/select,\"" + file + "\""); } catch { }
        }

        public void Navigate(Page page, bool replaceHistory = false)
        {
            if (replaceHistory) history.Clear();
            history.Add(page);
            Display(page);
        }
        void GoBack()
        {
            if (history.Count < 2) return;
            history.RemoveAt(history.Count - 1);
            Display(Current);
        }
        void Display(Page page)
        {
            content.SuspendLayout();
            content.Controls.Clear();
            content.Controls.Add(page);
            Restyle.Apply(page);
            content.ResumeLayout();
            RefreshButtons();
            page.OnShown();
        }
        public void RefreshButtons()
        {
            var p = Current;
            if (p == null) return;
            footer.Visible = p.ShowsFooter;
            back.Visible = p.CanGoBack && history.Count > 1;
            next.Visible = p.NextText != null;
            if (p.NextText != null) next.Text = p.NextText;
            var pp = p as ProgressPage;
            close.Text = pp != null && pp.Running ? L.T("Cancel") : L.T("Close");
        }

        /// <summary>Runs work on a background thread while the window shows a busy state.</summary>
        public void RunBusy(Control statusTarget, Action<Action<string>> work, Action<Exception> done)
        {
            UseWaitCursor = true;
            Enabled = false;
            Action<string> status = t => BeginInvoke((Action)(() => statusTarget.Text = t));
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Exception error = null;
                try { work(status); } catch (Exception ex) { error = ex; }
                BeginInvoke((Action)(() => { Enabled = true; UseWaitCursor = false; done(error); }));
            });
        }
    }
}

namespace CoAInstaller
{
    /// <summary>The language of the window, at the top right: a globe and the language's own name.</summary>
    class LanguageButton : Control
    {
        bool hover;
        public LanguageButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Ui.Header; Cursor = Cursors.Hand; Font = Ui.Small;
            var current = L.Languages().FirstOrDefault(l => l.Code == L.Code);
            Text = current != null ? current.Name : "English";
            Size = new Size(TextRenderer.MeasureText(Text, Font).Width + 52, 30);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Ui.Header); Draw.Smooth(g);
            using (var p = Draw.Round(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 8))
            using (var b = new SolidBrush(hover ? Ui.SidebarActive : Ui.SidebarHover)) g.FillPath(b, p);
            Icons.Paint(g, "globe", new RectangleF(9, 7, 16, 16), Color.FromArgb(214, 206, 245), 1.4f);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(30, 0, Width - 44, Height), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            using (var pen = new Pen(Color.FromArgb(196, 190, 220), 1.4f))
            {
                float x = Width - 14, y = Height / 2f;
                g.DrawLines(pen, new[] { new PointF(x - 3, y - 2), new PointF(x, y + 1), new PointF(x + 3, y - 2) });
            }
        }
    }

    /// <summary>The look of the window, beside the language in the header: a sun or a moon. A click offers
    /// light, dark and automatic.</summary>
    class ThemeButton : Control
    {
        bool hover;
        public ThemeButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Ui.Header; Cursor = Cursors.Hand;
            Size = new Size(30, 30);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Ui.Header); Draw.Smooth(g);
            using (var p = Draw.Round(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 8))
            using (var b = new SolidBrush(hover ? Ui.SidebarActive : Ui.SidebarHover)) g.FillPath(b, p);
            Icons.Paint(g, ReferenceEquals(Theme.Current, Theme.DarkLook) ? "moon" : "sun", new RectangleF(7, 7, 16, 16), Color.FromArgb(214, 206, 245), 1.4f);
        }
    }
}
