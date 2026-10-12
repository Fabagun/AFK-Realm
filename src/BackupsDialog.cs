using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CoAInstaller
{
    /// <summary>
    /// Lists the server snapshots in &lt;root&gt;\Backups. The engine creates one automatically before every
    /// update; here you can make one by hand, roll the server back to one, or delete one.
    /// </summary>
    class BackupsDialog : AfkForm
    {
        public bool BackUpNow { get; private set; }
        public string RestoreName { get; private set; }

        readonly Install inst;
        readonly ListView list = new ListView { View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, Dock = DockStyle.Fill, Font = Ui.Base, BorderStyle = BorderStyle.FixedSingle };
        readonly Button restore = Ui.Primary(L.T("Restore …")), delete = Ui.Secondary(L.T("Delete"));
        string Folder { get { return Path.Combine(inst.Root, "Backups"); } }

        class Snapshot { public string Name, Created, Reason, Core, Bots; public long Bytes; }

        public BackupsDialog(Install i)
        {
            inst = i;
            Text = Product.Name + " – " + L.T("Backups"); Font = Ui.Base; BackColor = Color.White; ShowIcon = false;
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(820, 470); MinimumSize = new Size(700, 380);

            var top = new Panel { Dock = DockStyle.Top, Height = 70, Padding = new Padding(16, 12, 16, 0) };
            top.Controls.Add(new Label { Dock = DockStyle.Fill, Font = Ui.Base, ForeColor = Ui.Muted,
                Text = L.F("Before every update {0} saves the server: programs, settings, all databases and the versions they were built from. " +
                       "If an update causes problems, restore the backup made before it. The newest 3 backups are kept.", Product.Name) });

            list.Columns.Add(L.T("Created"), 140);
            list.Columns.Add(L.T("Made"), 240);
            list.Columns.Add(L.T("CoA core"), 100);
            list.Columns.Add("Playerbots", 100);
            list.Columns.Add(L.T("Size"), 90);
            var listPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 4, 16, 8) };
            listPanel.Controls.Add(list);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(14, 10, 14, 10), FlowDirection = FlowDirection.RightToLeft, BackColor = Ui.Panel, WrapContents = false };
            var close = Ui.Secondary(L.T("Close")); close.Click += (s, e) => Close();
            var openFolder = Ui.Secondary(L.T("Open folder"));
            openFolder.Click += (s, e) => { Directory.CreateDirectory(Folder); Process.Start("explorer.exe", "\"" + Folder + "\""); };
            var now = Ui.Secondary(L.T("Back up now"));
            now.Click += (s, e) => { BackUpNow = true; Close(); };
            bottom.Controls.Add(close); bottom.Controls.Add(openFolder); bottom.Controls.Add(delete); bottom.Controls.Add(restore); bottom.Controls.Add(now);

            Controls.Add(listPanel);
            Controls.Add(bottom);
            Controls.Add(top);

            restore.Click += (s, e) => Restore();
            delete.Click += (s, e) => Delete();
            list.SelectedIndexChanged += (s, e) => UpdateButtons();
            Fill();
        }

        static Snapshot Read(string dir)
        {
            string manifest = Path.Combine(dir, "manifest.txt");
            if (!File.Exists(manifest)) return null;
            var v = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(manifest)) { int eq = line.IndexOf('='); if (eq > 0) v[line.Substring(0, eq)] = line.Substring(eq + 1); }
            Func<string, string> get = k => v.ContainsKey(k) ? v[k] : "";
            long bytes; long.TryParse(get("bytes"), out bytes);
            return new Snapshot { Name = Path.GetFileName(dir), Created = get("created"), Reason = get("reason"), Core = get("core"), Bots = get("playerbots"), Bytes = bytes };
        }

        static string Short(string rev) { return rev.Length >= 8 ? rev.Substring(0, 8) : (rev.Length > 0 ? rev : "–"); }
        static string Describe(string reason)
        {
            if (reason == "manual") return L.T("by hand");
            if (reason == "before update") return L.T("automatically, before update");
            if (reason == "before rebuild") return L.T("automatically, before rebuild");
            if (reason == "before module change") return L.T("automatically, before module change");
            return reason;
        }

        void Fill()
        {
            list.Items.Clear();
            if (Directory.Exists(Folder))
                foreach (var dir in Directory.GetDirectories(Folder).OrderByDescending(d => d, StringComparer.Ordinal))
                {
                    var b = Read(dir);
                    if (b == null) continue;
                    list.Items.Add(new ListViewItem(new[] { b.Created, Describe(b.Reason), Short(b.Core), Short(b.Bots),
                        (b.Bytes / 1048576.0).ToString("N0", CultureInfo.CurrentCulture) + " MB" }) { Tag = b });
                }
            if (list.Items.Count == 0) list.Items.Add(new ListViewItem(new[] { L.T("No backups yet"), "", "", "", "" }) { ForeColor = Ui.Muted });
            else list.Items[0].Selected = true;
            UpdateButtons();
        }

        Snapshot Selected { get { return list.SelectedItems.Count > 0 ? list.SelectedItems[0].Tag as Snapshot : null; } }
        void UpdateButtons() { restore.Enabled = delete.Enabled = Selected != null; }

        void Restore()
        {
            var b = Selected; if (b == null) return;
            if (!Ui.Confirm(this, L.F("Go back to the backup from {0}?", b.Created) + "\n\n" +
                L.T("The server programs, settings and all databases are replaced by the state of that moment. " +
                "Everything that happened on the server since then is lost: character progress, new characters and accounts, changed settings.") + "\n\n" +
                L.T("The server is stopped for this."))) return;
            RestoreName = b.Name;
            Close();
        }

        void Delete()
        {
            var b = Selected; if (b == null) return;
            if (!Ui.Confirm(this, L.F("Delete the backup from {0}?", b.Created))) return;
            try { Directory.Delete(Path.Combine(Folder, b.Name), true); }
            catch (Exception ex) { Ui.Error(this, L.T("The backup could not be deleted:") + "\n" + ex.Message); }
            Fill();
        }
    }
}
