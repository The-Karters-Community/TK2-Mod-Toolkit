# Using Mod Toolkit

## Player setup

Unzip the portable distribution into a writable folder and open `TK2 Mod Toolkit.exe`. Keep the folder together; it includes Python, UI assets, readable author source, the plugin and the loader distribution. Steam/game files are not included.

1. Game setup finds Steam's registered installation and all `libraryfolders.vdf` libraries. Select a detected folder or paste the folder containing TheKarters2.exe and GameAssembly.dll.
2. Prepare BepInEx copies the included Unity IL2CPP x64 distribution, preserving plugins/configs and backing up replaced loader files.
3. Start the game manually, wait for its menu, close it, then Check again. Readiness requires the configured Doorstop files, generated managed interop and a chainloader log marker.
4. Install/update plugin uses the bundled DLL, checks its integrity and the supported binary/loader hashes, and preserves current settings. This does not require a compiler. Restart the game to load a changed DLL.

The current test pack targets game 0.1.4.18, GameAssembly SHA-256 `e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130`. Other builds need author migration and validation; installing against an arbitrary changed binary is rejected.

## Configure packs

Choose a pack, expand a module and turn it on. Each setting shows its default and allowed values. Use Expand all or Collapse all on the separate row above the modules to open or close the displayed module panels, recipe sections and nested parameter groups. Use search to find a parameter; large tuning modules contain collapsible groups. An advanced value remains inactive until its Override switch is on. Reset module/pack changes the controls to defaults and switches affected modules off. Save changes applies your edits; reset does not secretly write a file.

Character names lets you pick a built-in racer as the matching name, then type any replacement text. Older saved custom match names remain available in the selector. When the installed plugin differs from the bundled version, the app header shows an Update plugin action; it uses the same backed-up install flow as the Installation page.

Settings reload on the game's main thread within about half a second. Content fingerprints detect edits even with identical file timestamps. Toggling, resetting or adjusting an existing module writes only the config, never compiles or replaces the DLL. Only edited keys are saved. BepInEx comment rewrites or unrelated external edits are merged; competing edits of the same key show specific conflicts. Reload conflicting settings keeps other unsaved changes. New or changed C# code needs Build, a plugin update and one game restart; normal config changes do not.

Camera setup contains FOV, Keep kart size, distance, height, side offset, aim, target height, pitch/yaw/roll and smoothing. Press F8 during a local race to open the in-game editor; choose C or another hotkey in the panel settings. Sliders preview immediately and save changed camera keys after a 350 ms idle delay. +/- adjusts precisely; Shift reduces the step. Reset in the panel restores geometry defaults while keeping its current enable state and accessible hotkey. Toolkit module resets still switch the whole module off. Keep kart size compensates distance when FOV changes. Native camera intro/spectator behavior stays intact. A very narrow FOV can still reduce visibility around the kart; the geometry change needs manual feel/testing.

Disable vignette is an optional visual module, off by default. Enable it to suppress the post-processing vignette while preserving the game's profile values; turning it off lets the game's current vignette settings apply again. The hook compiles against the installed game interop, but its live rendering behavior still needs an in-game check.

Online protection is enabled by default. It permits leaderboard submissions and online rooms when only the built-in allowlisted modules are active: HUD size and HUD transparency. Other enabled Toolkit modules, enabled recipes, and any other loaded BepInEx plugin block the selected actions. Turn the master switch or either action switch off only if you intend to use that feature with unapproved modifications. See [Online protection](ONLINE-PROTECTION.md) for the module-by-module review and hook coverage.

## Workshop

Search the complete game-method declaration catalog by type, method or parameter. Select a result to see its C# signature and recovery status. Open reconstructed C# when available; declarations without recovered bodies remain labeled as declarations. No fabricated body is supplied.

Choose a source file or create a recipe. The template demonstrates a configurable kart-hop shortcut using reconstructed AddVelocity; it compiles inside the same plugin. Edit in the built-in editor or an external editor. Save C#, then Build pack or Build & install. Author builds require the .NET SDK and the game's generated BepInEx interop references. The editor does not yet include IntelliSense or a native debugger.

Custom recipes implement IModRecipe. Their typed BepInEx settings appear after a manual game launch. Do not let two recipes own the same persistent setting without a restore/priority policy. Advanced speed/jump overrides take precedence over basic tuning; those modules take precedence over Nightmare AI's human speed multiplier. Alternate reserves takes precedence over custom drift-reserve lengths.

## Diagnostics and recovery

Game setup's checks distinguish loader readiness from the current plugin appearing in the latest log. Build output & diagnostics shows persistent compiler output and game log errors. Backups & restore is secondary; restore refuses files that were subsequently edited. Close the game before restoring DLL/loader files.

Gameplay controls apply to supported local/offline players. Online protection is a local client safeguard, not a server-side anti-cheat or proof that a mod is allowed by the game publisher. Start with one module at a time and record the current plugin version plus behavior; successful compilation does not verify native hooks or game transitions.

See [application flow](APPLICATION-FLOW.md). Use the window X to close Toolkit.
