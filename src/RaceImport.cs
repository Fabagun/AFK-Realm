using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CoAInstaller
{
    /// <summary>
    /// Races brought in from other game clients, through a race folder: one folder per race with its race.json (its rows
    /// of the client tables, texts as text) and files\ (its models and textures). The rows go into the client patch
    /// (patch-Y.MPQ) with new numbers; the files go into an archive of their own for each race (patch-ZY&lt;race&gt;.MPQ),
    /// built once and kept while the race is chosen.
    ///
    /// Also the rules the character creation of the Ascension client needs for the sections (skins, faces, hair,
    /// underwear) of every added race, whether imported or copied from the client's own rows; without them it crashes or
    /// paints the wrong textures:
    ///  - every row playable (flag 1), none for Death Knights only (4) or pre-composed (8);
    ///  - a face and an underwear for every skin colour, and no style without rows (the creation screen reads them
    ///    without checking: 0x004EA77F);
    ///  - female rows for races that have only men (the creation looks them up before the switch to male);
    ///  - the rows of a race and sex in one block, sorted;
    ///  - an HD row has the id of its plain row (the HD layer replaces rows by id).
    /// Found out by ilusixn for CoA Custom (github.com/ilusixn/azerothcore-wotlk-coa).
    /// </summary>
    static class RaceImport
    {
        public const string ArchivePrefix = "patch-ZY";
        const string StampName = "AFK-Realm\\race.txt";
        public const int DisplayBase = 980000;          // display and model ids of imported races: 980000 + race * 2 + sex

        public static string ArchiveName(int race) { return ArchivePrefix + race.ToString("00") + ".MPQ"; }

        public static bool IsOurArchive(string fileName)
        {
            return Regex.IsMatch(fileName ?? "", "^" + ArchivePrefix + @"\d\d\.mpq$", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// A race of the race folder. Its race.json can be large (tens of megabytes for a race with many skins), so what
        /// the setup page and the checks need of it - names, files, sizes - is kept in a small race.info.json beside it,
        /// made the first time and again when race.json changes; race.json itself is read only when a patch is built.
        /// </summary>
        public class Pack
        {
            public string Key, Dir;
            internal Dictionary<string, object> Info;
            Dictionary<string, object> data;
            public Dictionary<string, object> Data
            {
                get
                {
                    if (data == null)
                        data = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(System.IO.File.ReadAllText(Path.Combine(Dir, "race.json"), Encoding.UTF8)) as Dictionary<string, object>
                            ?? new Dictionary<string, object>();
                    return data;
                }
                internal set { data = value; }
            }
            public string Name { get { return Str(Info, "name"); } }
            public string Prefix { get { return Str(Info, "prefix"); } }
            /// <summary>The race's file name: the folder of its model (Character\VoidElf\...), not the file name of the row
            /// it had in its client, which a client may have reused (Ashen's Void Elf sits in the row of the Fel Orc).</summary>
            public string File { get { string f = Str(Info, "file"); return f.Length > 0 ? f : Key; } }
            public string Icon { get { return Str(Info, "icon"); } }
            public List<string> Files { get { return List(Info, "files").Select(Convert.ToString).ToList(); } }
            public List<object[]> Rows(string name) { return List(Data, name).OfType<object[]>().ToList(); }
            public bool MaleOnly { get { object v; return !(Info.TryGetValue("female", out v) && v is bool && (bool)v); } }
            /// <summary>The side it had in its client: 0 Alliance, 1 Horde, 2 unknown.</summary>
            public int Side { get { object v; return Info.TryGetValue("side", out v) && v != null ? Convert.ToInt32(v) : 2; } }
            public string Source
            {
                get
                {
                    string c = Str(Info, "client");
                    switch (c.ToLowerInvariant()) { case "cc": return "CoA Custom"; case "ashen": return "Ashen Order"; case "fta": return "From the Ashes"; default: return c; }
                }
            }
            public int Missing { get { object v; return Info.TryGetValue("missing", out v) && v != null ? Convert.ToInt32(v) : 0; } }
            public long Bytes { get { object v; return Info.TryGetValue("bytes", out v) && v != null ? Convert.ToInt64(v) : 0; } }
        }

        static string Str(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : ""; }
        static object[] List(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) ? (v as object[]) ?? (v is System.Collections.ArrayList ? ((System.Collections.ArrayList)v).ToArray() : new object[0]) : new object[0]; }
        static uint Num(object v) { return v == null ? 0 : v is string ? 0 : Convert.ToUInt32(Convert.ToInt64(v) & 0xFFFFFFFF); }

        const string InfoName = "race.info.json";
        const int InfoVersion = 1;

        /// <summary>What the page needs of a race, from its full race.json.</summary>
        static Dictionary<string, object> MakeInfo(string dir, Dictionary<string, object> data, string stamp)
        {
            var models = data.ContainsKey("models") ? data["models"] as Dictionary<string, object> : null;
            var m = models == null ? null : models.Values.OfType<Dictionary<string, object>>().FirstOrDefault();
            var parts = (m == null ? "" : Str(m, "path")).Split('\\');
            string file = parts.Length > 2 && parts[0].Equals("Character", StringComparison.OrdinalIgnoreCase) ? parts[1] : "";
            var source = data.ContainsKey("source") ? data["source"] as Dictionary<string, object> : null;
            var files = List(data, "files").Select(Convert.ToString).ToList();
            long bytes = files.Sum(f => { var p = Path.Combine(dir, "files", f.Replace('\\', Path.DirectorySeparatorChar)); return System.IO.File.Exists(p) ? new FileInfo(p).Length : 0; });
            bool female = List(data, "sections").OfType<object[]>().Any(r => r.Length > 3 && Num(r[2]) == 1 && Num(r[3]) == 0);
            object side; data.TryGetValue("side", out side);
            return new Dictionary<string, object>
            {
                { "version", InfoVersion }, { "stamp", stamp }, { "key", Str(data, "key") }, { "name", Str(data, "name") }, { "prefix", Str(data, "prefix") },
                { "file", file }, { "icon", Str(data, "icon") }, { "client", source == null ? "" : Str(source, "client") }, { "side", side ?? 2 },
                { "female", female }, { "missing", List(data, "missing").Length }, { "bytes", bytes }, { "files", files },
            };
        }

        static readonly Dictionary<string, Pack> cache = new Dictionary<string, Pack>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The races of a race folder, by their key. Read once per change of their race.json.</summary>
        public static List<Pack> Read(string folder)
        {
            var packs = new List<Pack>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return packs;
            var json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            foreach (var dir in Directory.GetDirectories(folder).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                string file = Path.Combine(dir, "race.json");
                if (!System.IO.File.Exists(file)) continue;
                var fi = new FileInfo(file);
                string stamp = fi.Length + "/" + fi.LastWriteTimeUtc.Ticks;
                Pack pack;
                lock (cache)
                    if (cache.TryGetValue(dir, out pack) && Str(pack.Info, "stamp") == stamp) { packs.Add(pack); continue; }
                try
                {
                    Dictionary<string, object> info = null, data = null;
                    string infoFile = Path.Combine(dir, InfoName);
                    try
                    {
                        if (System.IO.File.Exists(infoFile)) info = json.DeserializeObject(System.IO.File.ReadAllText(infoFile, Encoding.UTF8)) as Dictionary<string, object>;
                    }
                    catch { info = null; }
                    if (info == null || Str(info, "stamp") != stamp || Str(info, "version") != InfoVersion.ToString())
                    {
                        data = json.DeserializeObject(System.IO.File.ReadAllText(file, Encoding.UTF8)) as Dictionary<string, object>;
                        if (data == null) continue;
                        info = MakeInfo(dir, data, stamp);
                        try { System.IO.File.WriteAllText(infoFile, json.Serialize(info), new UTF8Encoding(false)); } catch { }
                    }
                    pack = new Pack { Key = Str(info, "key"), Dir = dir, Info = info };
                    if (data != null) pack.Data = data;
                    lock (cache) cache[dir] = pack;
                    packs.Add(pack);
                }
                catch { }
            }
            return packs;
        }

        public static Pack Find(string folder, string key)
        {
            return Read(folder).FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        }

        // ---------------------------------------------------------------------------------------- the rows

        /// <summary>Adds an imported race to the client tables: its race row (the base race's, with its own model, file
        /// name and prefix), models and displays, and its sections, hair and beards with new numbers.</summary>
        public static void AddRace(Func<string, byte[]> load, Action<string, byte[]> save, Pack pack, int race, int baseRace, string name)
        {
            var cr = DbcTable.Parse(load("DBFilesClient\\ChrRaces.dbc"), "ChrRaces.dbc");
            var di = DbcTable.Parse(load("DBFilesClient\\CreatureDisplayInfo.dbc"), "CreatureDisplayInfo.dbc");
            var md = DbcTable.Parse(load("DBFilesClient\\CreatureModelData.dbc"), "CreatureModelData.dbc");
            var baseRow = cr.Records.FirstOrDefault(r => cr.Field(r, 0) == baseRace);
            if (baseRow == null) throw new InvalidOperationException("The game client has no race " + baseRace + " for the " + name + " to lean on.");
            var displays = di.Records.ToDictionary(r => di.Field(r, 0));
            var models = md.Records.ToDictionary(r => md.Field(r, 0));
            var modelsOfPack = pack.Data.ContainsKey("models") ? pack.Data["models"] as Dictionary<string, object> : null;
            var newDisplay = new uint[2];
            for (int sex = 0; sex < 2; sex++)
            {
                var m = modelsOfPack == null ? null : (modelsOfPack.ContainsKey(sex == 0 ? "male" : "female") ? modelsOfPack[sex == 0 ? "male" : "female"] as Dictionary<string, object> : null);
                if (m == null) m = modelsOfPack == null ? null : modelsOfPack.Values.OfType<Dictionary<string, object>>().FirstOrDefault();
                if (m == null) throw new InvalidOperationException("The race folder of the " + name + " has no model.");
                uint id = (uint)(DisplayBase + race * 2 + sex);
                if (displays.ContainsKey(id) || models.ContainsKey(id))
                    throw new InvalidOperationException("The game client already uses display or model " + id + "; the " + name + " cannot take it.");
                // the base race's display and model, with the model file and size of the imported race
                byte[] baseDisplay; displays.TryGetValue(cr.Field(baseRow, 4 + sex), out baseDisplay);
                byte[] baseModel = baseDisplay == null ? null : models.ContainsKey(di.Field(baseDisplay, 1)) ? models[di.Field(baseDisplay, 1)] : null;
                if (baseDisplay == null || baseModel == null) throw new InvalidOperationException("The game client has no model for race " + baseRace + ".");
                var model = (byte[])baseModel.Clone();
                md.SetField(model, 0, id);
                md.SetField(model, 2, md.AddString(Str(m, "path")));
                var src = List(m, "model");
                for (int k = 14; k <= 25 && k < src.Length; k++) md.SetField(model, k, Num(src[k]));     // collision and size
                md.Records.Add(model);
                var display = (byte[])baseDisplay.Clone();
                di.SetField(display, 0, id);
                di.SetField(display, 1, id);
                di.Records.Add(display);
                newDisplay[sex] = id;
            }
            var row = (byte[])baseRow.Clone();
            cr.Records.RemoveAll(r => cr.Field(r, 0) == race);
            cr.SetField(row, 0, (uint)race);
            cr.SetField(row, 1, 0xC);                       // playable
            cr.SetField(row, 4, newDisplay[0]);
            cr.SetField(row, 5, newDisplay[1]);
            cr.SetField(row, 6, cr.AddString(pack.Prefix.Length > 0 ? pack.Prefix : "Hu"));
            cr.SetField(row, 11, cr.AddString(pack.File.Length > 0 ? pack.File : pack.Key));
            foreach (int k in new[] { 14, 31, 48 }) cr.SetField(row, k, cr.AddString(name));
            cr.Records.Add(row);
            save("DBFilesClient\\ChrRaces.dbc", cr.ToBytes());
            save("DBFilesClient\\CreatureDisplayInfo.dbc", di.ToBytes());
            save("DBFilesClient\\CreatureModelData.dbc", md.ToBytes());

            // sections: plain rows and their HD rows with the same new ids
            var cs = DbcTable.Parse(load("DBFilesClient\\CharSections.dbc"), "CharSections.dbc");
            var hd = DbcTable.Parse(load("DBFilesClient\\HDCharSections.dbc"), "HDCharSections.dbc");
            cs.Records.RemoveAll(r => cs.Field(r, 1) == race);
            hd.Records.RemoveAll(r => hd.Field(r, 1) == race);
            uint next = Math.Max(MaxId(cs), MaxId(hd)) + 1;
            Func<object[], string> key = r => Num(r[2]) + "/" + Num(r[3]) + "/" + Num(r[8]) + "/" + Num(r[9]);
            var plain = pack.Rows("sections");
            var hdRows = pack.Rows("hd_sections");
            var idOf = new Dictionary<string, uint>();
            foreach (var r in plain.OrderBy(r => Num(r[2])).ThenBy(r => Num(r[3])).ThenBy(r => Num(r[8])).ThenBy(r => Num(r[9])))
            {
                string k = key(r);
                if (idOf.ContainsKey(k)) continue;
                idOf[k] = next;
                cs.Records.Add(Section(cs, r, next++, race));
            }
            // without HD rows of its own, the race's rows are its HD rows too: its textures are laid out as HD textures
            var hdIds = new HashSet<uint>(hd.Records.Select(x => hd.Field(x, 0)));
            // A plain row without an HD row of its own, in a race that has HD rows: its texture is laid out for the old
            // model and lands askew on the HD one (eyes and mouth in the wrong place: the first skin colours of CoA
            // Custom's Undead Gnome). It takes the HD row of the same style in the nearest colour, else of the nearest style.
            var hdUse = new List<object[]>(hdRows);
            if (hdRows.Count > 0)
            {
                var hdKeys = new HashSet<string>(hdRows.Select(key));
                foreach (var r in plain)
                {
                    if (hdKeys.Contains(key(r))) continue;
                    var near = hdRows.Where(h => Num(h[2]) == Num(r[2]) && Num(h[3]) == Num(r[3]))
                        .OrderBy(h => Num(h[8]) == Num(r[8]) ? 0 : 1)
                        .ThenBy(h => Math.Abs((long)Num(h[9]) - Num(r[9])))
                        .ThenBy(h => Math.Abs((long)Num(h[8]) - Num(r[8]))).FirstOrDefault();
                    if (near == null) continue;
                    var copy = (object[])near.Clone();
                    copy[8] = r[8]; copy[9] = r[9];
                    hdUse.Add(copy);
                    hdKeys.Add(key(copy));
                }
            }
            foreach (var r in hdRows.Count > 0 ? hdUse : plain)
            {
                string k = key(r);
                uint id;
                if (!idOf.TryGetValue(k, out id)) { id = next++; idOf[k] = id; }
                if (!hdIds.Add(id)) continue;
                hd.Records.Add(Section(hd, r, id, race));
            }
            save("DBFilesClient\\CharSections.dbc", cs.ToBytes());
            save("DBFilesClient\\HDCharSections.dbc", hd.ToBytes());

            // hair geosets (by id, paired with the HD table), beards (no id) and their HD rows
            CopyRows(load, save, "CharHairGeosets", "HDCharHairGeosets", pack.Rows("hair_geosets"), pack.Rows("hd_hair_geosets"), race, 1, r => Num(r[2]) + "/" + Num(r[3]));
            var fh = DbcTable.Parse(load("DBFilesClient\\CharacterFacialHairStyles.dbc"), "CharacterFacialHairStyles.dbc");
            fh.Records.RemoveAll(r => fh.Field(r, 0) == race);
            foreach (var r in pack.Rows("facial_hair"))
            {
                var rec = new byte[fh.RecordSize];
                for (int k = 0; k < fh.Fields && k < r.Length; k++) fh.SetField(rec, k, Num(r[k]));
                fh.SetField(rec, 0, (uint)race);
                fh.Records.Add(rec);
            }
            save("DBFilesClient\\CharacterFacialHairStyles.dbc", fh.ToBytes());
            var hfh = load("DBFilesClient\\HDCharacterFacialHairStyles.dbc");
            if (hfh != null)
            {
                var t = DbcTable.Parse(hfh, "HDCharacterFacialHairStyles.dbc");
                t.Records.RemoveAll(r => t.Field(r, 1) == race);
                uint n = MaxId(t) + 1;
                foreach (var r in pack.Rows("hd_facial_hair"))
                {
                    var rec = new byte[t.RecordSize];
                    for (int k = 0; k < t.Fields && k < r.Length; k++) t.SetField(rec, k, Num(r[k]));
                    t.SetField(rec, 0, n++);
                    t.SetField(rec, 1, (uint)race);
                    t.Records.Add(rec);
                }
                save("DBFilesClient\\HDCharacterFacialHairStyles.dbc", t.ToBytes());
            }
        }

        static byte[] Section(DbcTable t, object[] r, uint id, int race)
        {
            var rec = new byte[t.RecordSize];
            t.SetField(rec, 0, id);
            t.SetField(rec, 1, (uint)race);
            t.SetField(rec, 2, Num(r[2]));
            t.SetField(rec, 3, Num(r[3]));
            // an empty texture must point at a NUL byte: offset 0 of Ascension's tables is the Human male skin
            for (int k = 4; k <= 6; k++) t.SetField(rec, k, t.AddString(r[k] as string ?? ""));
            t.SetField(rec, 7, Num(r[7]));
            t.SetField(rec, 8, Num(r[8]));
            t.SetField(rec, 9, Num(r[9]));
            return rec;
        }

        static void CopyRows(Func<string, byte[]> load, Action<string, byte[]> save, string table, string hdTable, List<object[]> rows, List<object[]> hdRows,
                             int race, int raceField, Func<object[], string> key)
        {
            var t = DbcTable.Parse(load("DBFilesClient\\" + table + ".dbc"), table);
            var hb = load("DBFilesClient\\" + hdTable + ".dbc");
            var h = hb == null ? null : DbcTable.Parse(hb, hdTable);
            t.Records.RemoveAll(r => t.Field(r, raceField) == race);
            if (h != null) h.Records.RemoveAll(r => h.Field(r, raceField) == race);
            uint next = Math.Max(MaxId(t), h == null ? 0 : MaxId(h)) + 1;
            var idOf = new Dictionary<string, uint>();
            foreach (var r in rows)
            {
                var rec = new byte[t.RecordSize];
                for (int k = 0; k < t.Fields && k < r.Length; k++) t.SetField(rec, k, Num(r[k]));
                idOf[key(r)] = next;
                t.SetField(rec, 0, next++);
                t.SetField(rec, raceField, (uint)race);
                t.Records.Add(rec);
            }
            save("DBFilesClient\\" + table + ".dbc", t.ToBytes());
            if (h == null) return;
            foreach (var r in hdRows)
            {
                var rec = new byte[h.RecordSize];
                for (int k = 0; k < h.Fields && k < r.Length; k++) h.SetField(rec, k, Num(r[k]));
                uint id;
                if (!idOf.TryGetValue(key(r), out id)) id = next++;
                h.SetField(rec, 0, id);
                h.SetField(rec, raceField, (uint)race);
                h.Records.Add(rec);
            }
            save("DBFilesClient\\" + hdTable + ".dbc", h.ToBytes());
        }

        static uint MaxId(DbcTable t) { return t.Records.Count == 0 ? 0 : t.Records.Max(r => t.Field(r, 0)); }

        // ---------------------------------------------------------------------------------------- the rules

        /// <summary>The rules of the character creation for the sections of one added race (see the class summary).</summary>
        public static void SectionRules(Func<string, byte[]> load, Action<string, byte[]> save, int race, bool maleOnly)
        {
            var cs = DbcTable.Parse(load("DBFilesClient\\CharSections.dbc"), "CharSections.dbc");
            var hdBytes = load("DBFilesClient\\HDCharSections.dbc");
            var hd = hdBytes == null ? null : DbcTable.Parse(hdBytes, "HDCharSections.dbc");
            var mine = cs.Records.Where(r => cs.Field(r, 1) == race).ToList();
            if (mine.Count == 0) return;
            uint next = Math.Max(MaxId(cs), hd == null ? 0 : MaxId(hd)) + 1;
            var added = new List<byte[]>();
            Func<byte[], uint, uint, byte[]> clone = (r, field, value) =>
            {
                var c = (byte[])r.Clone();
                cs.SetField(c, 0, next++);
                cs.SetField(c, (int)field, value);
                added.Add(c);
                return c;
            };
            // men only: the women get the men's rows
            var sexes = new HashSet<uint>(mine.Where(r => cs.Field(r, 3) == 0).Select(r => cs.Field(r, 2)));
            if (maleOnly || sexes.Count == 1)
            {
                uint only = sexes.Contains(0) ? 0u : 1u;
                cs.Records.RemoveAll(r => cs.Field(r, 1) == race && cs.Field(r, 2) != only);
                mine = cs.Records.Where(r => cs.Field(r, 1) == race).ToList();
                foreach (var r in mine.Where(r => cs.Field(r, 3) < 5).ToList()) clone(r, 2, 1 - only);
                cs.Records.AddRange(added); mine.AddRange(added); added.Clear();
            }
            // a face and an underwear for each skin colour; no style without rows
            foreach (uint sex in new uint[] { 0, 1 })
            {
                var skins = mine.Where(r => cs.Field(r, 2) == sex && cs.Field(r, 3) == 0 && cs.Field(r, 8) == 0).Select(r => cs.Field(r, 9)).ToList();
                if (skins.Count == 0) continue;
                uint colours = skins.Max() + 1;
                foreach (uint sec in new uint[] { 1, 2, 3, 4 })
                {
                    var rows = mine.Where(r => cs.Field(r, 2) == sex && cs.Field(r, 3) == sec).ToList();
                    if (rows.Count == 0) continue;
                    var styles = rows.GroupBy(r => cs.Field(r, 8)).ToDictionary(g => g.Key, g => g.ToDictionary(r => cs.Field(r, 9)));
                    uint top = styles.Keys.Max();
                    for (uint v = 0; v <= top; v++)
                        if (!styles.ContainsKey(v))
                        {
                            var copy = styles[styles.Keys.Min()].Values.Select(r => clone(r, 8, v)).ToList();
                            styles[v] = copy.ToDictionary(r => cs.Field(r, 9));
                        }
                    if (sec != 1 && sec != 4) continue;
                    foreach (var style in styles.Values.ToList())
                        for (uint c = 0; c < colours; c++)
                            if (!style.ContainsKey(c))
                            {
                                var near = style.Keys.Where(k => k < c).DefaultIfEmpty(style.Keys.Min()).Max();
                                style[c] = clone(style[near], 9, c);
                            }
                }
            }
            cs.Records.AddRange(added);
            // flags and order
            foreach (var r in cs.Records.Where(r => cs.Field(r, 1) == race && cs.Field(r, 3) < 5))
                cs.SetField(r, 7, (cs.Field(r, 7) | 1u) & ~4u & ~8u);
            var block = cs.Records.Where(r => cs.Field(r, 1) == race)
                .OrderBy(r => cs.Field(r, 2)).ThenBy(r => cs.Field(r, 3)).ThenBy(r => cs.Field(r, 8)).ThenBy(r => cs.Field(r, 9)).ToList();
            cs.Records.RemoveAll(r => cs.Field(r, 1) == race);
            cs.Records.AddRange(block);
            save("DBFilesClient\\CharSections.dbc", cs.ToBytes());
            if (hd == null) return;
            // HD rows of the race: the id of the plain row with the same race, sex, section, style and colour
            var idOf = block.GroupBy(r => cs.Field(r, 2) + "/" + cs.Field(r, 3) + "/" + cs.Field(r, 8) + "/" + cs.Field(r, 9)).ToDictionary(g => g.Key, g => cs.Field(g.First(), 0));
            var plainIds = new HashSet<uint>(cs.Records.Select(r => cs.Field(r, 0)));
            foreach (var r in hd.Records.Where(r => hd.Field(r, 1) == race))
            {
                uint id;
                if (idOf.TryGetValue(hd.Field(r, 2) + "/" + hd.Field(r, 3) + "/" + hd.Field(r, 8) + "/" + hd.Field(r, 9), out id)) hd.SetField(r, 0, id);
                hd.SetField(r, 7, (hd.Field(r, 7) | 1u) & ~4u & ~8u);
            }
            var seen = new HashSet<uint>();
            hd.Records.RemoveAll(r => hd.Field(r, 1) == race && !seen.Add(hd.Field(r, 0)));
            save("DBFilesClient\\HDCharSections.dbc", hd.ToBytes());
        }

        // ---------------------------------------------------------------------------------------- the files

        public class Wanted { public int Race; public Pack Pack; }

        /// <summary>A stamp of what an archive of a race holds: its race number and its files with their sizes and times.</summary>
        static string Stamp(Wanted w)
        {
            var sb = new StringBuilder();
            sb.Append("race ").Append(w.Race).Append(' ').Append(w.Pack.Key).Append('\n');
            foreach (var f in w.Pack.Files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var p = Path.Combine(w.Pack.Dir, "files", f.Replace('\\', Path.DirectorySeparatorChar));
                if (!File.Exists(p)) continue;
                var fi = new FileInfo(p);
                sb.Append(f).Append(' ').Append(fi.Length).Append(' ').Append(fi.LastWriteTimeUtc.Ticks).Append('\n');
            }
            using (var sha = SHA256.Create())
                return "Made by " + Product.Name + " for race " + w.Race + " (" + w.Pack.Key + ").\r\nContents " +
                    BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", "").Substring(0, 16) + "\r\n";
        }

        static string InstalledStamp(string path)
        {
            if (!File.Exists(path)) return null;
            try { using (var mpq = new MpqArchive(path)) { var b = mpq.Read(StampName); return b == null ? "" : Encoding.UTF8.GetString(b); } }
            catch { return ""; }
        }

        /// <summary>Whether the archives of the imported races in the game folder are as they should be.</summary>
        public static bool Current(string client, List<Wanted> wanted)
        {
            string data = Path.Combine(client, "Data");
            foreach (var w in wanted)
                if (InstalledStamp(Path.Combine(data, ArchiveName(w.Race))) != Stamp(w)) return false;
            var names = new HashSet<string>(wanted.Select(w => ArchiveName(w.Race)), StringComparer.OrdinalIgnoreCase);
            return !Directory.Exists(data) || !Directory.GetFiles(data).Select(Path.GetFileName).Any(n => IsOurArchive(n) && !names.Contains(n) && InstalledStamp(Path.Combine(data, n)) != "");
        }

        /// <summary>Builds the archives the imported races need and removes those of races no longer chosen.</summary>
        public static List<string> Sync(string client, List<Wanted> wanted) { return Sync(client, wanted, null); }

        /// <summary>The same, reporting each race and how far its archive is (text, step, steps).</summary>
        public static List<string> Sync(string client, List<Wanted> wanted, Action<string, int, int> progress)
        {
            var notes = new List<string>();
            string data = Path.Combine(client, "Data");
            foreach (var w in wanted)
            {
                string path = Path.Combine(data, ArchiveName(w.Race));
                string stamp = Stamp(w);
                if (InstalledStamp(path) == stamp) continue;
                if (InstalledStamp(path) == "") throw new InvalidOperationException("Data\\" + ArchiveName(w.Race) + " in the game folder was not made by " + Product.Name + ". Rename or remove it, then install again.");
                var files = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in w.Pack.Files)
                {
                    var p = Path.Combine(w.Pack.Dir, "files", f.Replace('\\', Path.DirectorySeparatorChar));
                    if (File.Exists(p)) files[f] = p;
                }
                string temp = path + ".new";
                string what = "Writing the files of the " + w.Pack.Name + " (Data\\" + ArchiveName(w.Race) + ")";
                if (progress != null) progress(what + " …", 0, files.Count + 2);
                MpqArchive.WriteFromFiles(temp, files, new Dictionary<string, byte[]> { { StampName, Encoding.UTF8.GetBytes(stamp) } },
                    progress == null ? null : (Action<int, int>)((done, all) => progress(what + ": " + done + " of " + all + " files", done, all)));
                try
                {
                    if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
                }
                catch (IOException)
                {
                    try { File.Delete(temp); } catch { }
                    throw new InvalidOperationException("Data\\" + ArchiveName(w.Race) + " is in use. Close the game and its launcher first.");
                }
                notes.Add(ArchiveName(w.Race) + " (" + w.Pack.Name + ")");
            }
            var names = new HashSet<string>(wanted.Select(w => ArchiveName(w.Race)), StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(data))
                foreach (var path in Directory.GetFiles(data).Where(p => IsOurArchive(Path.GetFileName(p)) && !names.Contains(Path.GetFileName(p))))
                    if (!string.IsNullOrEmpty(InstalledStamp(path)))
                        try { File.Delete(path); notes.Add(Path.GetFileName(path) + " removed"); } catch (IOException) { }
            return notes;
        }

        /// <summary>The archive of a race, for the ZIP of the other players.</summary>
        public static string BuildTemp(Wanted w)
        {
            var files = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in w.Pack.Files)
            {
                var p = Path.Combine(w.Pack.Dir, "files", f.Replace('\\', Path.DirectorySeparatorChar));
                if (File.Exists(p)) files[f] = p;
            }
            string temp = Path.GetTempFileName();
            MpqArchive.WriteFromFiles(temp, files, new Dictionary<string, byte[]> { { StampName, Encoding.UTF8.GetBytes(Stamp(w)) } });
            return temp;
        }
    }
}
