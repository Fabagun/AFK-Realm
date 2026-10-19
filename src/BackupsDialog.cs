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
    /// update; here you can make one by hand, roll the server back to one, delete one, and set how many
    /// backups are kept.
    /// </summary>
    class BackupsDialog : AfkForm
    {
        public bool BackUpNow { get; private set; }
        public string RestoreName { get; private set; }
        /// <summary>Only parts of a backup: its folder (a name in Backups, or the full path of a backup of another server) and the parts.</summary>
        public string PartsFolder { get; private set; }
        public string Parts { get; private set; }

        readonly Install inst;
        readonly ListView list = new ListView { View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, Dock = DockStyle.Fill, Font = Ui.Base, BorderStyle = BorderStyle.FixedSingle };
        readonly Button restore = Ui.Primary(L.T("Restore …")), parts = Ui.Secondary(L.T("Restore only parts …")), delete = Ui.Secondary(L.T("Delete"));
        string Folder { get { return Path.Combine(inst.Root, "Backups"); } }

        class Snapshot { public string Name, Created, Reason, Core, Bots; public long Bytes; }

        public BackupsDialog(Install i)
        {
            inst = i;
            Text = Product.Name + " – " + L.T("Backups"); Font = Ui.Base; BackColor = Ui.Surface; ShowIcon = false;
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(980, 470); MinimumSize = new Size(900, 380);

            var top = new Panel { Dock = DockStyle.Top, Height = 104, Padding = new Padding(16, 12, 16, 6) };
            var intro = new Label { Dock = DockStyle.Top, Height = 48, Font = Ui.Base, ForeColor = Ui.Muted,
                Text = L.F("Before every update {0} saves the server: programs, settings, all databases and the versions they were built from. " +
                       "If an update causes problems, restore the backup made before it.", Product.Name) };
            // How many snapshots are kept; the engine reads the same number (Dependencies\backup-settings.txt).
            var keepRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, WrapContents = false };
            keepRow.Controls.Add(new Label { Text = L.T("Backups to keep:"), Font = Ui.Base, ForeColor = Ui.Text, AutoSize = true, Margin = new Padding(0, 7, 6, 0) });
            var keep = new NumericUpDown { Minimum = BackupSettings.Min, Maximum = BackupSettings.Max, Value = BackupSettings.Load(inst.Root),
                Width = 56, Font = Ui.Base, TextAlign = HorizontalAlignment.Center, Margin = new Padding(0, 3, 10, 0) };
            keepRow.Controls.Add(keep);
            keepRow.Controls.Add(new Label { Text = L.T("Older ones are removed with the next backup."), Font = Ui.Small, ForeColor = Ui.Muted, AutoSize = true, Margin = new Padding(0, 10, 0, 0) });
            keep.ValueChanged += (s, e) => { try { BackupSettings.Save(inst.Root, (int)keep.Value); } catch (Exception ex) { Ui.Error(this, L.T("The setting could not be saved:") + "\n" + ex.Message); } };
            top.Controls.Add(keepRow);
            top.Controls.Add(intro);

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
            var other = Ui.Secondary(L.T("From another server …"));
            other.Click += (s, e) => FromOtherServer();
            bottom.Controls.Add(close); bottom.Controls.Add(openFolder); bottom.Controls.Add(delete); bottom.Controls.Add(other); bottom.Controls.Add(parts); bottom.Controls.Add(restore); bottom.Controls.Add(now);

            Controls.Add(listPanel);
            Controls.Add(bottom);
            Controls.Add(top);

            restore.Click += (s, e) => Restore();
            parts.Click += (s, e) => { var b = Selected; if (b != null) ChooseParts(b.Name, b.Created); };
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
        void UpdateButtons() { restore.Enabled = parts.Enabled = delete.Enabled = Selected != null; }

        /// <summary>A backup folder of another server (copied here or on another drive): only parts of it can be taken.</summary>
        void FromOtherServer()
        {
            using (var d = new FolderBrowserDialog { Description = L.T("The folder of a backup of another AFK Realm server (it holds manifest.txt)") })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                var b = Read(d.SelectedPath);
                if (b == null || !File.Exists(Path.Combine(d.SelectedPath, "server.zip")))
                {
                    Ui.Error(this, L.T("This folder is not a backup of AFK Realm. Choose the folder with the date as its name, the one that holds manifest.txt."));
                    return;
                }
                ChooseParts(d.SelectedPath, b.Created);
            }
        }

        /// <summary>Asks which parts to take from a backup.</summary>
        void ChooseParts(string folder, string created)
        {
            using (var f = new AfkForm { Text = L.T("Restore only parts"), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(18) })
            {
                var flow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
                Func<string, Label> text = t => new Label { Text = t, AutoSize = true, MaximumSize = new Size(560, 0), Font = Ui.Small, ForeColor = Ui.Muted, Margin = new Padding(22, 0, 0, 10) };
                flow.Controls.Add(new Label { Text = L.F("From the backup of {0}, take:", created), AutoSize = true, Font = Ui.Bold, ForeColor = Ui.Text, Margin = new Padding(0, 0, 0, 10) });
                var accounts = new CheckBox { Text = L.T("Accounts"), AutoSize = true, Font = Ui.Base };
                var characters = new CheckBox { Text = L.T("Characters"), AutoSize = true, Font = Ui.Base };
                var settings = new CheckBox { Text = L.T("Settings"), AutoSize = true, Font = Ui.Base };
                flow.Controls.Add(accounts);
                flow.Controls.Add(text(L.T("Every account with its password and GM rights. The realm list (name, address, port) stays that of this server.")));
                flow.Controls.Add(characters);
                flow.Controls.Add(text(L.T("Every character with everything it has - bags, bank, mail, guilds - and the Playerbots data. Characters belong to their accounts by number: take the accounts along, unless the backup comes from this very server.")));
                flow.Controls.Add(settings);
                flow.Controls.Add(text(L.T("The settings of the server and of the modules it has. The programs stay those of this server, and so do the database password and the folders.")));
                flow.Controls.Add(new Label { Text = L.T("What you take replaces what this server has now; everything else stays. The parts are brought up to this server's version afterwards, so a backup of an older server works too. The server is stopped for this."),
                    AutoSize = true, MaximumSize = new Size(580, 0), Font = Ui.Small, ForeColor = Ui.Warn, Margin = new Padding(0, 4, 0, 12) });
                var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0) };
                var ok = Ui.Primary(L.T("Restore")); var cancel = Ui.Secondary(L.T("Cancel"));
                cancel.Click += (s, e) => f.Close();
                ok.Click += (s, e) =>
                {
                    var chosen = new List<string>();
                    if (accounts.Checked) chosen.Add("accounts");
                    if (characters.Checked) chosen.Add("characters");
                    if (settings.Checked) chosen.Add("settings");
                    if (chosen.Count == 0) { Ui.Error(f, L.T("Choose at least one part.")); return; }
                    if (characters.Checked && !accounts.Checked && Path.IsPathRooted(folder) &&
                        !Ui.Confirm(f, L.T("The characters of another server belong to its accounts. Without them, characters whose account number this server does not have cannot be played. Restore only the characters anyway?"))) return;
                    PartsFolder = folder;
                    Parts = string.Join(",", chosen);
                    f.DialogResult = DialogResult.OK;
                };
                buttons.Controls.Add(cancel); buttons.Controls.Add(ok);
                flow.Controls.Add(buttons);
                f.Controls.Add(flow);
                f.AcceptButton = ok; f.CancelButton = cancel;
                if (f.ShowDialog(this) == DialogResult.OK) Close();
            }
        }

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
