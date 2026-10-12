# Changelog

## 0.9.5-preview (in progress)

- Languages: AFK Realm speaks English, German, French, Spanish, Portuguese (Brazil), Russian and Simplified Chinese. It starts in the language of Windows (English when there is none for it) and the language button at the top right of every page switches it; AFK Realm then restarts itself, running servers keep running. "Add a language …" in the same menu writes a file with every text of AFK Realm into `%APPDATA%\AFK-Realm\lang` to translate; files there (or in a `lang` folder next to the exe) are picked up at the next start and can also replace single texts of a built-in language. Chinese uses Microsoft YaHei UI
- Languages of the modules: a module can bring `lang/<code>.json` with the texts AFK Realm shows of it (names and reasons in afk-realm.json, its option descriptions, races and zone names); the four modules by AFK Realm carry all six languages, zones and dungeons with their names in the game's own language
- Messages of the engine (steps of an installation, errors, module notes) are shown in the language of the window; the log stays English, and so do announcements in the game, file names and the batch file of the map data. The READ ME in the ZIP for other players is written in the owner's language and in English
- Settings: an option described in a block of its own after another option's description (`CustomRaces.MoreRacesFile`) no longer shows both descriptions
- Setup pages: a choice whose buttons do not fit beside its label goes below it; bots page and game master tools make room for longer texts
- `tools/lang.py` lists, checks and merges the texts of AFK Realm and of a module (`--module <folder>`)
- Update check: before an update, the patches of the installed modules (in their newest version) are tried on the new CoA core and mod-playerbots; one that would not fit is named in the banner and in the question before the update

## 0.9.4-preview

- Several servers: the top of the side bar shows the server you look at (its realm's name) and opens the list of every server opened in AFK Realm, to switch between them with one click; "Add an existing server …" and "Install a new server …" are there too, and a server can be removed from the list (its folder stays). Switching away from a running server leaves it running

## 0.9.3-preview

- Custom races: after new races were made playable on the races page, AFK Realm offers to make the random bots anew, since the bots keep the races they were made with and only new bots can be of the new races

## 0.9.2-preview

- Custom races: "Create race packs from a client …" on the setup page reads the races of another client or repack (a game folder, or a folder with its patch archives such as the client folder of CoA Custom) and puts the ones you tick into the race folder: their rows, the files your own client lacks and their icon. The other client is no longer needed afterwards. Texture names of models are read in full (some were cut or padded before)
- Custom races: more than 31 races with a 64-race patch of the game client (the dinput8.dll of CoA Custom, kept in the race folder when you make race packs from it). On the setup page under "More than 31 races"; AFK Realm puts the DLL next to Ascension.exe when you install the client patch, keeps a DLL of the same name that was there as .afk-backup, takes it away again when the option is off, and adds it to the ZIP. Races above 32 do what their base race may do (core patch of mod-custom-races: races without a bit of their own use their base race's bit in race masks)
- Client patches: "rootFiles" puts a file of the user's own next to the game's exe while a setting asks for it
- Custom races: races of the race folder are sorted into the groups by looks like the others, not into a group of their own; a race of the folder leans by default on a race of the side it had in its client
- Custom races: the race folder found by itself (Downloads\AFK-Rassen) is saved, and a race whose folder is missing stops the build with a message instead of being left out unseen. Building the client patch with imported races takes seconds instead of minutes. The race columns of the character creation are further apart so their names no longer overlap
- The game archives AFK Realm reads may hold encrypted files
- No more frozen windows with large races: checking the game client, installing and the ZIP run in the background with a window that says what is going on and how far it is (each race archive file by file); the main window checks the game client in the background after a setup page or the game client window closes; the races page reads the game client and the race folder before it opens, with a progress window
- Custom races: a skin or face of a race from the race folder that has no HD row of its own (some skin colours of CoA Custom's Undead Gnome) takes the HD row of the same style in the nearest colour; before, its old texture landed askew on the HD model (eyes in the wrong place)
- Bots page: races added by a module show their names (not "Race 23") and count for their faction in Alliance : Horde
- Faster: a race of the race folder keeps a small summary beside its race.json (race.info.json), so its large race.json is read only for a build; the races of the game client and the client patch to compare with are worked out once and kept until the client, a setting or a module changes

## 0.9.1-preview

- Client patches: "setRows" changes fields of the rows of a DBC table that match (ChrRaces.dbc: a race of the client made playable for a faction); "cloneRows" can "replace" the row a copy would duplicate; "addRows" takes a "maxPerRace" limit (the character creation of the CoA client takes 30 classes per race in CharBaseInfo.dbc)
- Modules: a module that writes rows while the server runs brings SQL in data/sql/remove that takes them back; AFK Realm runs it when the module is removed, before the recorded changes are undone
- New module under "Modules by AFK Realm": mod-custom-races - the races the game client carries but no player can choose (in the CoA client: Goblin, Worgen, Pandaren, Vrykul, Naga and more, 23 in all) playable for players and bots, each leaning on a stock race of your choice for its start, quests, racial abilities and faction; with a setup page (Modules → Custom races) that reads the races from your own game client - after an update of the game too - and hands out its free race numbers; a race without helmet models of its own keeps helmets hidden, for players and bots
- Custom races from other game clients: a race folder (one folder per race with its rows in race.json and its files) is offered on the setup page next to the races of your client; a race of your client can take the looks of the race folder instead. The rows go into the client patch, the files into an archive of their own for each race (Data\patch-ZY<number>.MPQ), built once and kept while the race is on, and into the ZIP for the other players. Every race gets its icon on the race choice when the race folder has one
- Custom races: the character creation of the Ascension client no longer crashes or paints wrong textures on added races: every skin, face, hair and underwear row is usable, a face and an underwear for every skin colour, women's rows for races that only have men, the rows of a race in one block, HD rows on the ids of their plain rows, empty textures no longer drawn as the Human skin (found by ilusixn for CoA Custom)
- Client patches: "importRace", "sectionRules" and "addFile" for modules that bring races
- Client patches: "copyRows" with "idGroup" and "idKey" gives copied rows the same new ids in tables that pair by id (the HD tables of the Ascension client: HDCharSections row n is the HD version of CharSections row n)
- Client patches: an edit or a "fill" with "forEach" stands for one per word of a setting, with "{each}" replaced by the word ("{conf:KEY}" in a fill line: the value of a setting); a setting can give a text ("text": true)
- Client patches: a file or edit can depend on server settings ("when"), a number of an edit can come from a setting, "fill" takes several markers and {words} and {text}, "copyRows" copies rows by field (with new ids), and "setRows"/"copyRows" can write texts
- A copied or moved server folder (a test copy of the server, say) builds again: the build folder still named the old place and CMake refused it; it now starts afresh there. A build that fails while it is prepared no longer blames a module that was just added

## 0.9.0-preview

- New: the Bots page (side bar, under Server). How many random bots there are and how many are in the world, the players in the world, the average level and Alliance : Horde; then the bots by faction, class (with the names the game shows for CoA's classes), race, level band and zone (the fifteen zones with the most bots, the rest together). It counts the bots in the world or all bots, reads the database in the background and refreshes every 30 seconds while the page is open

## 0.8.4-preview

- Client patches: "fillRows" fills the empty rows of a DBC table from a sibling row (CharStartOutfit.dbc: an outfit that only shows armour takes the items of the same class and sex of another race of the same faction)
- Client patches: "cloneRows" copies rows within a DBC table with some bytes changed and a new id (for tables with byte fields such as CharStartOutfit.dbc); "addRows" takes a "maxRows" limit and stops with an error instead of building a table the game cannot hold, and keeps only the first of rows a table holds twice
- mod-classic-classes: the character creation no longer crashes. The CoA client holds CharBaseInfo.dbc in a list of about 300 rows; the module now keeps it at 300 (the Death Knight and the Hero, who cannot be created next to the classic classes, make room) and gives the Orc Druid the start outfit the client lacks. The new pairs wore a showcase of high level armour in the character creation and started without gear; they now wear and get the start outfit of their class

## 0.8.3-preview

- Client patches: "dropRows" reads the fields of a DBC table in the width the table stores them in (CharBaseInfo.dbc of the CoA client has a byte per field); before, it read four bytes and failed on such a table. mod-classic-classes needs it: the character creation of the client crashed with all 32 classes for a race, so the module takes out the Death Knight's rows

## 0.8.2-preview

- Client patches: a module can add rows to a DBC table of the client ("addRows": every combination of the value lists in "cross", one list per field; rows the table has are skipped, the field width of the table is kept). mod-classic-classes uses it to let every race choose every classic class in the character creation (CharBaseInfo.dbc)

## 0.8.1-preview

- Update check: whether the CoA core, Playerbots, a module or AFK Realm has something new is asked of Git (git ls-remote), which GitHub does not limit. Before, the check used only the GitHub API, which answers 60 questions an hour without signing in; after many starts it stayed silent and no update was shown. The API now only adds how many changes there are; when it refuses, the banner says "new version" and from when GitHub counts again. All modules are checked, not only the first ten
- A newer AFK Realm found while GitHub refuses opens its release page instead of updating in place
- The update notes sit under the server buttons now, as wide as the window, and the bar grows with them; before, they stood right of the status and ran out of the window. The server note names the parts with news ("CoA core, Playerbots and 3 more"); the whole list with the number of changes is in its tooltip and in the question before the update
- An update check that answered before the window was ready was lost; it now waits for the window

## 0.8.0-preview

- New module under "Modules by AFK Realm": mod-classic-classes - the classic classes (Warrior to Druid, without the Death Knight) next to the classes of CoA, for players and the random bots, played the way CoA plays Ascension's Warcraft Reborn classes; with a setup page (Modules → Classic classes) for which classes can be created
- New: client patches. A module can change files of the game client in its afk-realm.json ("client": {"patch": ...}): files of its own, lines added to text files of the client, rows taken out of DBC tables. AFK Realm builds one archive, Data\patch-Y.MPQ, from the player's own game client (so it never hands out files of the game), builds it anew when the client, a module or a setting it reads has changed, takes it out with its module and puts it into the ZIP for other players with a note on the client version it fits
- New: a module can ask for new random bots ("bots": {"reset": true}); AFK Realm then offers once, after the module came, to reset the random bots
- "Game client add-ons" is now "Game client" and also lists the client patch; after a setting on a setup page changed, the window opens by itself when the client patch has to be built again
- The module check no longer takes Lua files under a module's client/ folder for Eluna scripts, and no longer warns about client files a module made for AFK Realm brings itself
- World Journey setup: the Extra hard text names only the values coa.conf holds; the manual describes CoA's creature multipliers without fixing their values

## 0.7.3-preview

- World Journey setup, Difficulty: an "Extra hard" switch next to the presets. It turns CoA's own creature multipliers on (CoA.CreatureScaling.Enable in coa.conf, since October 2026) on top of the chosen level - open world creatures 2.5x health and 2x damage against players and pets, dungeons 2.5x health and 1.5x damage, with the values read from coa.conf. World Journey switches them off by default; "Reset to defaults" does too
- Setup pages can write options of a second config file and save them together with the module's

## 0.7.2-preview

- World Journey setup: the switch "Every character plays the scaled world" now says what it does with the question at character creation (it still appears, but "off" no longer counts) instead of pointing to the Destiny Weaver

## 0.7.1-preview

- Fix: the module check said a module needs Eluna when it only brings an add-on for the game client (World Journey); Lua files next to an add-on's .toc no longer count as Eluna scripts

## 0.7.0-preview

- New look: the server management is now a window with a side bar (Server, Settings, Modules, Game master, Accounts, Database, Maintenance) instead of one long page; the state of the database, login and game world and the Start/Stop/Restart buttons stay at the top on every page
- New look everywhere: rounded buttons, switches, sliders and cards in AFK Realm's colours, every window with the same header, lists and tables with flat headers, the installation steps on a white sheet, flat progress bars
- New: setup pages for the modules by AFK Realm (Modules → World Journey / Auction house bots), shown only when the module is installed. Sliders, switches and choices instead of numbers in a list; they write the module's own .conf file, so editing the file by hand keeps working
- World Journey setup: where each part of the world begins (with a picture of the journey from 1 to 60), the endgame, every zone and dungeon with its level on the journey and the option to move one by hand, the level window with a preview, difficulty presets and multipliers by rank, also for one part of the world, and what follows the journey
- Auction house bots setup: who trades, trips and pace, what is sold and for how much, buying, crafting, deals by chat and chatter, and the Trading Post
- Fix: AFK Realm closed right after starting when a module had a new add-on for the game client waiting
- New module under "Modules by AFK Realm": mod-world-journey - the old world, Outland and Northrend as one journey from 1 to 60, for the classes of CoA
- New: game client add-ons (Server management → Game client add-ons). A module can bring an add-on for the game client in its afk-realm.json ("client"); AFK Realm asks once for the game folder, copies the add-on into Interface\AddOns, clears the client's cache when the module asks for it, updates the add-on when the module changes and takes it out when the module is removed
- After a module brought, changed or lost an add-on, the game client window opens by itself once; the management page says when the game is not up to date
- "Export as ZIP …" packs the add-ons for other players, with a note on where to unpack them and which folder to delete
- Nothing is copied while the game or its launcher runs from the chosen folder
- The module check shows when a module brings a client add-on
- Server settings: section titles keep "the" in lower case ("The Shape of the Journey")

## 0.6.6-preview

- New: scheduled restart (Server management → Scheduled restart). The server can be restarted on its own after it has run for a number of hours, every day at a set time, or when the worldserver uses more than a set amount of memory
- Players are told in the game beforehand (at the chosen number of minutes, then at five and at one minute); then the server is stopped cleanly and started again, as with "Restart server"
- The management page shows how long the worldserver has been running, how much memory it uses and when the next restart is due; an announced restart can be postponed by an hour
- Every scheduled restart is written to logs\scheduled-restart.log
- The restart is done by AFK Realm itself, so it only happens while AFK Realm is open; nothing is handed to the server that would shut it down with nobody there to start it again

## 0.6.5-preview

- New: database editor (Server management → Database editor): browse the server's databases and tables, filter rows with a WHERE condition, edit cells, add and delete rows (written only on "Save changes", in one transaction), and a SQL tab that runs any statements and shows the results
- Database editor: "Import SQL file …" runs a file after saying what it does (tables replaced, created, rows deleted or written); the database is only asked for when the file does not name it. "Export …" writes the ticked tables or whole databases (structure and rows) to one SQL file
- Database editor: "Undo last change …" takes back the last save in the Table tab (the old rows are restored), the last statements of the SQL tab or the last import (the tables they name are copied beforehand to Backups\\undo; newly created tables are removed); the last ten changes are kept
- Database editor: every change while the worldserver is running (save, SQL, import, undo) first warns and offers to stop the server; "Yes" stops it cleanly and then carries out the change
- Server management: new button "Restart server" (clean stop of world- and authserver, then start; the database keeps running)
- The editor is read-only until "Allow changes" is ticked; that shows a warning and offers to back up the whole server first ("Back up the server first …" does it at any time)
- Manual: new chapter "Database editor", including when a change shows in the game (restart or reload, client cache, characters that are online)

## 0.6.4-preview

- Game master tools: choosing a character shows its quest log, with how far each objective is ("Large Candle 3/8"); "Quest log" and "Refresh" read it fresh from the running server
- "Quests near the character" is now "New quests nearby" and lists only quests the character does not have in its log yet

## 0.6.3-preview

- Fix: "Reset random bots" reported "The worldserver started normally instead of deleting the bots. Nothing was deleted." on fast PCs although the bots had been deleted: the worldserver finished loading before it shut itself down

## 0.6.2-preview

- Module manager: new section "Modules by AFK Realm" at the top of the list, with modules made for this server
- First module of that kind: mod-playerbots-auctions – the bots use the auction house like players (sell, buy, bid, craft, gather)
- Modules can bring changes for the CoA core or Playerbots and settings for other config files (file afk-realm.json in the module). AFK Realm applies the patches to the fresh source with every build, leaves one out when it no longer fits or is no longer needed, and takes patches and settings back when the module is removed
- Fix: every update and every module change compiled the whole server again (20 to 30 minutes), because the build folder was thrown away each time; now only what changed is compiled
- Fix: in the server settings, "Save changes" stayed grey after typing a new value until the field was left; a change now counts while typing
- The update notice at the top of the server management now also tells when an installed module has new changes (before, only "Check for updates and install" looked at modules)

## 0.6.1-preview

- Game master tools: "Change name" lets the player choose a new character name at the next login

## 0.6.0-preview

- AFK Realm updates itself: a click on the "new version" banner downloads the release from GitHub, checks it, replaces the program and restarts it; a running server keeps running

## 0.5.0-preview

- Server name: a new installation asks for the name players see in the realm list; "Server name" in the server management renames the server later
- A database error without a message from MySQL no longer shows an empty error window

## 0.4.1-preview

- Fix: game master actions failed with "character does not exist" for characters with a first and last name (CoA allows a space in names)
- The character list in the game master window is wider, so long names are shown in full

## 0.4.0-preview

- Game master tools: connection to the running server (SOAP, this PC only, own administrator account)
- Quest helper: find quests by NPC name, quest title or id, or list the open quests around a character; give, complete, reward, remove and check them with one click, for offline characters too
- Player actions: unstuck, revive, kick, set level, send gold and mail, customize appearance, change race or change faction at the next login, server-wide announcement
- Server console in the window for every other GM command, with the server's answer
- Authserver and worldserver run without console windows; "Open server consoles" shows both in one window, with a command line for the worldserver (the classic windows can be switched back on)
- User guide (docs/MANUAL.md) that explains every window
- Fix: the build stopped at "Prepare the build (CMake)" with "the version field is not 4 integer components" on PCs with more than one Visual Studio 2022 installation

## 0.3.0-preview

- Module manager: the AzerothCore module catalog with search and filters; tick to install, untick to remove; a check of each module before installing (database changes, settings, core patches, client files, Eluna, age) and its README in the window; modules by Git address
- Module installs and removals back up the server and rebuild it; a module that does not compile is taken out again and the server stays unchanged
- Database changes of modules are recorded (schema `afk_modules`, part of every backup) and undone when the module is removed; rows changed later are kept and reported
- Restoring a backup also brings the module folders back to the state of the backup
- Playerbots versions listed in the catalog are shown as included, since AFK Realm always builds the CoA version

## 0.2.0-preview

- Automatic server backup before every update, manual backups, one-click rollback (newest 3 kept)
- Player account management: list real player accounts with their characters, delete accounts, set new passwords, change access levels
- Account transfer between servers via `.afkaccount` files (export and import with renumbered ids)
- Update notice for newer server code and newer AFK Realm releases
- Settings: shared descriptions show only the option's own default; 0/1 variants are shown as a choice
- Neutral wording instead of fixed time estimates
- Fix: the build stopped with "Git was NOT FOUND" when Git was installed but not on PATH

## 0.1.0-preview

- First public preview: guided installation, automatic build tools, newest CoA core and Playerbots, portable MySQL, CoA world data, databases prepared before the first start
- Map data and CoA client DBC extraction from the game client
- Server management: start/stop, accounts, play over VPN or LAN
- Settings editor for the worldserver, Playerbots and all module configs
- One-click random bot reset
- Update and repair functions
