using System;
using System.Globalization;
using System.IO;

namespace CoAInstaller
{
    /// <summary>
    /// How many server snapshots are kept before the oldest one is removed. AFK Realm writes the
    /// choice as "keep=&lt;number&gt;" to &lt;root&gt;\Dependencies\backup-settings.txt, and the engine
    /// reads the same file when it makes a backup. Without the file, three are kept.
    /// </summary>
    static class BackupSettings
    {
        public const int Min = 1, Max = 99, Default = 3;

        public static string FileOf(string root) { return Path.Combine(root, "Dependencies", "backup-settings.txt"); }

        /// <summary>The saved number, or the default when the file is missing or holds none.</summary>
        public static int Load(string root)
        {
            try
            {
                foreach (var line in File.ReadAllLines(FileOf(root)))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0 || line.Substring(0, eq).Trim() != "keep") continue;
                    int value;
                    if (int.TryParse(line.Substring(eq + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                        && value >= Min && value <= Max)
                        return value;
                }
            }
            catch { }
            return Default;
        }

        public static void Save(string root, int keep)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FileOf(root)));
            File.WriteAllText(FileOf(root), "keep=" + Clamp(keep).ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
        }

        static int Clamp(int keep) { return Math.Max(Min, Math.Min(Max, keep)); }
    }
}
