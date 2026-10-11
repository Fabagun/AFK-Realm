using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace CoAInstaller
{
    /// <summary>What the random bots of the server are: how many, which side, class, race, level and where.</summary>
    class BotStats
    {
        public int Total, InWorld, Players, Counted;
        public int Alliance, Horde;
        public double AverageLevel;
        public readonly Dictionary<string, int> Classes = new Dictionary<string, int>();
        public readonly Dictionary<string, int> Races = new Dictionary<string, int>();
        public readonly int[] Levels = new int[LevelBands.Length];
        public readonly Dictionary<string, int> Zones = new Dictionary<string, int>();

        public static readonly string[] LevelBands = { "1–9", "10–19", "20–29", "30–39", "40–49", "50–59", "60–69", "70–79", "80" };
        static readonly HashSet<int> AllianceRaces = new HashSet<int> { 1, 3, 4, 7, 11 };
        static readonly HashSet<int> HordeRaces = new HashSet<int> { 2, 5, 6, 8, 10 };
        static readonly string[] RaceNames = { "", "Human", "Orc", "Dwarf", "Night Elf", "Undead", "Tauren", "Gnome", "Troll", "Goblin", "Blood Elf", "Draenei" };
        static readonly string[] ClassNames = { "", "Warrior", "Paladin", "Hunter", "Rogue", "Priest", "Death Knight", "Shaman", "Mage", "Warlock", "Hero", "Druid" };

        static string N(int v) { return v.ToString(CultureInfo.InvariantCulture); }

        /// <summary>Reads the bots from the database. <paramref name="inWorldOnly"/> counts only the bots in the world.</summary>
        public static BotStats Load(Install inst, bool inWorldOnly)
        {
            var login = DbLogin.FromConfig(inst);
            string bot = "UPPER(a.username) LIKE " + GameData.BotPattern(inst);
            var s = new BotStats();

            // The names the game shows for the classes of CoA (client_name; "name" is the internal one); the classic ones are fixed.
            var classNames = new Dictionary<int, string>();
            try
            {
                foreach (var row in MySql.Query(inst, login, "SELECT class, client_name FROM acore_world.ascension_custom_class;"))
                {
                    var f = row.Split('\t');
                    int id;
                    if (f.Length >= 2 && int.TryParse(f[0], out id) && f[1].Length > 0) classNames[id] = f[1];
                }
            }
            catch { }

            // races a module added (mod-custom-races writes their rows into chrraces_dbc): their names and sides
            var raceNames = new Dictionary<int, string>();
            var raceSide = new Dictionary<int, int>();
            try
            {
                foreach (var row in MySql.Query(inst, login, "SELECT ID, Name_Lang_enUS, Alliance FROM acore_world.chrraces_dbc;"))
                {
                    var f = row.Split('\t');
                    int id, side;
                    if (f.Length < 3 || !int.TryParse(f[0], out id)) continue;
                    if (f[1].Length > 0 && f[1] != "NULL") raceNames[id] = f[1];
                    if (int.TryParse(f[2], out side)) raceSide[id] = side;
                }
            }
            catch { }

            var players = MySql.Query(inst, login, "SELECT COUNT(*) FROM acore_characters.characters c JOIN acore_auth.account a ON a.id = c.account " +
                "WHERE c.online = 1 AND NOT (" + bot + ");");
            if (players.Count > 0) int.TryParse(players[0], out s.Players);

            long levelSum = 0;
            foreach (var row in MySql.Query(inst, login, "SELECT c.race, c.class, c.level, c.zone, c.online FROM acore_characters.characters c " +
                "JOIN acore_auth.account a ON a.id = c.account WHERE " + bot + ";"))
            {
                var f = row.Split('\t');
                int race, cls, level, zone;
                if (f.Length < 5 || !int.TryParse(f[0], out race) || !int.TryParse(f[1], out cls) || !int.TryParse(f[2], out level) || !int.TryParse(f[3], out zone))
                    continue;
                bool online = f[4] != "0";
                s.Total++;
                if (online) s.InWorld++;
                if (inWorldOnly && !online) continue;

                s.Counted++;
                levelSum += level;
                int side;
                if (AllianceRaces.Contains(race) || (raceSide.TryGetValue(race, out side) && side == 0)) s.Alliance++;
                else if (HordeRaces.Contains(race) || (raceSide.TryGetValue(race, out side) && side == 1)) s.Horde++;

                string className;
                if (!classNames.TryGetValue(cls, out className)) className = cls < ClassNames.Length ? ClassNames[cls] : "Class " + N(cls);
                Add(s.Classes, className);
                string raceName;
                if (!raceNames.TryGetValue(race, out raceName)) raceName = race < RaceNames.Length && RaceNames[race].Length > 0 ? RaceNames[race] : "Race " + N(race);
                Add(s.Races, raceName);
                s.Levels[Math.Min(LevelBands.Length - 1, Math.Max(0, level >= 80 ? 8 : level / 10))]++;
                if (zone > 0) Add(s.Zones, GameData.ZoneName(inst, zone));
            }
            s.AverageLevel = s.Counted > 0 ? (double)levelSum / s.Counted : 0;
            return s;
        }

        static void Add(Dictionary<string, int> d, string key)
        {
            int n;
            d[key] = d.TryGetValue(key, out n) ? n + 1 : 1;
        }
    }

    /// <summary>A list of horizontal bars, one row a value: name, bar, number and share.</summary>
    class BarList : Control
    {
        public class Item { public string Name; public int Value; public Color Color = Color.Empty; }

        List<Item> items = new List<Item>();
        int total;
        const int RowHeight = 26, NameWidth = 190, ValueWidth = 120;

        public BarList(int width)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.Surface;
            Width = width;
            Height = RowHeight;
            Margin = new Padding(0, 4, 0, 4);
        }

        /// <summary>The rows to show; <paramref name="total"/> is what the shares are of.</summary>
        public void Show(IEnumerable<Item> rows, int total)
        {
            items = rows.ToList();
            this.total = total;
            Height = Math.Max(1, items.Count) * RowHeight;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            if (items.Count == 0)
            {
                TextRenderer.DrawText(g, "No bots to count.", Ui.Base, new Rectangle(0, 0, Width, RowHeight), Ui.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                return;
            }
            Draw.Smooth(g);
            int max = Math.Max(1, items.Max(i => i.Value));
            int barLeft = NameWidth, barWidth = Math.Max(40, Width - NameWidth - ValueWidth - 12);
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                int y = i * RowHeight;
                TextRenderer.DrawText(g, it.Name, Ui.Base, new Rectangle(0, y, NameWidth - 10, RowHeight), Ui.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                var track = new RectangleF(barLeft, y + 8, barWidth, RowHeight - 16);
                using (var p = Draw.Round(track, 4)) using (var b = new SolidBrush(Ui.Panel)) g.FillPath(b, p);
                float w = it.Value <= 0 ? 0 : Math.Max(6f, barWidth * (float)it.Value / max);
                if (w > 0)
                    using (var p = Draw.Round(new RectangleF(barLeft, y + 8, w, RowHeight - 16), 4))
                    using (var b = new SolidBrush(it.Color.IsEmpty ? Ui.Accent : it.Color)) g.FillPath(b, p);
                string share = total > 0 ? "  " + Math.Round(100.0 * it.Value / total).ToString(CultureInfo.InvariantCulture) + " %" : "";
                TextRenderer.DrawText(g, it.Value.ToString("N0", CultureInfo.CurrentCulture) + share, Ui.Base,
                    new Rectangle(barLeft + barWidth + 12, y, ValueWidth, RowHeight), Ui.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
        }
    }

    /// <summary>A large number with a line under it.</summary>
    class StatTile : Control
    {
        string value = "–", caption;

        public StatTile(string caption, int width)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.caption = caption;
            BackColor = Ui.Surface;
            Size = new Size(width, 70);
            Margin = new Padding(0, 4, 12, 4);
        }

        public string Value { get { return value; } set { this.value = value; Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Draw.Behind(this));
            Draw.Smooth(g);
            using (var p = Draw.Round(new RectangleF(0, 0, Width - 1, Height - 1), 8))
            {
                using (var b = new SolidBrush(Ui.Panel)) g.FillPath(b, p);
            }
            TextRenderer.DrawText(g, value, Ui.H1, new Rectangle(12, 6, Width - 20, 34), Ui.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, caption, Ui.Small, new Rectangle(12, 40, Width - 20, 22), Ui.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    partial class ManagerPage
    {
        StatTile botsWorld, botsPlayers, botsLevel, botsSides;
        BarList botsFaction, botsClasses, botsRaces, botsLevels, botsZones;
        RadioButton botsOnline, botsAll;
        Label botsState;
        Card botsZoneCard;
        bool botsLoading;
        readonly System.Windows.Forms.Timer botsTimer = new System.Windows.Forms.Timer { Interval = 30000 };

        static readonly Color AllianceBlue = Color.FromArgb(46, 96, 178), HordeRed = Color.FromArgb(178, 46, 46);

        /// <summary>The Bots section: counts and distributions of the random bots, read from the database.</summary>
        void BuildBotsSection()
        {
            var overview = Section("bots", "The random bots", "How many bots there are and how they are spread. The numbers come from the database; " +
                "the server writes positions and levels of the bots every few minutes, so they can be a little behind.", "bots");
            int tileWidth = (overview.Inner - 3 * 12) / 4 - 1;
            botsWorld = new StatTile("bots in the world", tileWidth);
            botsPlayers = new StatTile("players in the world", tileWidth);
            botsLevel = new StatTile("average level", tileWidth);
            botsSides = new StatTile("Alliance : Horde", tileWidth);
            var tiles = Ui.Row(); tiles.BackColor = Ui.Surface;
            tiles.Controls.Add(botsWorld); tiles.Controls.Add(botsPlayers); tiles.Controls.Add(botsLevel); tiles.Controls.Add(botsSides);
            overview.Add(tiles);

            botsOnline = new RadioButton { Text = "Bots in the world", AutoSize = true, Checked = true, Font = Ui.Base, BackColor = Ui.Surface, Margin = new Padding(0, 6, 16, 0) };
            botsAll = new RadioButton { Text = "All bots", AutoSize = true, Font = Ui.Base, BackColor = Ui.Surface, Margin = new Padding(0, 6, 16, 0) };
            var refresh = Ui.Secondary("Refresh");
            botsState = Ui.Hint("");
            botsState.MaximumSize = new Size(overview.Inner - 400, 0);
            botsState.Margin = new Padding(8, 8, 0, 0);
            var controls = Ui.Row(); controls.BackColor = Ui.Surface;
            var count = new Label { Text = "Count:", AutoSize = true, Font = Ui.Base, ForeColor = Ui.Muted, BackColor = Ui.Surface, Margin = new Padding(0, 7, 8, 0) };
            controls.Controls.Add(count); controls.Controls.Add(botsOnline); controls.Controls.Add(botsAll); controls.Controls.Add(refresh); controls.Controls.Add(botsState);
            overview.Add(controls);
            botsOnline.CheckedChanged += (s, e) => { if (botsOnline.Checked) RefreshBots(); };
            botsAll.CheckedChanged += (s, e) => { if (botsAll.Checked) RefreshBots(); };
            refresh.Click += (s, e) => RefreshBots();

            Func<string, string, string, BarList> card = (title, text, icon) =>
            {
                var c = Section("bots", title, text, icon);
                var list = new BarList(c.Inner);
                c.Add(list);
                if (title == "Zones") botsZoneCard = c;
                return list;
            };
            botsFaction = card("Factions", null, null);
            botsClasses = card("Classes", null, null);
            botsRaces = card("Races", null, null);
            botsLevels = card("Levels", null, null);
            botsZones = card("Zones", "Where the bots are, the fifteen zones with the most of them.", "globe");

            botsTimer.Tick += (s, e) => { if (current == "bots") RefreshBots(); };
            botsTimer.Start();
        }

        /// <summary>Reads the numbers in the background and shows them; quietly does nothing while it is reading already.</summary>
        void RefreshBots()
        {
            if (botsLoading || botsWorld == null) return;
            botsLoading = true;
            bool inWorldOnly = botsOnline.Checked;
            botsState.Text = "Reading …";
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                BotStats stats = null;
                string error = null;
                try { stats = BotStats.Load(inst, inWorldOnly); }
                catch (Exception ex) { error = ex.Message; }
                Action show = () =>
                {
                    botsLoading = false;
                    if (stats == null)
                    {
                        botsState.Text = "Could not read the bots – is the database running? (" + error + ")";
                        return;
                    }
                    ShowBots(stats, inWorldOnly);
                    botsState.Text = "Updated " + DateTime.Now.ToString("HH:mm:ss") + " · every 30 s";
                };
                try { if (IsHandleCreated) BeginInvoke(show); else botsLoading = false; }
                catch { botsLoading = false; }
            });
        }

        void ShowBots(BotStats s, bool inWorldOnly)
        {
            var nf = CultureInfo.CurrentCulture;
            botsWorld.Value = s.InWorld.ToString("N0", nf) + " / " + s.Total.ToString("N0", nf);
            botsPlayers.Value = s.Players.ToString("N0", nf);
            botsLevel.Value = s.Counted > 0 ? s.AverageLevel.ToString("0.0", nf) : "–";
            botsSides.Value = s.Alliance.ToString("N0", nf) + " : " + s.Horde.ToString("N0", nf);

            botsFaction.Show(new[]
            {
                new BarList.Item { Name = "Alliance", Value = s.Alliance, Color = AllianceBlue },
                new BarList.Item { Name = "Horde", Value = s.Horde, Color = HordeRed }
            }, s.Counted);
            botsClasses.Show(s.Classes.OrderByDescending(k => k.Value).ThenBy(k => k.Key).Select(k => new BarList.Item { Name = k.Key, Value = k.Value }), s.Counted);
            botsRaces.Show(s.Races.OrderByDescending(k => k.Value).ThenBy(k => k.Key).Select(k => new BarList.Item { Name = k.Key, Value = k.Value }), s.Counted);
            // The level bands in their order, without the empty ones above the highest level reached.
            int last = Array.FindLastIndex(s.Levels, v => v > 0);
            botsLevels.Show(BotStats.LevelBands.Take(Math.Max(1, last + 1)).Select((b, i) => new BarList.Item { Name = "Level " + b, Value = s.Levels[i] }), s.Counted);

            var zones = s.Zones.OrderByDescending(k => k.Value).ThenBy(k => k.Key).ToList();
            var shown = zones.Take(15).Select(k => new BarList.Item { Name = k.Key, Value = k.Value }).ToList();
            int rest = zones.Skip(15).Sum(k => k.Value);
            if (rest > 0) shown.Add(new BarList.Item { Name = "Other zones (" + (zones.Count - 15) + ")", Value = rest, Color = Ui.Muted });
            botsZones.Show(shown, s.Counted);
        }
    }
}
