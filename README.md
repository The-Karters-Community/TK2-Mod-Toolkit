# TK2 Mod Garage

A Windows mod-control app and readable C# workshop for **The Karters 2 Turbo Charged 0.1.4.18**. One BepInEx plugin contains the Garage, MK and community packs, including custom recipes.

For players, use **TK2 Mod Garage.exe** from the portable distribution in `artifacts/portable`. Keep its accompanying folder intact. Python is included and installing the bundled plugin does not require a .NET SDK. The app discovers Steam libraries, accepts a game folder, checks the loader and its initialization, and includes the supplied BepInEx distribution. The source checkout also runs with **Launch Studio.cmd** or `python launch.py`.

Choose **Game setup**: select the installation, prepare BepInEx if needed, start the game yourself once and wait for its menu, close it, then check again and install the plugin. A closed game is required to replace DLLs. Your existing settings are preserved. If Windows denies writes to Program Files, run the Garage executable as administrator for installation.

**26 configurable modules** are grouped into packs. Expand a module to see its options, defaults and allowed values. Advanced physics and boost parameters use individual override switches; their presets are mod defaults, not asserted current-game defaults. Reset module or pack stages its defaults; Save changes applies them. Saves merge only edited values and report conflicts only when that same value changed elsewhere.

The camera changes the game's racing-camera position/FOV together, with optional kart-size compensation, distance and height. Legacy ports include fast fall and dodge, custom laps, driving challenge, automatic boosts, respawn, boost training, alternate reserves, dash/items, portal tricks, mirror, reverse races, practice snapshots, names, nearby voice lines, CNK-style meter, all 57 advanced parameters, Nightmare AI and chat commands. New modules start off. Gameplay ports run locally/offline; **no Disable Leaderboards dependency or upload-blocking hook is installed**, following the requested test policy.

Workshop edits ordinary C# and builds the same DLL. Author builds need a .NET SDK and initialized game interop. The function browser lists **32,350 game-method declarations** and distinguishes them from **12 reviewed reconstructed native methods**. Recovered implementations and new mod code have actual editable bodies. This remains partial reconstruction, not the complete original C# project.

The plugin is `BepInEx/plugins/TK2-Mod-Studio/TK2.Customization.dll`; settings are `BepInEx/config/local.tk2.customization.cfg`. Read [usage](docs/USAGE.md), [migration](docs/MOD-PACK.md), [native evidence](docs/REA-MIGRATION.md), [reconstructed source](docs/READABLE-SOURCE.md), [validation](docs/VALIDATION.md), and the [roadmap](docs/ROADMAP.md).

Compilation and offline tests are recorded separately from game observations. Current ports still need manual in-game testing; reverse races, full snapshot rewind and visual mirror corrections have explicit limitations. The app does not automatically start the game. Use Close Garage to stop its loopback service.
