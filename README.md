# TK2 Mod Toolkit

A Windows mod-control app and readable C# workshop for **The Karters 2 Turbo Charged 0.1.4.18**. One BepInEx plugin contains Toolkit Essentials and Community Mods, including custom recipes.

For players, use **TK2 Mod Toolkit.exe** from the portable distribution in `artifacts/portable`. Keep its accompanying folder intact. Python is included and installing the bundled plugin does not require a .NET SDK. The app discovers Steam libraries, accepts a game folder, checks the loader and its initialization, and includes the supplied BepInEx distribution. The source checkout also runs with **Launch Studio.cmd** or `python launch.py`.

Choose **Installation**: select the installation, prepare BepInEx if needed, start the game yourself once and wait for its menu, close it, then check again and install the plugin. A closed game is required to replace DLLs. Your existing settings are preserved. If Windows denies writes to Program Files, run the Toolkit executable as administrator for installation.

**26 configurable modules** are grouped into packs. Expand a module to see its options, defaults and allowed values. Advanced physics and boost parameters use individual override switches; their presets are mod defaults, not asserted current-game defaults. Reset module or pack stages its defaults; Save changes applies them. Saves merge only edited values and report conflicts only when that same value changed elsewhere.

The F8 in-race panel previews changes immediately and saves only changed config keys. F8 can be replaced by C or another listed key. The camera changes the game's racing-camera position/FOV together, with optional kart-size compensation, distance and height. Legacy ports include fast fall and dodge, custom laps, driving challenge, automatic boosts, respawn, boost training, alternate reserves, dash/items, portal tricks, mirror, reverse races, practice snapshots, names, nearby voice lines, CNK-style meter, all 57 advanced parameters, Nightmare AI and chat commands. New modules start off. Gameplay ports run locally/offline; **no Disable Leaderboards dependency or upload-blocking hook is installed**, following the requested test policy.

Expand a module and choose **Edit code** to open its actual C# files in **Workshop → Module editor**. The module selector filters navigation to its behavior, configuration and optional panel. Built-in modules share files in `plugins/TK2.Customization/`; custom recipes have individual files in `plugins/TK2.Customization/Recipes/`. Files live beside the application, not inside the game's plugins folder. Shared-file edits can affect multiple modules.

The editor has line numbers, text search, Tab indentation, Ctrl+S, source backups, **Open folder** for external editors, and **Reload file** to read external changes. Save C# writes source; **Build pack** checks compilation; **Build & install** saves your editor changes, compiles and replaces the one DLL with the game closed. Restart the game to load changed C#. A failed build does not install a DLL. Settings and toggles continue to use `local.tk2.customization.cfg` and reload live without building. Author builds need a .NET SDK and initialized game interop.

**Workshop → Game API reference** separates plain method-name navigation from read-only signatures. Camera, driving, boost/items, health, interface/audio and race/input categories show **844 methods in selected modding-related classes**; categories are navigation aids, not runtime compatibility guarantees. The full **32,350-declaration** catalog remains under All indexed functions. Scroll to load results automatically, with loading skeletons during requests. **12 reviewed reconstructed native methods** can open their editable implementations. This remains partial reconstruction, not the original C# project.

**Workshop → First mod tutorial** creates a configurable F9 kart-hop recipe and explains Configure/Tick/Restore, compilation, installation and live tuning. It suggests camera, audio, cooldown and frame-limit experiments. Read the [first-mod guide](docs/FIRST-MOD.md) for the workflow outside the app.

To build **TK2 Mod Toolkit.exe** yourself, double-click **Build Toolkit.cmd** in this checkout, or run:

```powershell
.\tools\build_toolkit.ps1
```

Install Python 3.10 or newer (64-bit) first. The script creates an isolated packaging environment, installs pinned PyInstaller, exports the supplied logo at ten Windows icon sizes, and packages the application. Its first build also compiles the plugin if no prebuilt artifact exists; that needs a .NET SDK and the game started once with BepInEx. Subsequent application-only builds reuse the prebuilt DLL. After editing plugin C#, run:

```powershell
.\tools\build_toolkit.ps1 -RebuildPlugin
```

Steam discovery finds the game by default. Optional paths work on other PCs:

```powershell
.\tools\build_toolkit.ps1 -RebuildPlugin -GamePath 'D:\SteamLibrary\steamapps\common\The Karters 2 Turbo Charged' -LoaderPath 'D:\Downloads\BepInEx-Unity.IL2CPP-win-x64'
```

Use the full loader distribution, or put it in `vendor/BepInEx`. Output: `artifacts/portable/TK2 Mod Toolkit 0.5.0/TK2 Mod Toolkit.exe` and `artifacts/portable/TK2-Mod-Toolkit-0.5.0-win-x64.zip`. Distribute the entire executable folder or ZIP. Close that version before rebuilding it. Repeated builds preserve previous output—including edited sources—in `local/portable-backups/`; packaging failures leave existing output intact. Packaging installs nothing into the game. Diagnostics are saved in `local/portable-build.log`.

The plugin is `BepInEx/plugins/TK2-Mod-Studio/TK2.Customization.dll`; settings are `BepInEx/config/local.tk2.customization.cfg`. Read [usage](docs/USAGE.md), [migration](docs/MOD-PACK.md), [native evidence](docs/REA-MIGRATION.md), [reconstructed source](docs/READABLE-SOURCE.md), [validation](docs/VALIDATION.md), and the [roadmap](docs/ROADMAP.md).

Compilation and offline tests are recorded separately from game observations. Current ports still need manual in-game testing; reverse races, full snapshot rewind and visual mirror corrections have explicit limitations. The app does not automatically start the game. Close the app window using its Windows X button. The runtime plugin keeps reading config independently of the Toolkit window.

See [application flow and live camera controls](docs/APPLICATION-FLOW.md). Toolkit 0.5.0 exports the supplied logo as a ten-frame TheKartersLogoModified.ico for the executable and window favicon. Its bundled runtime plugin is 0.4.1: the latest game log exposed an Auto boost hook that expected an obsolete `StopDrifting(bool)` signature. The hook now targets the current parameterless `StopDrifting()`. Choose Installation → Update plugin with the game closed to load that code fix on the next launch. The desktop UI changes themselves need no plugin update, and ordinary settings still reload live.
