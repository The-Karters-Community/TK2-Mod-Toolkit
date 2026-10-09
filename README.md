# TK2 Mod Toolkit

New: [Track boundaries](docs/TRACK-BOUNDARIES.md) selectively displays invisible-wall and respawn collider geometry. [Engine performance capture](docs/PERFORMANCE-ENGINE-CAPTURE.md) extends the race/time-trial investigation.

<img src="assets/Logo.png" alt="The Karters 2" width="260">

A Windows mod manager and C# workshop for **The Karters 2 Turbo Charged 0.1.4.18**. One BepInEx plugin, live settings, editable modules.

## Features

- Modules grouped into Toolkit Essentials, Community Mods and Your Recipes.
- Per-value, module and pack resets; visible defaults, allowed ranges and interactive sliders.
- Live camera tuning with **F8**, plus interface, audio, graphics and driving controls.
- Camera-mirror racing for local offline races and time trials.
- Race performance: cached draw-distance arrays, optional race GC and native AI physics cadence for offline testing.
- C# editor, first-mod tutorial, game API browser and selective **.tk2mod** export/import.

## Get started

1. From this checkout, open **Launch Studio.cmd** (Python 3.10+, 64-bit). A shared distribution runs through **TK2 Mod Toolkit.exe**; keep its files together.
2. In **Installation**, select the game folder and prepare BepInEx if needed. Start the game yourself once, close it, then install/update the plugin.
3. In **Mod packs**, enable a module, adjust its settings and choose **Save changes**.

Settings reload live through `BepInEx/config/local.tk2.customization.cfg`. Changing C# requires **Build & install** with the game closed, followed by a game restart. Ordinary settings do not rebuild the plugin.

The source checkout runs with `Launch Studio.cmd` or `python launch.py` (Python 3.10+, 64-bit). Player installation from a portable package needs neither Python nor a .NET SDK; compiling custom C# needs a .NET SDK and initialized game interop.

## Build the Windows app

Double-click **Build Toolkit.cmd**, or run:

```powershell
.\tools\build_toolkit.ps1 -RebuildPlugin
```

Requires Python 3.10+ (64-bit), a .NET SDK and the initialized game. Steam discovery supplies the game path; optional `-GamePath` and `-LoaderPath` arguments support other installations. Omit `-RebuildPlugin` to reuse the existing compiled plugin.

Only build a portable distribution when needed. Output: `artifacts/portable/TK2 Mod Toolkit 0.6.3/` and its ZIP. Distribute the entire folder or ZIP. The build script never installs into the game.

## Guides

- [First mod](docs/FIRST-MOD.md) · [Application flow](docs/APPLICATION-FLOW.md)
- [Share modules](docs/SHARING-MODULES.md) · [Community ports](docs/MOD-PACK.md)
- [Mirror race](docs/MIRROR-RACE.md)
- [Race performance & developer handoff](docs/PERFORMANCE-MOD.md) · [Performance investigation](docs/PERFORMANCE-INVESTIGATION-2026-10-09.md)
- [Performance follow-up and diagnostic capture](docs/PERFORMANCE-DIAGNOSTICS.md)
- [Measured race versus time-trial results](docs/PERFORMANCE-CAPTURE-2026-10-09.md)
- [Readable source & limits](docs/READABLE-SOURCE.md) · [Validation](docs/VALIDATION.md)

Modules start disabled. Gameplay changes run in local/offline races. This test build does not require Disable Leaderboards or block uploads. Native reconstruction is partial, not the original Unity source project. Compilation and simulated checks pass; handling and visuals still need manual in-game testing.
