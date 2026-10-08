# TK2 Mod Toolkit flow

## Install code once

Installation finds the Steam game folder and checks BepInEx files, generated interop and the chainloader log. Install plugin copies the built `TK2.Customization.dll` with a backup. The plugin ID and filename stay compatible with existing settings. Updating a DLL needs the game closed and one subsequent launch; settings do not.

## Configure while playing

1. Expand a module in Toolkit, enable it, adjust values, then Save changes.
2. Toolkit validates and merges only edited values into `BepInEx/config/local.tk2.customization.cfg`.
3. The already loaded plugin checks file content every 0.5 seconds on Unity's main thread and reloads its ConfigEntry values.
4. Existing hooks and update loops read those values. No compiler, new DLL, reinstallation or restart is involved.

External config editors use the same flow. Presentation overrides restore captured/native values when disabled. Some gameplay actions already performed, such as changing a lap outcome or killing a racer, cannot undo their past effects by switching a setting off. Online gameplay guards remain; this requested build has no leaderboard blocker.

## Tune the camera in a race

Press **F8** while the native local racing camera is active. PanelEnabled defaults on independently of Camera.Enabled, so the panel can open before camera overrides are enabled. Choose **C** or another key under Camera setup → In-race panel if F8 is inconvenient. `None` disables the keyboard shortcut. Close with the same key or the panel X; Escape also closes it.

Enable camera overrides inside the panel. Basic sliders cover FOV, distance, height and side offset; Keep kart size compensates FOV distance. Aim toward kart can keep the kart centered. Advanced sliders control target height, pitch, yaw, roll and adjustment smoothing. Zero smoothing applies immediately. +/- buttons give fine steps; Shift gives ten times finer steps. Panel scale lives in Toolkit and fits itself to the game screen.

Changes preview immediately in memory and persist after 350 ms without a new adjustment, or when the panel closes. Saving merges only changed camera keys with the latest file. A same-key competing Toolkit edit is reported and its current file value reloaded; unrelated settings survive. Toolkit polls the file to show changes made inside the race. Panel reset restores camera geometry while retaining the current enabled state, shortcut and panel scale.

Opening the panel exposes the mouse without pausing the race. Closing restores its captured cursor state when the panel still owns it. The panel hides outside observed local racing-camera output; intro and spectator cameras keep native behavior. Confirm these interactions manually in the game before relying on the new panel.

## Add or change C# behavior

Workshop creates/edits readable C# modules, then Build compiles the one plugin. Install/update replaces it with a backup while the game is closed. Restart once to load that code. After it is loaded, its ordinary options use the config-only path above.

In Toolkit 0.5.0, expand a module and choose **Edit code** to filter Workshop's Module editor to its actual files. Built-in code lives in `plugins/TK2.Customization`; recipes get their own `.cs` files in its `Recipes` subfolder. Save C# writes the selected source, Reload file reads external edits, and Open folder lets you use an external editor. The compiled DLL remains a separate result.

Game API reference and First mod tutorial have their own tabs. Method names are navigation; the signature pane is read-only reference. Only functions with reconstructed implementations can open recovered C# bodies. The tutorial creates a complete starter recipe and explains when to build versus save config.

Build the desktop executable from the checkout with **Build Toolkit.cmd**, or `tools/build_toolkit.ps1`. That script packages the application; `-RebuildPlugin` also compiles author changes. Neither action installs into the game. Install/update remains a deliberate action inside Game setup or Workshop.

Closing the Toolkit window with Windows X does not unload a running game's plugin. The plugin reads config independently of the desktop UI.

## Toolkit 0.6: recipes, model assets and sharing

The module catalog reads literal C# recipe bindings and presents them as configurable modules in packs. Numeric bindings require declared acceptable ranges; helper-based bindings need an explicit catalog adapter. The compiled artifact retains the defaults used by its last build, so changing an unbuilt source default does not silently change reset behavior for the installed code.

Per-value reset and header-level module reset stage changes. Save uses the existing semantic merge and config hot reload. This does not replace the DLL. Race Lab and CosmeticModel require updating the compiled plugin once (runtime 0.5.0) with the game closed.

FBX import uses a discovered or selected Blender executable to produce a validated static OBJ/material copy. Use on kart installs only assets under BepInEx/models and stages ModelPath/Enabled. Save applies selection and placement live. Importing or exporting .tk2mod files never launches the game or automatically builds/installs C#. Models from a shared pack first enter the local model library.
