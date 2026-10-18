using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace CoAInstaller
{
    /// <summary>The racial rows a race of the server pack brings. The server data folder holds them as its own dump
    /// (races\racial-spell-records.json); mod-custom-races reads the rows of every race it has turned on from
    /// afk-realm\races\&lt;key&gt;.sql of that folder. This writer keeps those files in step with the races that are on,
    /// and takes the file of a race without racials away, so a race that is turned off leaves its rows behind.</summary>
    static class ServerRacials
    {
        /// <summary>The ported racial abilities mod-custom-races has code for: its script, the spell the script runs, and
        /// the spells its code needs besides (their rows travel with the race as well, see src\CustomRaces.cpp).</summary>
        static readonly string[][] Ported =
        {
            new[] { "spell_race_bull_rush", "110600", "110601" },
        };

        /// <summary>The racial spell ids the server pack gives a race, as its entry writes them (script_grants).</summary>
        public static uint[] Spells(string serverDataFolder, string raceName)
        {
            var grants = Grants(serverDataFolder);
            List<uint> found;
            foreach (var key in Keys(raceName))
                if (grants.TryGetValue(key, out found)) return found.ToArray();
            return new uint[0];
        }

        /// <summary>The racial spells of a race as far as the client needs them too: the ones the server grants and the
        /// spells its ported scripts need besides (they come as an aura onto their targets, see CustomRaces.cpp).</summary>
        public static uint[] ClientSpells(string serverDataFolder, string raceName)
        {
            var spells = Spells(serverDataFolder, raceName).ToList();
            foreach (var ported in Ported)
                if (spells.Contains(uint.Parse(ported[1]))) spells.AddRange(ported.Skip(2).Select(uint.Parse));
            return spells.Distinct().OrderBy(s => s).ToArray();
        }

        /// <summary>Writes the racial file of every race that is on, and takes the file of a race whose set is empty
        /// away. A server data folder without race data is left alone.</summary>
        public static void Sync(string serverDataFolder, Dictionary<string, string> races)
        {
            if (races == null || string.IsNullOrEmpty(serverDataFolder) || !Directory.Exists(serverDataFolder)) return;
            if (Grants(serverDataFolder).Count == 0) return;
            var records = Records(serverDataFolder);
            string dir = Path.Combine(serverDataFolder, "afk-realm", "races");
            foreach (var race in races)
            {
                var spells = ClientSpells(serverDataFolder, race.Value).Where(records.ContainsKey).ToList();
                string file = Path.Combine(dir, race.Key + ".sql");
                if (spells.Count == 0) { if (File.Exists(file)) File.Delete(file); continue; }
                var lines = new List<string>();
                lines.Add("-- spells " + string.Join(" ", spells));
                foreach (uint spell in spells)
                {
                    lines.Add("DELETE FROM `spell_dbc` WHERE `ID` = " + spell + ";");
                    lines.Add(Insert("spell_dbc", records[spell]));
                }
                // the scripts stay with the spells the module has ported; other races keep their rows plain
                foreach (var ported in Ported.Where(p => spells.Contains(uint.Parse(p[1]))))
                {
                    lines.Add("DELETE FROM `spell_script_names` WHERE `spell_id` = " + ported[1] + ";");
                    lines.Add("INSERT INTO `spell_script_names` (`spell_id`, `ScriptName`) VALUES (" + ported[1] + ",'" + ported[0] + "');");
                }
                Directory.CreateDirectory(dir);
                File.WriteAllText(file, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(false));
            }
        }

        /// <summary>A spell record as SQL writes it: it carries its own column names, whoever wrote the dump.</summary>
        static string Insert(string table, List<KeyValuePair<string, object>> fields)
        {
            var cols = new List<string>();
            var values = new List<string>();
            foreach (var field in fields)
            {
                cols.Add("`" + field.Key + "`");
                values.Add(Literal(field.Value));
            }
            return "INSERT INTO `" + table + "` (" + string.Join(", ", cols) + ") VALUES (" + string.Join(", ", values) + ");";
        }

        /// <summary>A value as SQL writes it: a number plain, a text in quotes.</summary>
        static string Literal(object value)
        {
            if (value == null) return "NULL";
            if (value is string) return "'" + ((string)value).Replace("\\", "\\\\").Replace("'", "''") + "'";
            if (value is bool) return (bool)value ? "1" : "0";
            if (value is double || value is float || value is decimal) return Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        // ---------------------------------------------------------------- the race data of the folder

        static string readIn;                        // the folder the last read was taken from
        static Dictionary<string, List<uint>> readGrants;
        static string recordsIn;
        static Dictionary<uint, List<KeyValuePair<string, object>>> readRecords;

        /// <summary>The racial spell ids of the folder's races, by every shape their name may be written in.</summary>
        static Dictionary<string, List<uint>> Grants(string serverDataFolder)
        {
            serverDataFolder = serverDataFolder ?? "";
            if (readGrants != null && readIn == serverDataFolder) return readGrants;
            var map = new Dictionary<string, List<uint>>();
            string dir = Path.Combine(serverDataFolder, "races");
            if (Directory.Exists(dir))
            {
                var json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                foreach (var file in Directory.GetFiles(dir, "*.json"))
                {
                    if (file.EndsWith("racial-spell-records.json", StringComparison.OrdinalIgnoreCase)) continue;
                    object name, spells;
                    try
                    {
                        var entry = json.DeserializeObject(File.ReadAllText(file, Encoding.UTF8)) as Dictionary<string, object>;
                        if (entry == null || !entry.TryGetValue("name", out name) || !entry.TryGetValue("script_grants", out spells)) continue;
                    }
                    catch { continue; }   // what is not a race entry is left out
                    var ids = (spells as object[] ?? new object[0]).Select(s => (uint)Convert.ToInt64(s)).ToList();
                    if (ids.Count == 0) continue;
                    foreach (var key in Keys(name as string).Concat(Keys(Path.GetFileNameWithoutExtension(file))))
                        if (!map.ContainsKey(key)) map[key] = ids;
                }
            }
            readIn = serverDataFolder; readGrants = map;
            return map;
        }

        /// <summary>The spell records of the folder (its overrides of the spell table), by spell id. Records that only
        /// repeat the client's own spell table are left out: their rows already live on the server.</summary>
        static Dictionary<uint, List<KeyValuePair<string, object>>> Records(string serverDataFolder)
        {
            serverDataFolder = serverDataFolder ?? "";
            if (readRecords != null && recordsIn == serverDataFolder) return readRecords;
            var map = new Dictionary<uint, List<KeyValuePair<string, object>>>();
            string file = Path.Combine(serverDataFolder, "races", "racial-spell-records.json");
            if (File.Exists(file))
            {
                var json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                foreach (var item in json.DeserializeObject(File.ReadAllText(file, Encoding.UTF8)) as object[] ?? new object[0])
                {
                    var entry = item as Dictionary<string, object>;
                    object source, id, fields;
                    if (entry == null || !entry.TryGetValue("source", out source) || !(source is string) || (string)source != "spell_dbc override") continue;
                    if (!entry.TryGetValue("spell_id", out id) || !entry.TryGetValue("fields", out fields)) continue;
                    var values = fields as Dictionary<string, object>;
                    if (values != null) map[(uint)Convert.ToInt64(id)] = values.ToList();
                }
            }
            recordsIn = serverDataFolder; readRecords = map;
            return map;
        }

        /// <summary>The shapes a race name may be written in: "Highmountain Tauren", "HighmountainTauren", or a file
        /// name as "46-highmountain-tauren-horde" - all three give one word of letters and digits.</summary>
        static IEnumerable<string> Keys(string text)
        {
            string flat = Flat(text);
            if (flat.Length > 0) yield return flat;
            var parts = (text ?? "").Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count > 0 && parts[0].All(char.IsDigit)) parts.RemoveAt(0);
            if (parts.Count > 0 && (parts[parts.Count - 1].Equals("horde", StringComparison.OrdinalIgnoreCase) || parts[parts.Count - 1].Equals("alliance", StringComparison.OrdinalIgnoreCase))) parts.RemoveAt(parts.Count - 1);
            string bare = Flat(string.Concat(parts));
            if (bare.Length > 0 && bare != flat) yield return bare;
        }

        static string Flat(string text) { return new string((text ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray()); }
    }
}
