using System;
using System.IO;
using System.Windows.Forms;

namespace CoAInstaller
{
    /// <summary>
    /// The main settings of Immersion Life (the AFK Realm continuation of City Life). The module has
    /// hundreds of options; this page exposes the ones a server owner changes first, and the file
    /// itself stays available for everything else. The database button applies the module's manual
    /// WORLD/CHARACTERS files once, in the order its README asks for.
    /// </summary>
    class ImmersionSetup : SetupDialog
    {
        const string Module = "mod-playerbots-immersion";

        public ImmersionSetup(Install i) : base(i, "mod_playerbots_immersion.conf", L.T("Immersion Life"),
            L.T("mod-playerbots-immersion  -  modules\\mod_playerbots_immersion.conf"), "accounts", "CityLife.Enable", L.T("Immersion Life on"))
        {
            // ---- cities
            Tab(L.T("Cities"));
            var population = Card(L.T("City population"),
                L.T("These bots are borrowed from the normal playerbot pool and reserved while they stay. The limit is shared by city residents, questers, legendary characters and fishers; PvP has its own budget."), "accounts");
            AddSlider(population, "CityLife.Population.MaxTotal", L.T("At most at once"), 20, 1000, 10, v => ((int)v).ToString());
            AddToggle(population, "CityLife.Time.Enable", L.T("More people in the evening"),
                L.T("The population follows the time of day: fewer in the morning and at night, full in the evening."));

            var towns = Card(L.T("Cities and towns"),
                L.T("How many residents each place asks for. Values are requests; only free, eligible bots are used."), "globe");
            Town(towns, "CityLife.Hub.Stormwind.Population", L.T("Stormwind"));
            Town(towns, "CityLife.Hub.Orgrimmar.Population", L.T("Orgrimmar"));
            Town(towns, "CityLife.Hub.Ironforge.Population", L.T("Ironforge"));
            Town(towns, "CityLife.Hub.Darnassus.Population", L.T("Darnassus"));
            Town(towns, "CityLife.Hub.Exodar.Population", L.T("Exodar"));
            Town(towns, "CityLife.Hub.Undercity.Population", L.T("Undercity"));
            Town(towns, "CityLife.Hub.ThunderBluff.Population", L.T("Thunder Bluff"));
            Town(towns, "CityLife.Hub.Silvermoon.Population", L.T("Silvermoon"));
            Town(towns, "CityLife.Hub.Shattrath.Population", L.T("Shattrath"));
            Town(towns, "CityLife.Hub.Dalaran.Population", L.T("Dalaran"));
            Town(towns, "CityLife.Hub.Goldshire.Population", L.T("Goldshire"));
            Note(L.T("Further towns, quest hubs and all advanced options are in the config file. Open it with \"Open the file\" below."));

            // ---- questing
            Tab(L.T("Questing"));
            var questing = Card(L.T("Bots take and complete quests"),
                L.T("Questers accept, do and turn in real quests. They are off by default and share the city limit."), "gamemaster");
            AddToggle(questing, "CityLife.Questing.Enable", L.T("Questing on"));
            Note(L.T("Questing needs the quest hubs and their data from the module's database files. Install them once on the Database tables tab."));

            // ---- legendary residents
            Tab(L.T("Legendary bots"));
            var custom = Card(L.T("Custom city characters"),
                L.T("Custom city characters include the legendary residents, beggars, buyers and training scenes."), "accounts");
            AddToggle(custom, "CityLife.Custom.Enable", L.T("Custom city characters on"));
            AddToggle(custom, "CityLife.Custom.Legendary.Enable", L.T("Legendary residents"),
                L.T("The legendary residents keep their fixed names, outfits and veteran history. They are on by default and count inside the city limit."));
            var legendary = Card(L.T("Legendary residents per city"),
                L.T("How many legendary residents each of the three main cities asks for. Further cities are in the config file."), "accounts");
            Town(legendary, "CityLife.Custom.Legendary.Stormwind.Population", L.T("Stormwind"), 12);
            Town(legendary, "CityLife.Custom.Legendary.Orgrimmar.Population", L.T("Orgrimmar"), 12);
            Town(legendary, "CityLife.Custom.Legendary.Dalaran.Population", L.T("Dalaran"), 12);
            Note(L.T("Legendary residents stay on when PvP Life and Fishing Life are off."));

            // ---- PvP
            Tab(L.T("PvP"));
            var pvp = Card(L.T("PvP Life"),
                L.T("PvP Life brings duels, world PvP and faction campaigns. It is off by default for Conquest of Azeroth and uses its own budget."), "gamemaster");
            AddToggle(pvp, "PvPLife.Enable", L.T("PvP Life on"));
            AddSlider(pvp, "PvPLife.Bots.MaxPerSide", L.T("Bots per side"), 0, 200, 5);
            Note(L.T("PvP Life is independent of the city limit. Turn it on only after its database tables are installed."));

            // ---- fishing
            Tab(L.T("Fishing"));
            var fishing = Card(L.T("Fishing Life"),
                L.T("Fishing Life places fishers at the waterfronts. It is off by default and counts inside the city limit."), "journey");
            AddToggle(fishing, "FishingLife.Enable", L.T("Fishing Life on"));
            AddSlider(fishing, "FishingLife.CreateMissing.MaxBots", L.T("Fishers at most"), 0, 120, 1);
            Note(L.T("Fishing Life has its own bot pool for missing participants. Turn it on only after its database tables are installed."));

            // ---- database
            Tab(L.T("Database tables"));
            var database = Card(L.T("Database tables"),
                L.T("The module keeps its cities, custom characters, fishing spots and PvP locations in the world and characters databases. Install these tables once before the first start."), "database");
            var status = Small(L.T("Only needed once. Existing tables and rows are kept."), database.Inner - 40);
            var install = Ui.Secondary(L.T("Install the database tables ..."));
            install.Margin = new Padding(0, 2, 0, 6);
            database.Add(install);
            database.Add(status);
            install.Click += (s, e) => InstallDatabase(install, status);
            Note(L.T("Stop the worldserver before installing the tables. The changes show after the next start."));

            Start();
        }

        void Town(Card card, string key, string name, int max = 80)
        {
            // One step: the small towns use values such as 5 or 8, not only multiples of five.
            AddSlider(card, key, name, 0, max, 1);
        }

        void InstallDatabase(Button button, Label status)
        {
            if (!Ui.Confirm(this, L.T("Install the database tables now? This creates the module's tables in the world and characters databases. It is only needed once; existing tables and rows are kept."))) return;
            button.Enabled = false;
            status.Text = L.T("Installing the database tables ...");
            string done = null, error = null;
            var control = new ServerControl(inst);
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    // The module's tables can also be installed while the server is stopped.
                    // Start the portable database for it and leave it running for the next start.
                    if (control.Db == null && !Net.PortOpen(control.DbPort))
                    {
                        try { BeginInvoke((Action)(() => status.Text = L.T("Starting the database ..."))); } catch { }
                        control.StartDatabase(s => { });
                    }
                    ApplyDatabase();
                    done = L.T("The database tables are ready.");
                }
                catch (Exception ex) { error = ex.Message; }
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        button.Enabled = true;
                        status.Text = error == null ? done : L.T("The database tables could not be installed:") + " " + error;
                        if (error != null) Ui.Error(this, L.T("The database tables could not be installed:") + "\n\n" + error);
                    }));
                }
                catch { }
            });
        }

        void ApplyDatabase()
        {
            string folder = Path.Combine(ModuleCatalog.ModulesDir(inst), Module, "data", "sql", "manual");
            if (!Directory.Exists(folder))
                throw new InvalidOperationException(L.T("The module's database files were not found."));
            DbLogin login = DbLogin.FromConfig(inst);
            Func<string, string, bool> exists = (database, table) =>
            {
                var rows = MySql.Query(inst, login, "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = " +
                    MySql.Quote(database) + " AND TABLE_NAME = " + MySql.Quote(table) + ";");
                return rows.Count > 0 && rows[0].Trim() != "0";
            };
            Action<string, string> run = (database, name) =>
            {
                string file = Path.Combine(folder, name);
                if (!File.Exists(file)) throw new FileNotFoundException(L.T("The module's database files were not found."), file);
                MySql.RunFile(inst, login, database, file);
            };

            if (!exists("acore_world", "city_life_hub")) run("acore_world", "world_city_life.sql");
            run("acore_world", "world_city_life_quest_zones.sql");
            run("acore_world", "world_city_life_locations.sql");
            run("acore_world", "world_city_life_custom.sql");
            run("acore_world", "world_city_life_fishing.sql");
            run("acore_world", "world_city_life_fishing_spacing.sql");
            run("acore_world", "world_city_life_legendary_stormwind.sql");
            run("acore_characters", "characters_city_life_custom.sql");
            run("acore_characters", "characters_city_life_custom_bots.sql");
            if (!exists("acore_world", "pvp_life_zone")) run("acore_world", "world_pvp_life.sql");
            run("acore_world", "world_pvp_life_locations.sql");
        }
    }
}
