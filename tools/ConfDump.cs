// Prints, as a JSON list, the texts AFK Realm shows for the options of .conf.dist templates: their sections,
// descriptions and the names of their choices, exactly as the settings window reads them.
// Built and run by tools/lang.py (--module) together with src/*.cs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace CoAInstaller
{
    static class ConfDump
    {
        static void Main(string[] args)
        {
            var texts = new List<string>();
            foreach (var dist in args)
            {
                string conf = dist.Substring(0, dist.Length - ".dist".Length);
                var file = ConfigFile.Load(conf, Path.GetFileName(conf));
                foreach (var o in file.Options)
                {
                    Add(texts, o.Section);
                    Add(texts, o.DescriptionBody ?? o.Description);
                    foreach (var c in o.Choices) Add(texts, c.Value);
                }
            }
            Console.Write(new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(texts));
        }

        static void Add(List<string> texts, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            text = text.Replace("\r\n", "\n").Trim();
            if (!texts.Contains(text)) texts.Add(text);
        }
    }
}
