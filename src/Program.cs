using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace CoAInstaller
{
    static class Program
    {
        /// <summary>A start that follows the end of the last one (the language was changed): it waits for the old one to close.</summary>
        public const string RestartArgument = "--restart";

        [STAThread]
        static int Main(string[] args)
        {
            L.Init();
            Theme.Init();
            // Installing Visual Studio, firewall rules and services needs administrator rights.
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(Application.ExecutablePath, string.Join(" ", args)) { UseShellExecute = true, Verb = "runas" });
                }
                catch
                {
                    MessageBox.Show(L.F("{0} needs administrator rights (for Visual Studio, the firewall and the database). Please confirm the Windows prompt.", Product.Name),
                        Product.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return 0;
            }
            bool created;
            using (var single = new Mutex(true, Product.FileStem + ".SingleInstance", out created))
            {
                // After an update the old version needs a moment to close.
                if (!created && (Array.IndexOf(args, SelfUpdate.RestartArgument) >= 0 || Array.IndexOf(args, RestartArgument) >= 0))
                {
                    try { created = single.WaitOne(15000); } catch (AbandonedMutexException) { created = true; }
                }
                if (!created)
                {
                    MessageBox.Show(L.F("{0} is already open.", Product.Name), Product.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                SelfUpdate.JustUpdated = Array.IndexOf(args, SelfUpdate.RestartArgument) >= 0;
                SelfUpdate.CleanUp();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => MessageBox.Show(L.T("Unexpected error:") + "\n" + e.Exception.Message, Product.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Run(new MainForm());
            }
            return 0;
        }
    }
}
