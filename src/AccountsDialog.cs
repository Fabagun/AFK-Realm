using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace CoAInstaller
{
    /// <summary>Lists the accounts of real players (bot accounts are left out) and lets you delete them, reset passwords and change access levels.</summary>
    class AccountsDialog : AfkForm
    {
        static readonly string[] Levels = { L.T("Player"), L.T("Moderator (GM 1)"), L.T("Game Master (GM 2)"), L.T("Administrator (GM 3)") };

        readonly Install inst;
        readonly ServerControl ctl;
        readonly ListView list = new ListView { View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, Dock = DockStyle.Fill, Font = Ui.Base, BorderStyle = BorderStyle.FixedSingle };
        readonly Label status = new Label { AutoSize = true, Font = Ui.Base, ForeColor = Ui.Muted, Margin = new Padding(0, 10, 16, 0) };
        readonly Button delete = Ui.Secondary(L.T("Delete account …"));
        readonly Button password = Ui.Secondary(L.T("New password …"));
        readonly ComboBox level = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Font = Ui.Base, Margin = new Padding(16, 4, 4, 0) };
        readonly Button applyLevel = Ui.Secondary(L.T("Set access level"));
        readonly Button export = Ui.Secondary(L.T("Export …"));
        readonly List<Control> actions = new List<Control>();

        public AccountsDialog(Install i, ServerControl c)
        {
            inst = i; ctl = c;
            Text = Product.Name + " – " + L.T("Player accounts"); Font = Ui.Base; BackColor = Color.White; ShowIcon = false;
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(900, 560); MinimumSize = new Size(760, 420);

            var top = new Panel { Dock = DockStyle.Top, Height = 64, Padding = new Padding(16, 12, 16, 0) };
            top.Controls.Add(new Label { Dock = DockStyle.Fill, Font = Ui.Base, ForeColor = Ui.Muted,
                Text = L.T("Accounts of real players. The Playerbots accounts are not listed and are managed with \"Reset random bots\".") + "\r\n" +
                       L.T("Select an account to delete it, give it a new password, change its access level or export it for another server.") });

            list.Columns.Add(L.T("Account"), 150);
            list.Columns.Add(L.T("Access level"), 170);
            list.Columns.Add(L.T("Characters"), 320);
            list.Columns.Add(L.T("Last login"), 140);
            list.Columns.Add(L.T("Online"), 70);
            var listPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 4, 16, 8) };
            listPanel.Controls.Add(list);

            var actionRow = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(14, 6, 14, 6), WrapContents = false };
            foreach (var l in Levels) level.Items.Add(l);
            actionRow.Controls.Add(delete);
            actionRow.Controls.Add(password);
            actionRow.Controls.Add(level);
            actionRow.Controls.Add(applyLevel);
            export.Margin = new Padding(16, 3, 3, 3);
            actionRow.Controls.Add(export);
            actions.AddRange(new Control[] { delete, password, level, applyLevel, export });

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(14, 10, 14, 10), FlowDirection = FlowDirection.RightToLeft, BackColor = Ui.Panel, WrapContents = false };
            var close = Ui.Secondary(L.T("Close")); close.Click += (s, e) => Close();
            var refresh = Ui.Secondary(L.T("Refresh")); refresh.Click += (s, e) => Reload(null);
            var import = Ui.Secondary(L.T("Import account file …")); import.Click += (s, e) => ImportFile();
            bottom.Controls.Add(close); bottom.Controls.Add(refresh); bottom.Controls.Add(import); bottom.Controls.Add(status);

            Controls.Add(listPanel);
            Controls.Add(actionRow);
            Controls.Add(bottom);
            Controls.Add(top);

            list.SelectedIndexChanged += (s, e) => UpdateActions();
            delete.Click += (s, e) => DeleteSelected();
            password.Click += (s, e) => NewPassword();
            applyLevel.Click += (s, e) => SetLevel();
            export.Click += (s, e) => ExportSelected();
            Shown += (s, e) => Reload(null);
            UpdateActions();
        }

        Accounts.Info Selected { get { return list.SelectedItems.Count > 0 ? (Accounts.Info)list.SelectedItems[0].Tag : null; } }

        void UpdateActions()
        {
            var a = Selected;
            foreach (var c in actions) c.Enabled = a != null;
            if (a != null) level.SelectedIndex = Math.Max(0, Math.Min(3, a.Level));
        }

        /// <summary>Runs database work in the background; the window stays responsive.</summary>
        void Run(string what, Action work, Action<Exception> done)
        {
            UseWaitCursor = true; Enabled = false; status.Text = what; status.ForeColor = Ui.Muted;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Exception error = null;
                try { if (ctl.Db == null) ctl.StartDatabase(t => { }); work(); } catch (Exception ex) { error = ex; }
                try { BeginInvoke((Action)(() => { Enabled = true; UseWaitCursor = false; done(error); })); } catch { }
            });
        }

        void Reload(string message)
        {
            List<Accounts.Info> accounts = null;
            Run(L.T("Loading accounts …"), () => accounts = Accounts.ListPlayers(inst), err =>
            {
                if (err != null) { status.Text = ""; Ui.Error(this, L.T("The accounts could not be loaded:") + "\n" + err.Message); return; }
                int keep = Selected != null ? Selected.Id : -1;
                list.BeginUpdate(); list.Items.Clear();
                foreach (var a in accounts)
                {
                    var item = new ListViewItem(new[] { a.Name, Levels[Math.Max(0, Math.Min(3, a.Level))],
                        a.CharacterCount == 0 ? "–" : a.Characters + (a.CharacterCount > 1 ? "  (" + a.CharacterCount + ")" : ""),
                        a.LastLogin.Length > 0 ? a.LastLogin : L.T("never"), a.Online ? L.T("yes") : "" }) { Tag = a };
                    if (a.Online) item.ForeColor = Ui.Ok;
                    list.Items.Add(item);
                    if (a.Id == keep) item.Selected = true;
                }
                list.EndUpdate();
                status.ForeColor = message != null ? Ui.Ok : Ui.Muted;
                status.Text = message ?? (accounts.Count == 1 ? L.T("1 player account") : L.F("{0} player accounts", accounts.Count));
                UpdateActions();
            });
        }

        void DeleteSelected()
        {
            var a = Selected; if (a == null) return;
            if (ctl.World != null) { Ui.Error(this, L.T("Stop the server first. Accounts can only be deleted while the worldserver is not running, so no character is still in use.")); return; }
            string chars = a.CharacterCount == 0 ? L.T("It has no characters.") :
                a.CharacterCount == 1 ? L.F("Its character ({0}) will be deleted as well.", a.Characters)
                : L.F("Its {0} characters ({1}) will be deleted as well.", a.CharacterCount, a.Characters);
            string purge = Accounts.PurgesDeletedCharacters(inst)
                ? L.T("The worldserver removes the characters' remaining data (items, mail, guild membership …) at its next start.")
                : L.T("Note: CharDelete.KeepDays is 0 in your settings, so the worldserver never purges deleted characters. They stay hidden in the database.");
            if (!Ui.Confirm(this, L.F("Delete the account \"{0}\"?", a.Name) + "\n\n" + chars + "\n" + purge + "\n\n" + L.T("This cannot be undone."))) return;
            Run(L.F("Deleting {0} …", a.Name), () => Accounts.Delete(inst, a.Id), err =>
            {
                if (err != null) { status.Text = ""; Ui.Error(this, L.T("The account could not be deleted:") + "\n" + err.Message); return; }
                Reload(L.F("Account {0} was deleted.", a.Name));
            });
        }

        void NewPassword()
        {
            var a = Selected; if (a == null) return;
            string pw;
            using (var d = new PasswordPrompt(a.Name)) { if (d.ShowDialog(this) != DialogResult.OK) return; pw = d.Password; }
            Run(L.T("Setting the password …"), () => Accounts.SetPassword(inst, a.Id, a.Name, pw), err =>
            {
                if (err != null) { status.Text = ""; Ui.Error(this, L.T("The password could not be changed:") + "\n" + err.Message); return; }
                Reload(L.F("New password set for {0}.", a.Name));
            });
        }

        void SetLevel()
        {
            var a = Selected; if (a == null) return;
            int lvl = level.SelectedIndex;
            Run(L.T("Changing the access level …"), () => Accounts.SetLevel(inst, a.Id, lvl), err =>
            {
                if (err != null) { status.Text = ""; Ui.Error(this, L.T("The access level could not be changed:") + "\n" + err.Message); return; }
                Reload(a.Online ? L.F("{0} is now: {1} (applies at the next login).", a.Name, Levels[lvl]) : L.F("{0} is now: {1}.", a.Name, Levels[lvl]));
            });
        }

        void ExportSelected()
        {
            var a = Selected; if (a == null) return;
            if (ctl.World != null && !Ui.Confirm(this, L.T("The server is running. Characters that are online right now are exported as they were last saved.") + "\n\n" + L.T("Export anyway?"))) return;
            string file;
            using (var d = new SaveFileDialog { Title = L.F("Export {0}", a.Name), FileName = a.Name + AccountTransfer.Extension,
                Filter = L.F("{0} account", Product.Name) + " (*" + AccountTransfer.Extension + ")|*" + AccountTransfer.Extension,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) })
            { if (d.ShowDialog(this) != DialogResult.OK) return; file = d.FileName; }
            AccountTransfer.Summary sum = null;
            Run(L.F("Exporting {0} …", a.Name), () => sum = AccountTransfer.Export(inst, a.Name, file, t => BeginInvoke((Action)(() => status.Text = t))), err =>
            {
                if (err != null) { status.Text = ""; Ui.Error(this, L.T("The account could not be exported:") + "\n" + err.Message); return; }
                status.ForeColor = Ui.Ok; status.Text = L.F("{0} was exported.", a.Name);
                MessageBox.Show(this, L.F("{0} was exported to", a.Name) + "\n" + file + "\n\n" +
                    (sum.Characters.Count == 0 ? L.T("The account has no characters.") : L.T("Characters:") + " " + string.Join(", ", sum.Characters) + "\n" + L.T("Items:") + " " + sum.Items) +
                    "\n\n" + L.T("On the new server, open \"Manage player accounts\" and choose \"Import account file\"."),
                    Product.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        void ImportFile()
        {
            if (ctl.World != null) { Ui.Error(this, L.T("Stop the server first. Characters can only be imported while the worldserver is not running.")); return; }
            string file;
            using (var d = new OpenFileDialog { Title = L.T("Import an account"), Filter = L.F("{0} account", Product.Name) + " (*" + AccountTransfer.Extension + ")|*" + AccountTransfer.Extension,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) })
            { if (d.ShowDialog(this) != DialogResult.OK) return; file = d.FileName; }
            Dictionary<string, string> m;
            try { m = AccountTransfer.ReadManifest(file); }
            catch (Exception ex) { Ui.Error(this, ex.Message); return; }
            string chars; m.TryGetValue("characters", out chars);
            string created; m.TryGetValue("created", out created);
            if (!Ui.Confirm(this, L.F("Import the account {0}?", m["accounts"]) + "\n\n" +
                L.T("Characters:") + " " + (string.IsNullOrEmpty(chars) ? L.T("none") : chars) + "\n" + L.T("Exported:") + " " + created + "\n\n" +
                L.T("If an account with this name exists here, the characters are added to it and its password becomes the one from the file. " +
                "Guild, group and arena team memberships are not carried over."))) return;
            AccountTransfer.Summary sum = null;
            Run(L.T("Importing …"), () => sum = AccountTransfer.Import(inst, file, t => BeginInvoke((Action)(() => status.Text = t))), err =>
            {
                if (err != null) { status.Text = ""; Ui.Error(this, L.T("The account could not be imported:") + "\n" + err.Message); return; }
                Reload(L.F("Imported {0}.", m["accounts"]));
                MessageBox.Show(this, L.T("Import finished.") + "\n\n" + L.T("Account:") + " " + string.Join(", ", sum.Accounts) +
                    "\n" + L.T("Characters:") + "\n  " + (sum.Characters.Count == 0 ? L.T("none") : string.Join("\n  ", sum.Characters)) +
                    "\n" + L.T("Items:") + " " + sum.Items, Product.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        /// <summary>Asks for a new password twice, with the same rules as account creation.</summary>
        class PasswordPrompt : AfkForm
        {
            readonly TextBox pw1 = Ui.Input(220, true), pw2 = Ui.Input(220, true);
            readonly string user;
            public string Password { get { return pw1.Text; } }
            public PasswordPrompt(string account)
            {
                user = account;
                Text = L.F("New password for {0}", account); Font = Ui.Base; BackColor = Color.White; ShowIcon = false;
                FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
                StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(440, 190);
                var col = Ui.Column(); col.Padding = new Padding(20); col.Dock = DockStyle.Fill;
                var r1 = Ui.Row(); r1.Controls.Add(Ui.FieldLabel(L.T("New password"), 150)); r1.Controls.Add(pw1); col.Controls.Add(r1);
                var r2 = Ui.Row(); r2.Controls.Add(Ui.FieldLabel(L.T("Repeat password"), 150)); r2.Controls.Add(pw2); col.Controls.Add(r2);
                var ok = Ui.Primary(L.T("Set password")); ok.Margin = new Padding(0, 14, 0, 0);
                ok.Click += (s, e) =>
                {
                    string err = Accounts.Validate("account", pw1.Text, pw2.Text);  // only the password rules matter here
                    if (err != null) { Ui.Error(this, err); return; }
                    DialogResult = DialogResult.OK;
                };
                col.Controls.Add(ok);
                AcceptButton = ok;
                Controls.Add(col);
            }
        }
    }
}
