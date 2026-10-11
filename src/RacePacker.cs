using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CoAInstaller
{
    /// <summary>
    /// Makes race packs for the race folder out of another game client or repack: for each race its rows of the client
    /// tables (race, models and displays, skins and faces, hair, beards and their HD rows) and the files it needs (its
    /// models with their skins, animations and textures, the textures of its rows, its icon). Files the player's own game
    /// client has byte for byte are left out: the game has them anyway. After that, the other client can be deleted.
    ///
    /// The other client may be a whole game folder (with Data\) or just a folder of patch archives (CoA Custom ships its
    /// races as patches for the CoA client): its archives are read before those of the player's client, and an archive
    /// of the player's client with the same name as one of it is left out (a patch it replaces).
    /// </summary>
    static class RacePacker
    {
        /// <summary>A set of archives, read as the game reads them: the one it reads last wins.</summary>
        public sealed class Archives : IDisposable
        {
            readonly List<MpqArchive> list = new List<MpqArchive>();      // the one read last first
            public readonly List<string> Unreadable = new List<string>();
            public readonly Dictionary<string, DbcTable> Tables = new Dictionary<string, DbcTable>(StringComparer.OrdinalIgnoreCase);

            public Archives(IEnumerable<string> inLoadOrder)
            {
                foreach (var path in inLoadOrder.Reverse())
                    try { list.Add(new MpqArchive(path)); } catch { Unreadable.Add(Path.GetFileName(path)); }
            }
            public int Count { get { return list.Count; } }
            MpqArchive Holder(string name) { foreach (var a in list) if (a.Contains(name)) return a; return null; }
            public bool Has(string name) { return Holder(name) != null; }
            public long SizeOf(string name) { var a = Holder(name); return a == null ? -1 : a.SizeOf(name); }
            public byte[] Read(string name) { var a = Holder(name); return a == null ? null : a.Read(name); }
            public void Dispose() { foreach (var a in list) a.Dispose(); }
        }

        /// <summary>A race the other client offers.</summary>
        public class Candidate
        {
            public int Id;
            public string Key, Name;
            public bool MaleOnly;
            public bool InYourClient;     // the player's client has a race of this name too
            public bool InRaceFolder;     // the race folder has it already
            public override string ToString() { return Name; }
        }

        /// <summary>The archives of a folder in the order the game reads them: a game folder (Data\ and its language
        /// folder), the Data folder itself, or any folder with patch archives somewhere below it.</summary>
        public static List<string> ArchivesOf(string folder)
        {
            if (Directory.Exists(Path.Combine(folder, "Data"))) return ClientPatch.ClientArchives(folder, true);
            if (Path.GetFileName(folder.TrimEnd('\\', '/')).Equals("Data", StringComparison.OrdinalIgnoreCase)) return ClientPatch.ClientArchives(Path.GetDirectoryName(folder.TrimEnd('\\', '/')), true);
            // a game folder further down (a repack unpacked into a folder of its own)
            foreach (var dir in Below(folder, 3))
                if (Directory.Exists(Path.Combine(dir, "Data")) && Directory.GetFiles(Path.Combine(dir, "Data"), "*.mpq").Length > 0) return ClientPatch.ClientArchives(dir, true);
            var found = new List<string>();
            foreach (var dir in new[] { folder }.Concat(Below(folder, 4)))
                found.AddRange(Directory.GetFiles(dir).Where(f => f.EndsWith(".mpq", StringComparison.OrdinalIgnoreCase)));
            Func<string, int> rank = path =>
            {
                string name = Path.GetFileName(path).ToLowerInvariant();
                if (!name.StartsWith("patch")) return 0;
                return name == "patch.mpq" ? 1 : Regex.IsMatch(name, @"^patch-\d\.mpq$") ? 2 : 3;
            };
            return found.OrderBy(rank).ThenBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase).ToList();
        }

        static IEnumerable<string> Below(string folder, int depth)
        {
            if (depth == 0) yield break;
            string[] dirs;
            try { dirs = Directory.GetDirectories(folder); } catch { yield break; }
            foreach (var d in dirs.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                yield return d;
                foreach (var e in Below(d, depth - 1)) yield return e;
            }
        }

        /// <summary>The other client on top of the player's: its archives first, then those of the player's client it
        /// does not replace.</summary>
        public static Archives Open(string other, string yourClient)
        {
            var theirs = ArchivesOf(other);
            if (theirs.Count == 0) throw new InvalidOperationException("There are no game archives (.MPQ) in " + other + ".");
            var names = new HashSet<string>(theirs.Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);
            var yours = yourClient == null ? new List<string>() : ClientPatch.ClientArchives(yourClient).Where(p => !names.Contains(Path.GetFileName(p)));
            // read order: the player's archives first, then the other client's (the last read wins)
            return new Archives(yours.Concat(theirs).ToList());
        }

        /// <summary>Whether a DLL is a 64-race patch of the game client (it names itself so in its texts).</summary>
        public static bool IsRacePatch(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length > 8000000) return false;
                return Encoding.ASCII.GetString(File.ReadAllBytes(path)).Contains("64-race support");
            }
            catch { return false; }
        }

        /// <summary>The 64-race patch an other client brings (CoA Custom: dinput8.dll), or null.</summary>
        public static string FindRacePatch(string folder)
        {
            foreach (var dir in new[] { folder }.Concat(Below(folder, 4)))
            {
                string f = Path.Combine(dir, "dinput8.dll");
                if (File.Exists(f) && IsRacePatch(f)) return f;
            }
            return null;
        }

        /// <summary>Keeps the 64-race patch of an other client in the race folder (_Client\dinput8.dll). A note, or null.</summary>
        public static string KeepRacePatch(string folder, string raceFolder)
        {
            string found = FindRacePatch(folder);
            if (found == null) return null;
            string to = Path.Combine(raceFolder, "_Client", "dinput8.dll");
            if (File.Exists(to) && File.ReadAllBytes(to).SequenceEqual(File.ReadAllBytes(found))) return null;
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            File.Copy(found, to, true);
            return "Its 64-race patch (dinput8.dll) is kept in your race folder too (_Client).";
        }

        /// <summary>A short name of the other client for the race folder.</summary>
        public static string SourceLabel(string folder)
        {
            string f = folder.ToLowerInvariant();
            if (f.Contains("coa-custom") || f.Contains("coa_custom")) return "cc";
            if (f.Contains("ashen")) return "ashen";
            if (f.Contains("from_the_ashes") || f.Contains("from the ashes")) return "fta";
            return Path.GetFileName(folder.TrimEnd('\\', '/'));
        }

        static string Str(DbcTable t, byte[] row, int field)
        {
            uint at = t.Field(row, field);
            if (at >= t.Strings.Length) return "";
            int end = (int)at;
            while (end < t.Strings.Length && t.Strings[end] != 0) end++;
            return Encoding.UTF8.GetString(t.Strings, (int)at, end - (int)at);
        }

        static DbcTable Table(Archives a, string name)
        {
            DbcTable t;
            if (a.Tables.TryGetValue(name, out t)) return t;
            var bytes = a.Read("DBFilesClient\\" + name + ".dbc");
            return a.Tables[name] = bytes == null ? null : DbcTable.Parse(bytes, name + ".dbc");
        }

        static string M2(string path) { return Regex.Replace(path ?? "", @"\.(mdx|mdl)$", ".m2", RegexOptions.IgnoreCase); }

        static string NameOf(DbcTable races, byte[] row) { return Str(races, row, 14).Split('|')[0].Trim(); }

        /// <summary>The races of the other client beyond the stock ones that have a body there.</summary>
        public static List<Candidate> Races(Archives source, string yourClient, string raceFolder)
        {
            var races = Table(source, "ChrRaces");
            if (races == null) throw new InvalidOperationException("These archives have no race table (DBFilesClient\\ChrRaces.dbc).");
            var di = Table(source, "CreatureDisplayInfo");
            var md = Table(source, "CreatureModelData");
            var displayModel = di == null ? new Dictionary<uint, uint>() : di.Records.GroupBy(r => di.Field(r, 0)).ToDictionary(g => g.Key, g => di.Field(g.First(), 1));
            var modelPath = md == null ? new Dictionary<uint, string>() : md.Records.GroupBy(r => md.Field(r, 0)).ToDictionary(g => g.Key, g => Str(md, g.First(), 2));
            Func<uint, bool> hasBody = display =>
            {
                uint m; string p;
                return displayModel.TryGetValue(display, out m) && modelPath.TryGetValue(m, out p) && p.Length > 0 && source.Has(M2(p));
            };
            var sections = Table(source, "CharSections");
            var skins = new Dictionary<long, int>();
            if (sections != null)
                foreach (var r in sections.Records)
                {
                    long k = sections.Field(r, 1) * 2L + sections.Field(r, 2);
                    int c; skins.TryGetValue(k, out c); skins[k] = c + 1;
                }

            var yours = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (yourClient != null)
                try { foreach (var r in ClientRaces.Read(yourClient, null).Races) yours.Add(r.Key); } catch { }
            var packed = new HashSet<string>(RaceImport.Read(raceFolder).Select(p => p.Key), StringComparer.OrdinalIgnoreCase);

            var rows = races.Records.GroupBy(r => (int)races.Field(r, 0)).ToDictionary(g => g.Key, g => g.First());
            var stockNames = new HashSet<string>(ClientRaces.Stock.Where(rows.ContainsKey).Select(id => NameOf(races, rows[id])), StringComparer.OrdinalIgnoreCase);
            var list = new List<Candidate>();
            foreach (var id in rows.Keys.OrderBy(i => i))
            {
                if (ClientRaces.Stock.Contains(id)) continue;
                var row = rows[id];
                string name = NameOf(races, row);
                string key = ClientRaces.KeyOf(name);
                if (key.Length == 0 || stockNames.Contains(name)) continue;
                if (!hasBody(races.Field(row, 4))) continue;
                if (list.Any(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase))) continue;       // HD duplicates
                int female; skins.TryGetValue(id * 2L + 1, out female);
                list.Add(new Candidate { Id = id, Key = key, Name = name, MaleOnly = female < 10 || !hasBody(races.Field(row, 5)),
                    InYourClient = yours.Contains(key), InRaceFolder = packed.Contains(key) });
            }
            return list;
        }

        /// <summary>The files a model needs: itself, its skins, its animations kept outside it and the textures named in it.</summary>
        static IEnumerable<string> ModelFiles(Archives source, string path)
        {
            path = M2(path);
            var files = new List<string> { path };
            byte[] b;
            try { b = source.Read(path); } catch { return files; }
            if (b == null || b.Length < 0x60 || Encoding.ASCII.GetString(b, 0, 4) != "MD20") return files;
            string stem = path.Substring(0, path.Length - 3);
            Func<int, uint> u32 = at => at + 4 <= b.Length ? BitConverter.ToUInt32(b, at) : 0;
            uint views = u32(0x44);
            for (int i = 0; i < views && i < 16; i++) files.Add(stem + i.ToString("00") + ".skin");
            uint count = u32(0x1C), at0 = u32(0x20);
            for (long i = 0; i < count && at0 + i * 64 + 64 <= b.Length; i++)
            {
                long at = at0 + i * 64;
                ushort anim = BitConverter.ToUInt16(b, (int)at), sub = BitConverter.ToUInt16(b, (int)at + 2);
                uint flags = u32((int)at + 12);
                if ((flags & 0x20) == 0 && (flags & 0x40) == 0) files.Add(stem + anim.ToString("0000") + "-" + sub.ToString("00") + ".anim");
            }
            count = u32(0x50); at0 = u32(0x54);
            for (long i = 0; i < count && at0 + i * 16 + 16 <= b.Length; i++)
            {
                long at = at0 + i * 16;
                uint type = u32((int)at), length = u32((int)at + 8), offset = u32((int)at + 12);
                if (type != 0 || length < 2 || offset + length > b.Length) continue;
                // the name up to its first NUL: some models pad it, some count no NUL at all
                int end = (int)offset;
                while (end < offset + length && b[end] != 0) end++;
                string name = Encoding.UTF8.GetString(b, (int)offset, end - (int)offset).Trim();
                if (name.Length > 0) files.Add(name);
            }
            return files;
        }

        static List<object> Rows(DbcTable t, int raceField, int race, params int[] strings)
        {
            var list = new List<object>();
            if (t == null) return list;
            foreach (var r in t.Records)
            {
                if (t.Field(r, raceField) != race) continue;
                var row = new object[t.Fields];
                for (int k = 0; k < t.Fields; k++) row[k] = strings.Contains(k) ? (object)(t.Field(r, k) == 0 ? "" : Str(t, r, k)) : t.Field(r, k);
                list.Add(row);
            }
            return list;
        }

        /// <summary>Puts one race of the other client into the race folder (RaceFolder\Key\race.json and files\). Returns a
        /// line saying what it holds.</summary>
        public static string Pack(Archives source, Archives yours, Candidate race, string raceFolder, string sourceLabel, Action<string> progress)
        {
            var cr = Table(source, "ChrRaces");
            var row = cr.Records.First(r => cr.Field(r, 0) == race.Id);
            var di = Table(source, "CreatureDisplayInfo");
            var md = Table(source, "CreatureModelData");
            var models = new Dictionary<string, object>();
            var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int sex = 0; sex < 2; sex++)
            {
                uint display = cr.Field(row, 4 + sex);
                var d = di.Records.FirstOrDefault(r => di.Field(r, 0) == display);
                var m = d == null ? null : md.Records.FirstOrDefault(r => md.Field(r, 0) == di.Field(d, 1));
                if (m == null) continue;
                string path = Str(md, m, 2);
                models[sex == 0 ? "male" : "female"] = new Dictionary<string, object>
                {
                    { "display", Enumerable.Range(0, di.Fields).Select(k => (object)di.Field(d, k)).ToArray() },
                    { "model", Enumerable.Range(0, md.Fields).Select(k => (object)md.Field(m, k)).ToArray() },
                    { "path", path },
                };
                progress("Reading the model of the " + race.Name + " …");
                foreach (var f in ModelFiles(source, path)) files.Add(f);
            }
            if (models.Count == 0) throw new InvalidOperationException("The " + race.Name + " has no model in these archives.");

            var sections = Rows(Table(source, "CharSections"), 1, race.Id, 4, 5, 6);
            var hdSections = Rows(Table(source, "HDCharSections"), 1, race.Id, 4, 5, 6);
            foreach (object[] r in sections.Concat(hdSections))
                for (int k = 4; k <= 6; k++) { var t = r[k] as string; if (!string.IsNullOrEmpty(t)) files.Add(t); }
            string icon = "Interface\\Icons\\CoA_Race_" + race.Key;
            // its icon: from the other client, or from the player's (CoA has icons for races it does not offer)
            var iconFrom = source.Has(icon + ".blp") ? source : yours != null && yours.Has(icon + ".blp") ? yours : null;
            bool hasIcon = iconFrom != null;

            // which files the race folder needs: those the player's client lacks, or has otherwise
            var missing = new List<string>();
            var keep = new List<string>();
            var same = new List<string>();
            string dest = Path.Combine(raceFolder, race.Key);
            string filesDir = Path.Combine(dest, "files");
            if (Directory.Exists(filesDir)) Directory.Delete(filesDir, true);
            if (hasIcon)
            {
                string to = Path.Combine(filesDir, (icon + ".blp").Replace('\\', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.WriteAllBytes(to, iconFrom.Read(icon + ".blp"));
                keep.Add(icon + ".blp");
            }
            int done = 0;
            long bytes = 0;
            foreach (var f in files)
            {
                done++;
                if (done % 20 == 0) progress("The " + race.Name + ": file " + done + " of " + files.Count + " …");
                byte[] data;
                try { data = source.Read(f); }
                catch { data = null; }
                if (data == null) { missing.Add(f); continue; }
                if (yours != null && yours.SizeOf(f) == data.Length)
                {
                    byte[] mine;
                    try { mine = yours.Read(f); } catch { mine = null; }
                    if (mine != null && mine.SequenceEqual(data)) { same.Add(f); continue; }
                }
                string to = Path.Combine(filesDir, f.Replace('\\', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.WriteAllBytes(to, data);
                keep.Add(f);
                bytes += data.Length;
            }

            var json = new Dictionary<string, object>
            {
                { "key", race.Key },
                { "name", race.Name },
                { "source", new Dictionary<string, object> { { "client", sourceLabel }, { "race", race.Id } } },
                { "flags", cr.Field(row, 1) },
                { "side", cr.Field(row, 13) },
                { "prefix", Str(cr, row, 6) },
                { "file", Str(cr, row, 11) },
                { "models", models },
                { "sections", sections },
                { "hd_sections", hdSections },
                { "hair_geosets", Rows(Table(source, "CharHairGeosets"), 1, race.Id) },
                { "hd_hair_geosets", Rows(Table(source, "HDCharHairGeosets"), 1, race.Id) },
                { "facial_hair", Rows(Table(source, "CharacterFacialHairStyles"), 0, race.Id) },
                { "hd_facial_hair", Rows(Table(source, "HDCharacterFacialHairStyles"), 1, race.Id) },
                { "missing", missing },
                { "files", keep },
                { "uses_coa_files", same },
                { "icon", hasIcon ? icon : "" },
            };
            Directory.CreateDirectory(dest);
            File.WriteAllText(Path.Combine(dest, "race.json"), new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(json), new UTF8Encoding(false));
            return race.Name + ": " + keep.Count + " files, " + (bytes / 1000000) + " MB" + (same.Count > 0 ? ", " + same.Count + " taken from your client" : "") +
                (missing.Count > 0 ? ", " + missing.Count + " missing" : "") + ".";
        }
    }
}
