# TK2 Mod Garage

A local mod-control app and readable C# workshop for **The Karters 2 Turbo Charged 0.1.4.18**. Uses the installed BepInEx IL2CPP loader and builds **one pack DLL**, including custom recipes.

Double-click **Launch Studio.cmd**, or run `python launch.py`. The modern interface opens in an Edge app window, with a default-browser fallback. It has the supplied logo/artwork, persistent light/dark modes, searchable player controls, a C# editor, build/install, diagnostics, and restorable backups. Python and a .NET SDK are needed; no additional Python packages are required on this machine.

The pack is installed at `BepInEx/plugins/TK2-Mod-Studio/TK2.Customization.dll`. The plugin directory contains exactly one DLL after this delivery. Its single configuration file is `BepInEx/config/local.tk2.customization.cfg`. All modules start disabled.

Available for manual runtime testing: HUD scale/opacity, audio multiplier, camera FOV, shadow distance, and a reversible frame-limiter recipe. Fresh legacy-inspired ports include custom laps, driving challenge, automatic drift boost, kart tuning, and fast fall. **Gameplay execution remains locked until runtime leaderboard protection is validated.** No module has yet been certified working in the running game.

The workshop opens editable reconstructed C# with actual behavior. Seven native methods now have compiled semantic reconstructions, including jump handling, camera selection, and velocity accumulation. This is partial source reconstruction; the original complete source and Unity project have not been recovered.

Read [usage](docs/USAGE.md), [readable source and provenance](docs/READABLE-SOURCE.md), [pack modules and migration](docs/MOD-PACK.md), [validation](docs/VALIDATION.md), and the [detailed roadmap](docs/ROADMAP.md). The original [investigation](docs/INVESTIGATION.md), [legacy audit](docs/LEGACY-AUDIT.md), and [mod possibilities](docs/CAPABILITIES.md) remain available.

```text
studio/web/                Modern player controls and workshop
studio/webapp.py           Loopback server and managed authoring/install operations
plugins/TK2.Customization/  One pack, isolated modules, recipe lifecycle
src/Reconstructed/         Editable C# behavior and native provenance
templates/PackRecipe.cs.txt New recipe added to the same pack
tools/ghidra/              Read-only Ghidra export tools
tests/                     Core/API/frontend state/native behavior tests
docs/                      Evidence, plan, usage, validation
local/, artifacts/         Ignored exports, recovered third-party code, backups and DLLs
```

The app does not start the game. Start it yourself for manual testing. Use Close Garage to stop the local service before closing its window. The old analysis interface remains available for advanced investigation; it is no longer the default player GUI.
