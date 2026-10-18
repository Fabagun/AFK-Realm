using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CoAInstaller
{
    /// <summary>
    /// The look of AFK Realm: light, dark, or automatic (following Windows). The colours of Ui come from the
    /// palette kept here; a change is carried to every open window at once, without a new start. The choice
    /// lives in %APPDATA%\AFK-Realm\theme.txt, next to language.txt.
    /// </summary>
    static class Theme
    {
        public const string Automatic = "auto", Light = "light", Dark = "dark";

        /// <summary>The colours that differ between the light and the dark look (the accents are the same in both).</summary>
        public sealed class Palette
        {
            public Color Text, Muted, Canvas, Panel, Surface, Line, AccentSoft, AccentInk, ToggleOff, GridChanged, GridAdded;
        }

        public static readonly Palette LightLook = new Palette
        {
            Text = Color.FromArgb(33, 33, 40),
            Muted = Color.FromArgb(105, 105, 120),
            Canvas = Color.FromArgb(246, 245, 250),
            Panel = Color.FromArgb(246, 245, 250),
            Surface = Color.White,
            Line = Color.FromArgb(226, 223, 236),
            AccentSoft = Color.FromArgb(238, 233, 251),
            AccentInk = Color.FromArgb(78, 52, 150),
            ToggleOff = Color.FromArgb(200, 198, 212),
            GridChanged = Color.FromArgb(255, 244, 200),
            GridAdded = Color.FromArgb(222, 244, 226),
        };

        public static readonly Palette DarkLook = new Palette
        {
            Text = Color.FromArgb(233, 231, 242),
            Muted = Color.FromArgb(158, 155, 174),
            Canvas = Color.FromArgb(21, 19, 30),
            Panel = Color.FromArgb(27, 25, 38),
            Surface = Color.FromArgb(33, 30, 44),
            Line = Color.FromArgb(52, 49, 64),
            AccentSoft = Color.FromArgb(47, 41, 70),
            AccentInk = Color.FromArgb(196, 182, 246),
            ToggleOff = Color.FromArgb(84, 80, 100),
            GridChanged = Color.FromArgb(64, 56, 30),
            GridAdded = Color.FromArgb(36, 56, 40),
        };

        /// <summary>The palette in effect.</summary>
        public static Palette Current = LightLook;

        static string chosen;

        static string ChoiceFile { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Product.FileStem, "theme.txt"); } }

        /// <summary>What the user chose: "auto" (as Windows), "light" or "dark".</summary>
        public static string Choice
        {
            get
            {
                if (chosen == null)
                {
                    try
                    {
                        string c = File.Exists(ChoiceFile) ? File.ReadAllText(ChoiceFile).Trim().ToLowerInvariant() : "";
                        chosen = c == Light || c == Dark ? c : Automatic;
                    }
                    catch { chosen = Automatic; }
                }
                return chosen;
            }
            set
            {
                chosen = value == Light || value == Dark ? value : Automatic;
                try { Directory.CreateDirectory(Path.GetDirectoryName(ChoiceFile)); File.WriteAllText(ChoiceFile, chosen); } catch { }
            }
        }

        /// <summary>Does Windows show apps dark?</summary>
        public static bool WindowsIsDark()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    if (value is int) return (int)value == 0;
                }
            }
            catch { }
            return false;
        }

        /// <summary>The look a choice means ("auto": the one of Windows).</summary>
        public static bool IsDark(string choice)
        {
            if (choice == Dark) return true;
            if (choice == Light) return false;
            return WindowsIsDark();
        }

        /// <summary>Loads the choice at the start, before the first window is built.</summary>
        public static void Init() { Current = IsDark(Choice) ? DarkLook : LightLook; }

        /// <summary>Switches to a choice and repaints every open window right away.</summary>
        public static void Set(string choice)
        {
            Choice = choice;
            SwitchTo(IsDark(choice) ? DarkLook : LightLook);
            Repaint();
        }

        /// <summary>Follows Windows while the choice is "automatic" (a timer of the main window calls this).</summary>
        public static void FollowWindows()
        {
            if (Choice != Automatic) return;
            SwitchTo(WindowsIsDark() ? DarkLook : LightLook);
        }

        static void SwitchTo(Palette next)
        {
            if (ReferenceEquals(next, Current)) return;
            var old = Current;
            Current = next;
            foreach (Form f in Application.OpenForms) Recolour(f, old, next);
        }

        /// <summary>Repaints every open window (for the choice changed: the button's sign may look different).</summary>
        public static void Repaint()
        {
            foreach (Form f in Application.OpenForms) { try { f.Invalidate(true); } catch { } }
        }

        /// <summary>Walks a window and carries every colour of the old palette over to the new one.</summary>
        static void Recolour(Control c, Palette from, Palette to)
        {
            bool otherThread = false;
            try { otherThread = c.InvokeRequired; } catch { }
            if (otherThread)
            {
                // A window of its own thread (the consoles): it is asked to do its own recolouring.
                try { c.BeginInvoke((Action)(() => Recolour(c, from, to))); } catch { }
                return;
            }
            Swap(c, from, to);
            foreach (Control child in c.Controls) Recolour(child, from, to);
            try { c.Invalidate(); } catch { }
        }

        static void Swap(Control c, Palette from, Palette to)
        {
            Color back = c.BackColor, newBack = MapBack(from, to, back);
            if (newBack != back) { try { c.BackColor = newBack; } catch { } }
            Color fore = c.ForeColor, newFore = MapFore(from, to, fore);
            if (newFore != fore) { try { c.ForeColor = newFore; } catch { } }

            var list = c as ListView;
            if (list != null)
                foreach (ListViewItem item in list.Items)
                {
                    back = item.BackColor; newBack = MapBack(from, to, back);
                    if (newBack != back) item.BackColor = newBack;
                    fore = item.ForeColor; newFore = MapFore(from, to, fore);
                    if (newFore != fore) item.ForeColor = newFore;
                }

            var combo = c as ComboBox;
            if (combo != null) StyleCombo(combo);

            var pill = c as StatusPill;
            if (pill != null)
            {
                newBack = MapFore(from, to, pill.Dot);
                if (newBack != pill.Dot) pill.Dot = newBack;
            }
        }

        /// <summary>A drop-down list in the Windows style ignores its colours; in the dark look it is drawn flat, so it takes them.</summary>
        public static void StyleCombo(ComboBox combo)
        {
            var want = ReferenceEquals(Current, DarkLook) ? FlatStyle.Flat : FlatStyle.Standard;
            if (combo.FlatStyle != want) { try { combo.FlatStyle = want; } catch { } }
        }

        static Color MapBack(Palette from, Palette to, Color c)
        {
            if (c.A != 255) return c;
            if (c == from.Surface || c == Color.White) return to.Surface;
            if (c == from.Canvas) return to.Canvas;
            if (c == from.Panel) return to.Panel;
            if (c == from.Line) return to.Line;
            if (c == from.AccentSoft) return to.AccentSoft;
            if (c == from.Text) return to.Text;
            if (c == from.Muted) return to.Muted;
            if (c == SystemColors.Control) return to.Canvas;   // a panel or window that was never given a colour
            if (c == SystemColors.Window) return to.Surface;   // an input that was never given a colour
            return c;
        }

        static Color MapFore(Palette from, Palette to, Color c)
        {
            if (c.A != 255) return c;
            if (c == from.Text) return to.Text;
            if (c == from.Muted) return to.Muted;
            if (c == from.AccentInk) return to.AccentInk;
            if (c == SystemColors.ControlText || c == Color.Black) return to.Text;   // a control that was never given a colour
            if (c == SystemColors.WindowText) return to.Text;
            return c;
        }
    }
}
