using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CoAInstaller
{
    enum PatchState { None, Current, Missing, Outdated, Leftover }

    /// <summary>
    /// The client patch of the modules: an archive (Data\patch-Y.MPQ) that changes files of the game client - the
    /// "client": {"patch": ...} part of a module's afk-realm.json. A module brings files of its own ("files") and asks
    /// for changes to files of the client ("edits"); AFK Realm takes those files from the player's own game client,
    /// so it never hands out files of the game, and builds the archive anew whenever the client, a module or a setting
    /// it reads has changed. The archive goes into the game folder, and into the ZIP for the other players.
    /// </summary>
    static class ClientPatch
    {
        public const string ArchiveName = "patch-Y.MPQ";
        const string StampName = "AFK-Realm\\client-patch.txt";

        static string RecordFile(Install inst) { return Path.Combine(inst.Root, "Dependencies", "client-patch.txt"); }

        /// <summary>A file next to the game's exe (a DLL the game loads), taken from a file of the user's own (a module
        /// never ships one).</summary>
        internal class RootFile { public string Module, From, To; }

        internal class FileSpec { public string Module, From, To; public List<Dictionary<string, object>> Fills; }
        internal class EditSpec { public string Module, File, InsertAfter, Line; public Dictionary<string, object> DropRows, AddRows, CloneRows, FillRows, SetRows, CopyRows, ImportRace, SectionRules, AddFile; public long? IdShift; }

        public class Plan
        {
            public List<string> Modules = new List<string>();
            public List<string> Why = new List<string>();
            public bool ClearCache;
            internal List<FileSpec> Files = new List<FileSpec>();
            internal List<EditSpec> Edits = new List<EditSpec>();
            internal List<RaceImport.Wanted> Imports = new List<RaceImport.Wanted>();
            internal List<RootFile> RootFiles = new List<RootFile>();
            public bool Empty { get { return Files.Count == 0 && Edits.Count == 0; } }
        }

        /// <summary>What the installed modules want changed in the client.</summary>
        public static Plan Collect(Install inst)
        {
            var plan = new Plan();
            string modules = ModuleCatalog.ModulesDir(inst);
            if (!Directory.Exists(modules)) return plan;
            var json = new JavaScriptSerializer();
            foreach (var dir in Directory.GetDirectories(modules).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                string file = Path.Combine(dir, ModuleCatalog.ManifestName);
                if (!File.Exists(file)) continue;
                Dictionary<string, object> clientPart, patch;
                object v;
                try
                {
                    var data = json.DeserializeObject(File.ReadAllText(file, Encoding.UTF8)) as Dictionary<string, object>;
                    if (data == null || !data.TryGetValue("client", out v) || (clientPart = v as Dictionary<string, object>) == null) continue;
                    if (!clientPart.TryGetValue("patch", out v) || (patch = v as Dictionary<string, object>) == null) continue;
                }
                catch { continue; }
                string module = Path.GetFileName(dir);
                string root = Path.GetFullPath(dir).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
                int before = plan.Files.Count + plan.Edits.Count;
                foreach (var f in List(patch, "files"))
                {
                    if (!When(inst, f)) continue;
                    string from = Text(f, "from"), to = Text(f, "to");
                    string source;
                    try { source = Path.GetFullPath(Path.Combine(dir, from.Replace('/', Path.DirectorySeparatorChar))); } catch { continue; }
                    if (!source.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(source) || !SafeClientPath(to)) continue;
                    plan.Files.Add(new FileSpec { Module = module, From = source, To = to.Replace('/', '\\'), Fills = Dicts(f.TryGetValue("fill", out v) ? v : null) });
                }
                foreach (var f in List(patch, "rootFiles"))
                {
                    if (!When(inst, f)) continue;
                    string to = Text(f, "to");
                    if (!Regex.IsMatch(to, @"^[A-Za-z0-9_.-]+\.dll$")) continue;          // a DLL by its name, next to the exe only
                    plan.RootFiles.Add(new RootFile { Module = module, To = to, From = Convert.ToString(f.TryGetValue("from", out v) ? Settings(inst, v) : "") });
                }
                foreach (var e in Expand(inst, List(patch, "edits")))
                {
                    string target = Text(e, "file");
                    if (!SafeClientPath(target) || !When(inst, e)) continue;
                    foreach (var k in e.Keys.Where(k => k != "when").ToList()) e[k] = Settings(inst, e[k]);
                    var edit = new EditSpec { Module = module, File = target.Replace('/', '\\'), InsertAfter = Text(e, "insertAfter"), Line = Text(e, "line"),
                        DropRows = e.TryGetValue("dropRows", out v) ? v as Dictionary<string, object> : null,
                        AddRows = e.TryGetValue("addRows", out v) ? v as Dictionary<string, object> : null,
                        CloneRows = e.TryGetValue("cloneRows", out v) ? v as Dictionary<string, object> : null,
                        FillRows = e.TryGetValue("fillRows", out v) ? v as Dictionary<string, object> : null,
                        SetRows = e.TryGetValue("setRows", out v) ? v as Dictionary<string, object> : null,
                        CopyRows = e.TryGetValue("copyRows", out v) ? v as Dictionary<string, object> : null,
                        ImportRace = e.TryGetValue("importRace", out v) ? v as Dictionary<string, object> : null,
                        SectionRules = e.TryGetValue("sectionRules", out v) ? v as Dictionary<string, object> : null,
                        AddFile = e.TryGetValue("addFile", out v) ? v as Dictionary<string, object> : null };
                    if (edit.ImportRace != null)
                    {
                        // a race from another client: its rows go into this patch, its files into an archive of its own
                        var pack = RaceImport.Find(Text(edit.ImportRace, "folder"), Text(edit.ImportRace, "key"));
                        int race;
                        if (!int.TryParse(Text(edit.ImportRace, "id"), out race) || race <= 0) continue;
                        if (pack != null) plan.Imports.Add(new RaceImport.Wanted { Race = race, Pack = pack });
                        // without its folder the build says so (Build), instead of leaving the race out unseen
                    }
                    if ((edit.Line.Length > 0 && edit.InsertAfter.Length > 0) || edit.DropRows != null || edit.AddRows != null || edit.CloneRows != null ||
                        edit.FillRows != null || edit.SetRows != null || edit.CopyRows != null || edit.ImportRace != null || edit.SectionRules != null || edit.AddFile != null)
                        plan.Edits.Add(edit);
                }
                if (plan.Files.Count + plan.Edits.Count + plan.RootFiles.Count(r => r.Module == module) > before)
                {
                    plan.Modules.Add(module);
                    string why = clientPart.TryGetValue("why", out v) ? Convert.ToString(v) : "";
                    if (why.Length > 0) plan.Why.Add(module + ": " + why);
                    plan.ClearCache |= clientPart.TryGetValue("clearCache", out v) && v is bool && (bool)v;
                }
            }
            return plan;
        }

        static IEnumerable<Dictionary<string, object>> List(Dictionary<string, object> d, string key)
        {
            object v;
            var list = d.TryGetValue(key, out v) ? v as object[] : null;
            return list == null ? Enumerable.Empty<Dictionary<string, object>>() : list.OfType<Dictionary<string, object>>();
        }

        static string Text(Dictionary<string, object> d, string key) { object v; return d.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : ""; }

        /// <summary>
        /// A number of an edit taken from a server setting: {"setting": key, "conf": file, "default": number} becomes the
        /// number the setting holds (its default when it holds none). Example: the number a realm gave a race.
        /// </summary>
        static object Settings(Install inst, object v)
        {
            var d = v as Dictionary<string, object>;
            if (d != null)
            {
                if (d.ContainsKey("setting"))
                {
                    string text = ConfValue(inst, Text(d, "conf"), Text(d, "setting"));
                    object isText;
                    if (d.TryGetValue("text", out isText) && isText is bool && (bool)isText) return text ?? Text(d, "default");
                    long n;
                    if (text != null && long.TryParse(text.Trim(), out n)) return n;
                    object def;
                    return d.TryGetValue("default", out def) ? def : 0;
                }
                foreach (var k in d.Keys.ToList()) d[k] = Settings(inst, d[k]);
                return d;
            }
            var list = v as object[];
            if (list != null) for (int i = 0; i < list.Length; i++) list[i] = Settings(inst, list[i]);
            return v;
        }

        /// <summary>
        /// Edits written once for many: an edit with "forEach" ({conf, key}) stands for one edit per word of that setting,
        /// with "{each}" replaced by the word everywhere in it (texts and names). Example: the same changes for every race
        /// a realm chose, each with its own settings ("CustomRaces.{each}.Id").
        /// </summary>
        static IEnumerable<Dictionary<string, object>> Expand(Install inst, IEnumerable<Dictionary<string, object>> items)
        {
            foreach (var item in items)
            {
                object v;
                var each = item.TryGetValue("forEach", out v) ? v as Dictionary<string, object> : null;
                if (each == null) { yield return item; continue; }
                foreach (var word in Words(ConfValue(inst, Text(each, "conf"), Text(each, "key")) ?? Text(each, "default")))
                {
                    var copy = (Dictionary<string, object>)Subst(item, "{each}", word);
                    copy.Remove("forEach");
                    yield return copy;
                }
            }
        }

        static object Subst(object v, string token, string value)
        {
            var text = v as string;
            if (text != null) return text.Replace(token, value);
            var d = v as Dictionary<string, object>;
            if (d != null)
            {
                var copy = new Dictionary<string, object>();
                foreach (var kv in d) copy[kv.Key.Replace(token, value)] = Subst(kv.Value, token, value);
                return copy;
            }
            var list = v as object[];
            if (list != null) return list.Select(x => Subst(x, token, value)).ToArray();
            return v;
        }

        /// <summary>One object or a list of them.</summary>
        static List<Dictionary<string, object>> Dicts(object v)
        {
            if (v is Dictionary<string, object>) return new List<Dictionary<string, object>> { (Dictionary<string, object>)v };
            return ((v as object[]) ?? new object[0]).OfType<Dictionary<string, object>>().ToList();
        }

        /// <summary>The words of a setting: "Goblin Worgen" or "Goblin, Worgen".</summary>
        static string[] Words(string value)
        {
            return Regex.Split(value ?? "", @"[\s,;]+").Where(w => w.Length > 0).ToArray();
        }

        /// <summary>
        /// Whether a file or edit of a module's client patch is wanted with the server's settings: every condition of its
        /// "when" ({conf, key, default} and one of: "has" - the setting lists this word; "in" - the setting is one of these
        /// words; "notIn" - it is none of them; "is" - the setting is this word; case does not count) must hold. Without
        /// "when", always.
        /// </summary>
        static bool When(Install inst, Dictionary<string, object> item)
        {
            object v;
            if (!item.TryGetValue("when", out v)) return true;
            foreach (var c in Dicts(v))
            {
                string value = ConfValue(inst, Text(c, "conf"), Text(c, "key")) ?? Text(c, "default");
                var words = Words(value);
                object w;
                if (c.TryGetValue("has", out w) && !words.Any(x => x.Equals(Convert.ToString(w), StringComparison.OrdinalIgnoreCase))) return false;
                if (c.TryGetValue("is", out w) && !value.Trim().Equals(Convert.ToString(w), StringComparison.OrdinalIgnoreCase)) return false;
                if (c.TryGetValue("in", out w))
                {
                    var allowed = ((w as object[]) ?? new object[0]).Select(Convert.ToString);
                    if (!allowed.Any(a => a.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase))) return false;
                }
                if (c.TryGetValue("notIn", out w))
                {
                    var refused = ((w as object[]) ?? new object[0]).Select(Convert.ToString);
                    if (refused.Any(a => a.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase))) return false;
                }
            }
            return true;
        }

        /// <summary>Only files of the interface and the data tables, by a plain relative name.</summary>
        static bool SafeClientPath(string path)
        {
            return Regex.IsMatch(path ?? "", @"^(Interface|DBFilesClient)[\\/][\w .\-\\/]+$", RegexOptions.IgnoreCase) && !path.Contains("..");
        }

        /// <summary>The archives of the client, the one that wins last: the game reads the patches after its base archives.</summary>
        internal static List<string> ClientArchives(string client) { return ClientArchives(client, false); }

        /// <summary>The archives of a game client in the order the game reads them. other: a client AFK Realm did not
        /// patch (its patch-Y is its own).</summary>
        internal static List<string> ClientArchives(string client, bool other)
        {
            string data = Path.Combine(client, "Data");
            Func<string, int> rank = path =>
            {
                string name = Path.GetFileName(path).ToLowerInvariant();
                bool locale = !Path.GetDirectoryName(path).Equals(data, StringComparison.OrdinalIgnoreCase);
                if (!name.StartsWith("patch")) return locale ? 1 : 0;
                if (locale) return 2;
                // patch.MPQ, then patch-2 to patch-9, then the lettered ones
                return name == "patch.mpq" ? 3 : Regex.IsMatch(name, @"^patch-\d\.mpq$") ? 4 : 5;
            };
            var all = new List<string>();
            if (!Directory.Exists(data)) return all;
            Func<string, IEnumerable<string>> archives = dir => Directory.GetFiles(dir).Where(f => f.EndsWith(".mpq", StringComparison.OrdinalIgnoreCase));
            all.AddRange(archives(data));
            foreach (var sub in Directory.GetDirectories(data))
                if (Regex.IsMatch(Path.GetFileName(sub), @"^[a-z]{2}[A-Z]{2}$")) all.AddRange(archives(sub));
            return all.Where(p => other || (!Path.GetFileName(p).Equals(ArchiveName, StringComparison.OrdinalIgnoreCase) && !RaceImport.IsOurArchive(Path.GetFileName(p))))
                .OrderBy(rank).ThenBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>The newest copy of a file of the client: from the archive the game reads last.</summary>
        internal static byte[] FromClient(string client, string name, out string source)
        {
            foreach (var path in Enumerable.Reverse(ClientArchives(client)))
            {
                MpqArchive mpq;
                try { mpq = new MpqArchive(path); } catch { continue; }
                using (mpq)
                    if (mpq.Contains(name)) { source = Path.GetFileName(path); return mpq.Read(name); }
            }
            source = null;
            return null;
        }

        /// <summary>The value of an option of a server config file (the .conf, or its .dist), or null.</summary>
        static string ConfValue(Install inst, string conf, string key)
        {
            foreach (var path in new[] { Path.Combine(inst.ConfigDir, "modules", conf), Path.Combine(inst.ConfigDir, conf),
                                         Path.Combine(inst.ConfigDir, "modules", conf + ".dist"), Path.Combine(inst.ConfigDir, conf + ".dist") })
            {
                if (!File.Exists(path)) continue;
                var m = Regex.Match(File.ReadAllText(path), @"(?m)^[ \t]*" + Regex.Escape(key) + @"[ \t]*=[ \t]*(.*?)[ \t]*\r?$");
                if (m.Success) return m.Groups[1].Value.Trim().Trim('"');
            }
            return null;
        }

        /// <summary>The contents of the archive as it should be now: name -> bytes. Throws when the client lacks a file a module edits.</summary>
        public static SortedDictionary<string, byte[]> Build(Install inst, string client, Plan plan, List<string> notes)
        {
            var files = new SortedDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in plan.Files)
            {
                var bytes = File.ReadAllBytes(f.From);
                foreach (var fill in f.Fills) bytes = Fill(inst, bytes, fill);
                files[f.To] = bytes;
            }
            IdShifts(client, plan, files);
            foreach (var group in plan.Edits.Where(e => e.ImportRace == null && e.SectionRules == null && e.AddFile == null).GroupBy(e => e.File, StringComparer.OrdinalIgnoreCase))
            {
                byte[] bytes;
                string source = "this server's modules";
                if (!files.TryGetValue(group.Key, out bytes))
                {
                    bytes = FromClient(client, group.Key, out source);
                    if (bytes == null) throw new InvalidOperationException("The game client has no " + group.Key + ", which module " + group.First().Module + " changes. Is this the game folder of Conquest of Azeroth?");
                }
                foreach (var edit in group)
                    bytes = edit.DropRows != null ? DropRows(bytes, edit, group.Key) : edit.AddRows != null ? AddRows(bytes, edit, group.Key) :
                        edit.CloneRows != null ? CloneRows(bytes, edit, group.Key) :
                        edit.FillRows != null ? FillRows(bytes, edit, group.Key) :
                        edit.SetRows != null ? SetRows(bytes, edit, group.Key) :
                        edit.CopyRows != null ? CopyRows(bytes, edit, group.Key) : InsertAfter(bytes, edit, group.Key);
                files[group.Key] = bytes;
                if (notes != null) notes.Add(group.Key + " (from " + source + ")");
            }
            // imported races, then the rules of the character creation for every added race
            Func<string, byte[]> load = name =>
            {
                byte[] b; string src;
                if (files.TryGetValue(name, out b)) return b;
                b = FromClient(client, name, out src);
                if (b != null && notes != null) notes.Add(name + " (from " + src + ")");
                return b;
            };
            Action<string, byte[]> save = (name, b) => files[name] = b;
            // files of the user's race folder (the icon of a race): taken as they are, when they exist
            foreach (var e in plan.Edits.Where(e => e.AddFile != null))
            {
                string from = Text(e.AddFile, "from");
                if (from.Length > 0 && File.Exists(from) && new FileInfo(from).Length < 16 << 20) files[e.File] = File.ReadAllBytes(from);
            }
            foreach (var e in plan.Edits.Where(e => e.ImportRace != null))
            {
                var pack = RaceImport.Find(Text(e.ImportRace, "folder"), Text(e.ImportRace, "key"));
                int race, baseRace;
                if (pack == null)
                    throw new InvalidOperationException(Text(e.ImportRace, "folder").Length == 0
                        ? "The race " + Text(e.ImportRace, "key") + " comes from a race folder, but no race folder is set (module " + e.Module + "). Open its setup page and save it."
                        : "The race folder " + Text(e.ImportRace, "folder") + " has no race \"" + Text(e.ImportRace, "key") + "\" (module " + e.Module + ").");
                if (!int.TryParse(Text(e.ImportRace, "id"), out race) || race <= 0) continue;
                if (!int.TryParse(Text(e.ImportRace, "base"), out baseRace)) baseRace = BaseRaceId(Text(e.ImportRace, "base"));
                string name = Text(e.ImportRace, "name");
                RaceImport.AddRace(load, save, pack, race, baseRace, name.Length > 0 ? name : pack.Name);
            }
            foreach (var e in plan.Edits.Where(e => e.SectionRules != null))
            {
                int race;
                if (!int.TryParse(Text(e.SectionRules, "race"), out race) || race <= 0) continue;
                RaceImport.SectionRules(load, save, race, Text(e.SectionRules, "maleOnly") == "1");
            }
            if (files.Count > 0) files[StampName] = Encoding.UTF8.GetBytes(Stamp(files) + string.Concat(plan.Imports.Select(w => "race " + w.Race + " " + w.Pack.Key + "\r\n")));
            return files;
        }

        static int BaseRaceId(string name)
        {
            switch ((name ?? "").Replace(" ", "").ToLowerInvariant())
            {
                case "orc": return 2; case "dwarf": return 3; case "nightelf": return 4; case "undead": case "scourge": return 5;
                case "tauren": return 6; case "gnome": return 7; case "troll": return 8; case "bloodelf": return 10; case "draenei": return 11;
                default: return 1;
            }
        }

        static byte[] Fill(Install inst, byte[] bytes, Dictionary<string, object> fill)
        {
            string marker = Text(fill, "marker"), template = Text(fill, "line");
            if (marker.Length == 0 || template.Length == 0) return bytes;
            object v;
            var each = fill.TryGetValue("forEach", out v) ? v as Dictionary<string, object> : null;
            if (each != null)
            {
                // One line per word of a setting: "{each}" is the word, "{conf:KEY}" the value of setting KEY (with "{each}"
                // in it), for inside a Lua string.
                Func<string, string> luaText = t => (t ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");
                string conf = Text(each, "conf");
                var made = new List<string>();
                foreach (var word in Words(ConfValue(inst, conf, Text(each, "key")) ?? Text(each, "default")))
                {
                    string one = template.Replace("{each}", word);
                    one = Regex.Replace(one, @"\{conf:([^}]+)\}", m => luaText(ConfValue(inst, conf, m.Groups[1].Value) ?? ""));
                    made.Add(one);
                }
                string all = Encoding.UTF8.GetString(bytes);
                var parts = all.Split('\n');
                for (int i = 0; i < parts.Length; i++)
                    if (parts[i].Contains(marker)) parts[i] = string.Join("\n", made) + (made.Count == 0 ? "-- " + marker : "") + (parts[i].EndsWith("\r") ? "\r" : "");
                return Encoding.UTF8.GetBytes(string.Join("\n", parts));
            }
            string value = ConfValue(inst, Text(fill, "conf"), Text(fill, "key")) ?? Text(fill, "default");
            var numbers = Regex.Matches(value, @"\d+").Cast<Match>().Select(m => int.Parse(m.Value)).Distinct().OrderBy(n => n);
            string set = "{ " + string.Join(", ", numbers.Select(n => "[" + n + "] = true")) + " }";
            Func<string, string> lua = t => t.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");
            // {set}: the numbers of the setting as a Lua set; {words}: its words as a Lua list of strings; {text}: the
            // setting itself, for inside a Lua string.
            string line = template.Replace("{set}", set)
                .Replace("{words}", "{ " + string.Join(", ", Words(value).Select(w => "\"" + lua(w) + "\"")) + " }")
                .Replace("{text}", lua(value.Trim()));
            string text = Encoding.UTF8.GetString(bytes);
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Contains(marker)) lines[i] = line + (lines[i].EndsWith("\r") ? "\r" : "");
            return Encoding.UTF8.GetBytes(string.Join("\n", lines));
        }

        static byte[] InsertAfter(byte[] bytes, EditSpec edit, string file)
        {
            string text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
            string nl = text.Contains("\r\n") ? "\r\n" : "\n";
            var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
            if (lines.Any(l => l.Trim().Equals(edit.Line.Trim(), StringComparison.OrdinalIgnoreCase))) return bytes;
            int at = lines.FindIndex(l => l.Trim().Equals(edit.InsertAfter.Trim(), StringComparison.OrdinalIgnoreCase));
            if (at < 0) throw new InvalidOperationException(file + " of the game client has no line \"" + edit.InsertAfter + "\", after which module " + edit.Module + " adds its own. The client may have changed; update the module.");
            lines.Insert(at + 1, edit.Line);
            bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var result = Encoding.UTF8.GetBytes(string.Join(nl, lines));
            return bom ? new byte[] { 0xEF, 0xBB, 0xBF }.Concat(result).ToArray() : result;
        }

        static byte[] DropRows(byte[] bytes, EditSpec edit, string file)
        {
            var table = DbcTable.Parse(bytes, file);
            object v;
            long idBelow = edit.DropRows.TryGetValue("idBelow", out v) ? Convert.ToInt64(v) : long.MaxValue;
            int field = edit.DropRows.TryGetValue("field", out v) ? Convert.ToInt32(v) : 0;
            var listed = (edit.DropRows.TryGetValue("values", out v) ? v as object[] : null) ?? new object[0];
            var values = new HashSet<uint>(listed.Select(x => Convert.ToUInt32(x)));
            if (field < 0 || field >= table.Fields) throw new InvalidOperationException(file + " has no field " + field + " (module " + edit.Module + ").");
            table.Records.RemoveAll(r => table.Field(r, 0) < idBelow && values.Contains(table.Field(r, field)));
            return table.ToBytes();
        }

        /// <summary>
        /// Rows added to a DBC table: every combination of the value lists in "cross" (one list per field, in field order),
        /// unless the table has that row already (a row the table holds twice keeps only its first). The fields keep the
        /// width the table has (CharBaseInfo.dbc of the CoA client stores a byte per field), and the rows go at the end,
        /// so the rows of the client keep their order.
        /// </summary>
        static byte[] AddRows(byte[] bytes, EditSpec edit, string file)
        {
            var table = DbcTable.Parse(bytes, file);
            object v;
            var lists = ((edit.AddRows.TryGetValue("cross", out v) ? v as object[] : null) ?? new object[0])
                .Select(l => ((l as object[]) ?? new object[0]).Select(x => Convert.ToUInt32(x)).ToArray()).ToList();
            if (lists.Count != table.Fields || lists.Any(l => l.Length == 0))
                throw new InvalidOperationException(file + " has " + table.Fields + " fields; module " + edit.Module + " gives values for " + lists.Count + ".");
            int width = table.Fields > 0 ? table.RecordSize / table.Fields : 0;
            if ((width != 1 && width != 2 && width != 4) || width * table.Fields != table.RecordSize)
                throw new InvalidOperationException(file + " has a row layout module " + edit.Module + " cannot add rows to.");
            if (width < 4 && lists.Any(l => l.Any(x => x >= (1u << (8 * width)))))
                throw new InvalidOperationException(file + " stores " + width + " byte(s) per field; a value of module " + edit.Module + " does not fit.");
            var have = new HashSet<string>();
            // A row the table holds twice takes a place in the client's list for nothing: the second one goes.
            table.Records.RemoveAll(r => !have.Add(Convert.ToBase64String(r)));
            var combo = new uint[lists.Count];
            Action<int> walk = null;
            walk = i =>
            {
                if (i == lists.Count)
                {
                    var rec = new byte[table.RecordSize];
                    for (int f = 0; f < combo.Length; f++)
                        for (int b = 0; b < width; b++) rec[f * width + b] = (byte)(combo[f] >> (8 * b));
                    if (have.Add(Convert.ToBase64String(rec))) table.Records.Add(rec);
                    return;
                }
                foreach (var value in lists[i]) { combo[i] = value; walk(i + 1); }
            };
            walk(0);
            // Some tables the client reads into lists of fixed size; more rows than that crash the game, so the module
            // names its limit: of the whole table ("maxRows"), or of the rows that share their first field ("maxPerRace":
            // the character creation of the CoA client takes 30 classes per race in CharBaseInfo.dbc).
            int most = edit.AddRows.TryGetValue("maxRows", out v) ? Convert.ToInt32(v) : int.MaxValue;
            if (table.Records.Count > most)
                throw new InvalidOperationException(file + " would have " + table.Records.Count + " rows, more than the " + most +
                    " the game takes (module " + edit.Module + "). Another module may have added rows to it too.");
            int perFirst = edit.AddRows.TryGetValue("maxPerRace", out v) ? Convert.ToInt32(v) : int.MaxValue;
            var crowded = table.Records.GroupBy(r => table.Field(r, 0)).FirstOrDefault(g => g.Count() > perFirst);
            if (crowded != null)
                throw new InvalidOperationException(file + " would have " + crowded.Count() + " rows for " + crowded.Key + ", more than the " + perFirst +
                    " the game takes (module " + edit.Module + "). Another module may have added rows to it too.");
            return table.ToBytes();
        }

        /// <summary>
        /// Rows copied within a DBC table: every row whose bytes at the offsets in "match" hold those values is copied with
        /// the bytes in "set" changed and a new id (the first field, after the highest id), unless a row with the copy's
        /// bytes at the "key" offsets exists already. For tables with byte fields, such as CharStartOutfit.dbc (the start
        /// outfit of a race, class and sex).
        /// </summary>
        static byte[] CloneRows(byte[] bytes, EditSpec edit, string file)
        {
            var table = DbcTable.Parse(bytes, file);
            object v;
            Func<string, Dictionary<int, byte>> offsets = name =>
            {
                var d = edit.CloneRows.TryGetValue(name, out v) ? v as Dictionary<string, object> : null;
                var result = new Dictionary<int, byte>();
                if (d != null) foreach (var kv in d) result[int.Parse(kv.Key)] = Convert.ToByte(kv.Value);
                return result;
            };
            var match = offsets("match"); var set = offsets("set");
            var key = ((edit.CloneRows.TryGetValue("key", out v) ? v as object[] : null) ?? new object[0]).Select(x => Convert.ToInt32(x)).ToArray();
            if (match.Count == 0 || set.Count == 0 || table.RecordSize < 4 || match.Keys.Concat(set.Keys).Concat(key).Any(o => o < 4 || o >= table.RecordSize))
                throw new InvalidOperationException("The rows module " + edit.Module + " copies in " + file + " are not described in a way it can use.");
            Func<byte[], string> keyOf = r => string.Join(",", key.Select(o => r[o]));
            bool replace = edit.CloneRows.TryGetValue("replace", out v) && v is bool && (bool)v;
            var have = new Dictionary<string, byte[]>();
            foreach (var r in table.Records) if (!have.ContainsKey(keyOf(r))) have[keyOf(r)] = r;
            uint next = table.Records.Count == 0 ? 1 : table.Records.Max(r => BitConverter.ToUInt32(r, 0)) + 1;
            foreach (var row in table.Records.Where(r => match.All(m => r[m.Key] == m.Value)).ToList())
            {
                var copy = (byte[])row.Clone();
                foreach (var s in set) copy[s.Key] = s.Value;
                byte[] existing;
                if (have.TryGetValue(keyOf(copy), out existing))
                {
                    // "replace": the row the copy would duplicate takes the copy's bytes and keeps its id.
                    if (replace && existing != row) Buffer.BlockCopy(copy, 4, existing, 4, copy.Length - 4);
                    continue;
                }
                BitConverter.GetBytes(next++).CopyTo(copy, 0);
                have[keyOf(copy)] = copy;
                table.Records.Add(copy);
            }
            return table.ToBytes();
        }

        /// <summary>
        /// Fields of rows changed in a DBC table: every row whose fields in "match" (field number: value) hold those values
        /// gets the values in "set". The fields keep the width the table stores them in. Example: making a race of
        /// ChrRaces.dbc playable for a faction.
        /// </summary>
        static byte[] SetRows(byte[] bytes, EditSpec edit, string file)
        {
            var table = DbcTable.Parse(bytes, file);
            object v;
            // A value in quotes is a text: it goes into the table's strings, and the field takes its offset.
            Func<string, Dictionary<int, uint>> fields = name =>
            {
                var d = edit.SetRows.TryGetValue(name, out v) ? v as Dictionary<string, object> : null;
                var result = new Dictionary<int, uint>();
                if (d != null) foreach (var kv in d) result[int.Parse(kv.Key)] = kv.Value is string ? table.AddString((string)kv.Value) : Convert.ToUInt32(kv.Value);
                return result;
            };
            var match = fields("match"); var set = fields("set");
            if (match.Count == 0 || set.Count == 0 || match.Keys.Concat(set.Keys).Any(f => f < 0 || f >= table.Fields))
                throw new InvalidOperationException("The fields module " + edit.Module + " changes in " + file + " are not described in a way it can use.");
            int changed = 0;
            foreach (var row in table.Records.Where(r => match.All(m => table.Field(r, m.Key) == m.Value)))
            {
                foreach (var s in set) table.SetField(row, s.Key, s.Value);
                changed++;
            }
            if (changed == 0)
                throw new InvalidOperationException(file + " of the game client has no row module " + edit.Module + " changes. The client may have changed; update the module.");
            return table.ToBytes();
        }

        /// <summary>
        /// Rows copied within a DBC table, by field: every row whose fields in "match" (field number: value) hold those
        /// values is copied with the fields in "set" changed. With "newId" the copy gets a new id (field 0, after the
        /// highest). A copy is left out when a row agrees with it in the fields of "key" already (default: all fields but
        /// the id). The fields keep the width the table stores them in. Example: the skins and hair of a race of the
        /// client, copied to the number a module gives that race.
        /// </summary>
        /// <summary>
        /// Ids of copied rows that tables share: the HD tables of the Ascension client pair with their plain ones by id
        /// (HDCharSections row 8500 is the HD version of CharSections row 8500). copyRows with "idGroup" (a name) and
        /// "idKey" (what the copies are for, such as the new race) gives the copies the same new ids in every table of
        /// the group: each key gets a block of ids above the highest id of all tables in the group, its rows keep their
        /// distance to each other, and the HD copy of a row has the id of its plain copy.
        /// </summary>
        static void IdShifts(string client, Plan plan, SortedDictionary<string, byte[]> files)
        {
            var groups = new Dictionary<string, List<EditSpec>>(StringComparer.OrdinalIgnoreCase);
            object v;
            foreach (var e in plan.Edits)
                if (e.CopyRows != null && e.CopyRows.TryGetValue("idGroup", out v) && Convert.ToString(v).Length > 0)
                {
                    List<EditSpec> list;
                    if (!groups.TryGetValue(Convert.ToString(v), out list)) groups[Convert.ToString(v)] = list = new List<EditSpec>();
                    list.Add(e);
                }
            var tables = new Dictionary<string, DbcTable>(StringComparer.OrdinalIgnoreCase);
            Func<string, DbcTable> table = name =>
            {
                DbcTable t;
                if (tables.TryGetValue(name, out t)) return t;
                byte[] bytes; string source;
                if (!files.TryGetValue(name, out bytes)) bytes = FromClient(client, name, out source);
                if (bytes == null) throw new InvalidOperationException("The game client has no " + name + ".");
                return tables[name] = DbcTable.Parse(bytes, name);
            };
            foreach (var g in groups.Values)
            {
                long next = g.Select(e => e.File).Distinct(StringComparer.OrdinalIgnoreCase).Select(table)
                    .Max(t => t.Records.Count == 0 ? 0L : t.Records.Max(r => (long)t.Field(r, 0))) + 1;
                // the ids the copies of each key are made from, over all tables of the group
                var spans = new List<KeyValuePair<string, long[]>>();
                foreach (var e in g)
                {
                    var t = table(e.File);
                    var match = new Dictionary<int, uint>();
                    var d = e.CopyRows.TryGetValue("match", out v) ? v as Dictionary<string, object> : null;
                    if (d != null) foreach (var kv in d) match[int.Parse(kv.Key)] = Convert.ToUInt32(kv.Value);
                    string key = e.CopyRows.TryGetValue("idKey", out v) ? Convert.ToString(v) : "";
                    var ids = t.Records.Where(r => match.All(m => t.Field(r, m.Key) == m.Value)).Select(r => (long)t.Field(r, 0)).ToList();
                    if (ids.Count == 0) continue;
                    int at = spans.FindIndex(s => s.Key == key);
                    if (at < 0) spans.Add(new KeyValuePair<string, long[]>(key, new[] { ids.Min(), ids.Max() }));
                    else spans[at] = new KeyValuePair<string, long[]>(key, new[] { Math.Min(spans[at].Value[0], ids.Min()), Math.Max(spans[at].Value[1], ids.Max()) });
                }
                var shift = new Dictionary<string, long>();
                foreach (var s in spans)
                {
                    shift[s.Key] = next - s.Value[0];
                    next += s.Value[1] - s.Value[0] + 1;
                }
                if (next > uint.MaxValue) throw new InvalidOperationException("Too many rows to copy into " + g[0].File + ".");
                foreach (var e in g)
                {
                    long n;
                    if (shift.TryGetValue(e.CopyRows.TryGetValue("idKey", out v) ? Convert.ToString(v) : "", out n)) e.IdShift = n;
                }
            }
        }

        static byte[] CopyRows(byte[] bytes, EditSpec edit, string file)
        {
            var table = DbcTable.Parse(bytes, file);
            object v;
            // A value in quotes is a text: it goes into the table's strings, and the field takes its offset.
            Func<string, Dictionary<int, uint>> fields = name =>
            {
                var d = edit.CopyRows.TryGetValue(name, out v) ? v as Dictionary<string, object> : null;
                var result = new Dictionary<int, uint>();
                if (d != null) foreach (var kv in d) result[int.Parse(kv.Key)] = kv.Value is string ? table.AddString((string)kv.Value) : Convert.ToUInt32(kv.Value);
                return result;
            };
            var match = fields("match"); var set = fields("set");
            bool newId = edit.IdShift.HasValue || (edit.CopyRows.TryGetValue("newId", out v) && v is bool && (bool)v);
            var key = ((edit.CopyRows.TryGetValue("key", out v) ? v as object[] : null) ?? new object[0]).Select(x => Convert.ToInt32(x)).ToArray();
            if (key.Length == 0) key = Enumerable.Range(newId ? 1 : 0, Math.Max(0, table.Fields - (newId ? 1 : 0))).ToArray();
            if (match.Count == 0 || set.Count == 0 || match.Keys.Concat(set.Keys).Concat(key).Any(f => f < 0 || f >= table.Fields))
                throw new InvalidOperationException("The rows module " + edit.Module + " copies in " + file + " are not described in a way it can use.");
            Func<byte[], string> keyOf = r => string.Join(",", key.Select(f => table.Field(r, f)));
            var have = new HashSet<string>(table.Records.Select(keyOf));
            uint next = table.Records.Count == 0 ? 1 : table.Records.Max(r => table.Field(r, 0)) + 1;
            foreach (var row in table.Records.Where(r => match.All(m => table.Field(r, m.Key) == m.Value)).ToList())
            {
                var copy = (byte[])row.Clone();
                foreach (var f in set) table.SetField(copy, f.Key, f.Value);
                if (!have.Add(keyOf(copy))) continue;
                if (edit.IdShift.HasValue) table.SetField(copy, 0, (uint)(table.Field(row, 0) + edit.IdShift.Value));
                else if (newId) table.SetField(copy, 0, next++);
                table.Records.Add(copy);
            }
            return table.ToBytes();
        }

        /// <summary>
        /// Rows of a DBC table filled from a sibling: a row whose bytes at a "match" offset hold one of its values and whose
        /// 32-bit numbers in "empty" are all 0 or below gets the bytes in "copy" from a row that agrees with it at the "same"
        /// offsets and is not empty - first from a row whose byte at "prefer.offset" is in the same list of "prefer.groups".
        /// CharStartOutfit.dbc: an outfit of the CoA client that only shows armour, without items, takes the items of the
        /// same class and sex of another race of the same faction.
        /// </summary>
        static byte[] FillRows(byte[] bytes, EditSpec edit, string file)
        {
            var table = DbcTable.Parse(bytes, file);
            var spec = edit.FillRows;
            object v;
            Func<string, int[]> ints = name => ((spec.TryGetValue(name, out v) ? v as object[] : null) ?? new object[0]).Select(x => Convert.ToInt32(x)).ToArray();
            var match = new Dictionary<int, HashSet<byte>>();
            if (spec.TryGetValue("match", out v) && v is Dictionary<string, object>)
                foreach (var kv in (Dictionary<string, object>)v)
                    match[int.Parse(kv.Key)] = new HashSet<byte>(((kv.Value as object[]) ?? new[] { kv.Value }).Select(x => Convert.ToByte(x)));
            int[] empty = ints("empty"), copy = ints("copy"), same = ints("same");
            int preferAt = -1; var groups = new List<HashSet<byte>>();
            if (spec.TryGetValue("prefer", out v) && v is Dictionary<string, object>)
            {
                var p = (Dictionary<string, object>)v;
                preferAt = p.TryGetValue("offset", out v) ? Convert.ToInt32(v) : -1;
                if (p.TryGetValue("groups", out v) && v is object[])
                    foreach (var g in (object[])v) groups.Add(new HashSet<byte>(((g as object[]) ?? new object[0]).Select(x => Convert.ToByte(x))));
            }
            int size = table.RecordSize;
            if (empty.Length != 2 || copy.Length != 2 || empty[0] < 4 || empty[1] > size || (empty[1] - empty[0]) % 4 != 0 || copy[0] < 4 || copy[1] > size ||
                copy[0] >= copy[1] || same.Any(o => o < 4 || o >= size) || match.Keys.Any(o => o < 4 || o >= size) || preferAt >= size)
                throw new InvalidOperationException("The rows module " + edit.Module + " fills in " + file + " are not described in a way it can use.");
            Func<byte[], bool> isEmpty = r => { for (int o = empty[0]; o < empty[1]; o += 4) if (BitConverter.ToInt32(r, o) > 0) return false; return true; };
            var donors = table.Records.Where(r => !isEmpty(r)).ToList();
            foreach (var row in table.Records)
            {
                if (!match.All(m => m.Value.Contains(row[m.Key])) || !isEmpty(row)) continue;
                var fitting = donors.Where(d => same.All(o => d[o] == row[o])).ToList();
                if (preferAt >= 4)
                {
                    var group = groups.FirstOrDefault(g => g.Contains(row[preferAt]));
                    var near = group == null ? null : fitting.Where(d => group.Contains(d[preferAt])).OrderBy(d => d[preferAt]).FirstOrDefault();
                    if (near != null) fitting.Insert(0, near);
                }
                var donor = fitting.FirstOrDefault();
                if (donor != null) Buffer.BlockCopy(donor, copy[0], row, copy[0], copy[1] - copy[0]);
            }
            return table.ToBytes();
        }

        static string Stamp(SortedDictionary<string, byte[]> files)
        {
            using (var sha = SHA256.Create())
            {
                foreach (var f in files)
                {
                    if (f.Key.Equals(StampName, StringComparison.OrdinalIgnoreCase)) continue;
                    var name = Encoding.UTF8.GetBytes(f.Key.ToLowerInvariant() + "\n");
                    sha.TransformBlock(name, 0, name.Length, null, 0);
                    sha.TransformBlock(f.Value, 0, f.Value.Length, null, 0);
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                return "Made by " + Product.Name + " for the game client it was built from.\r\nContents " + BitConverter.ToString(sha.Hash).Replace("-", "").Substring(0, 16) + "\r\n";
            }
        }

        /// <summary>The stamp of the patch in the game folder: null without one, "" when the archive there is not ours.</summary>
        static string InstalledStamp(string client)
        {
            string path = Path.Combine(client, "Data", ArchiveName);
            if (!File.Exists(path)) return null;
            try
            {
                using (var mpq = new MpqArchive(path))
                {
                    var b = mpq.Read(StampName);
                    string stamp = b == null ? "" : Encoding.UTF8.GetString(b);
                    return stamp.StartsWith("Made by " + Product.Name) ? stamp : "";
                }
            }
            catch { return ""; }
        }

        const string Foreign = "Data\\" + ArchiveName + " in the game folder was not made by " + Product.Name + ". Rename or remove it, then install again.";

        // ---------------------------------------------------------------------------- files next to the exe

        static string RootRecord(Install inst) { return Path.Combine(inst.Root, "Dependencies", "client-root-files.txt"); }

        static string Hash(string path)
        {
            using (var sha = System.Security.Cryptography.SHA1.Create())
            using (var f = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "");
        }

        static void CheckRootFiles(Plan plan)
        {
            foreach (var r in plan.RootFiles)
                if (string.IsNullOrEmpty(r.From) || !File.Exists(r.From))
                    throw new InvalidOperationException(r.Module + " wants " + r.To + " next to the game, from " + (string.IsNullOrEmpty(r.From) ? "a file not chosen yet" : r.From) +
                        ", which is not there. Open its setup page and choose the file again, or switch that option off.");
        }

        /// <summary>The files next to the exe put there before: name -> hash.</summary>
        static Dictionary<string, string> RootPlaced(Install inst)
        {
            var placed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(RootRecord(inst)))
                foreach (var line in File.ReadAllLines(RootRecord(inst)))
                {
                    var parts = line.Split('\t');
                    if (parts.Length == 2) placed[parts[0]] = parts[1];
                }
            return placed;
        }

        static bool RootFilesCurrent(Install inst, string client, Plan plan)
        {
            var placed = RootPlaced(inst);
            foreach (var r in plan.RootFiles)
            {
                string dest = Path.Combine(client, r.To);
                if (!File.Exists(dest) || Hash(dest) != Hash(r.From)) return false;
            }
            // one placed before and no longer wanted is still there
            foreach (var p in placed)
                if (!plan.RootFiles.Any(r => r.To.Equals(p.Key, StringComparison.OrdinalIgnoreCase)) && File.Exists(Path.Combine(client, p.Key)) && Hash(Path.Combine(client, p.Key)) == p.Value)
                    return false;
            return true;
        }

        /// <summary>Puts the wanted files next to the exe and takes back those no longer wanted. A file of the same name
        /// that was there before is kept as &lt;name&gt;.afk-backup and comes back when ours goes.</summary>
        static List<string> SyncRootFiles(Install inst, string client, Plan plan)
        {
            var notes = new List<string>();
            var placed = RootPlaced(inst);
            var now = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var r in plan.RootFiles)
                {
                    string dest = Path.Combine(client, r.To), want = Hash(r.From);
                    if (File.Exists(dest) && Hash(dest) == want) { now[r.To] = want; continue; }
                    string mine;
                    if (File.Exists(dest) && !(placed.TryGetValue(r.To, out mine) && Hash(dest) == mine) && !File.Exists(dest + ".afk-backup"))
                        File.Move(dest, dest + ".afk-backup");
                    File.Copy(r.From, dest, true);
                    now[r.To] = want;
                    notes.Add(r.To + " placed next to the game");
                }
                foreach (var p in placed)
                {
                    if (now.ContainsKey(p.Key)) continue;
                    string dest = Path.Combine(client, p.Key);
                    if (File.Exists(dest) && Hash(dest) == p.Value)
                    {
                        File.Delete(dest);
                        if (File.Exists(dest + ".afk-backup")) File.Move(dest + ".afk-backup", dest);
                        notes.Add(p.Key + " taken away");
                    }
                }
            }
            catch (IOException ex) { throw new InvalidOperationException("A file next to the game could not be changed (" + ex.Message + "). Close the game and its launcher first."); }
            catch (UnauthorizedAccessException ex) { throw new InvalidOperationException("A file next to the game could not be changed (" + ex.Message + ")."); }
            Directory.CreateDirectory(Path.GetDirectoryName(RootRecord(inst)));
            if (now.Count == 0) { if (File.Exists(RootRecord(inst))) File.Delete(RootRecord(inst)); }
            else File.WriteAllLines(RootRecord(inst), now.Select(p => p.Key + "\t" + p.Value), new UTF8Encoding(false));
            return notes;
        }

        /// <summary>Whether the client has the patch the modules want. Building it reads the client, which takes a moment.</summary>
        public static PatchState StateOf(Install inst, string client, Plan plan, out string problem)
        {
            problem = null;
            string installed = client == null ? null : InstalledStamp(client);
            if (plan.Empty) return (!string.IsNullOrEmpty(installed) && WasOurs(inst)) || (client != null && !RootFilesCurrent(inst, client, plan)) ? PatchState.Leftover : PatchState.None;
            if (client == null) return PatchState.Missing;
            if (installed == "") { problem = Foreign; return PatchState.Outdated; }
            try
            {
                var files = Build(inst, client, plan, null);
                string wanted = Encoding.UTF8.GetString(files[StampName]);
                if (installed == null) return PatchState.Missing;
                CheckRootFiles(plan);
                return installed == wanted && RaceImport.Current(client, plan.Imports) && RootFilesCurrent(inst, client, plan) ? PatchState.Current : PatchState.Outdated;
            }
            catch (Exception ex) { problem = ex.Message; return PatchState.Outdated; }
        }

        static bool WasOurs(Install inst) { return File.Exists(RecordFile(inst)); }

        /// <summary>Builds the patch and puts it into the game folder, or takes it out when no module needs it any more.</summary>
        public static string Install(Install inst, string client, Plan plan)
        {
            string path = Path.Combine(client, "Data", ArchiveName);
            string installed = InstalledStamp(client);
            if (plan.Empty)
            {
                var gone = SyncRootFiles(inst, client, plan);
                if (!string.IsNullOrEmpty(installed) && WasOurs(inst))
                {
                    try { File.Delete(path); }
                    catch (IOException) { throw new InvalidOperationException("Data\\" + ArchiveName + " is in use. Close the game and its launcher first."); }
                    File.Delete(RecordFile(inst));
                    return "the client patch removed (no module needs it any more)" + (gone.Count > 0 ? ", " + string.Join(", ", gone) : "");
                }
                return string.Join(", ", gone);
            }
            if (installed == "") throw new InvalidOperationException(Foreign);
            CheckRootFiles(plan);
            var notes = new List<string>();
            var files = Build(inst, client, plan, notes);
            string temp = path + ".new";
            MpqArchive.Write(temp, files);
            try
            {
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            catch (IOException)
            {
                try { File.Delete(temp); } catch { }
                throw new InvalidOperationException("Data\\" + ArchiveName + " is in use. Close the game and its launcher first.");
            }
            notes.AddRange(RaceImport.Sync(client, plan.Imports));
            notes.AddRange(SyncRootFiles(inst, client, plan));
            Directory.CreateDirectory(Path.GetDirectoryName(RecordFile(inst)));
            File.WriteAllLines(RecordFile(inst), new[] { ArchiveName }.Concat(plan.Modules), new UTF8Encoding(false));
            return "the client patch Data\\" + ArchiveName + " built for " + string.Join(", ", plan.Modules) + (notes.Count > 0 ? " (changes " + string.Join(", ", notes) + ")" : "");
        }

        /// <summary>Puts the patch into a ZIP for other players, as Data\patch-Y.MPQ.</summary>
        public static void AddToZip(ZipArchive zip, Install inst, string client, Plan plan)
        {
            var files = Build(inst, client, plan, null);
            string temp = Path.GetTempFileName();
            try
            {
                MpqArchive.Write(temp, files);
                zip.CreateEntryFromFile(temp, "Data/" + ArchiveName, CompressionLevel.Optimal);
            }
            finally { try { File.Delete(temp); } catch { } }
            CheckRootFiles(plan);
            foreach (var r in plan.RootFiles) zip.CreateEntryFromFile(r.From, r.To, CompressionLevel.Optimal);
            foreach (var w in plan.Imports)
            {
                string race = RaceImport.BuildTemp(w);
                try { zip.CreateEntryFromFile(race, "Data/" + RaceImport.ArchiveName(w.Race), CompressionLevel.Fastest); }
                finally { try { File.Delete(race); } catch { } }
            }
        }
    }
}
