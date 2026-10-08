# Using Mod Garage

## Start and choose your setup

Double-click `Launch Studio.cmd`. It starts a service bound only to `127.0.0.1:8765` and opens an Edge app window, or your default browser if Edge is unavailable. A second launch reuses only an authenticated Garage service. Light/dark selection persists in the window's local storage; initial theme follows the system. Keyboard focus and reduced motion are supported. Use **Close Garage** to stop the service before closing the window. Unsaved edits prevent that action until saved.

The supplied game installation is the default. For another installation run `python -m studio.webapp --game "<game folder>"`. The app requires Python and the local .NET compiler for builds, but no third-party Python packages. The launcher displays an error dialog if startup fails.

**My mods** shows visual/audio controls and the locked offline lab. Filter or search, toggle a visual/audio module, set its values, and press **Save changes**. Each write has a restorable backup. Empty filters and out-of-range values are rejected. The plugin polls config changes on Unity's main thread roughly once per second.

The one pack is already installed in this workspace. **Update mod pack** rebuilds it against current local game/loader references, then installs the single DLL while the game is closed. No old community plugin or overlay dependency is copied back. The existing loader remains in use. New builds have `runtimeTested=false`; compile success does not mean a feature works in-game.

## One plugin and one config

Plugin: `BepInEx/plugins/TK2-Mod-Studio/TK2.Customization.dll`.

Config: `BepInEx/config/local.tk2.customization.cfg`.

Ten built-in modules and the FrameLimiter recipe live in that same assembly. Recipes you create also join it. All toggles initially false. Gameplay modules are hard-gated at runtime and cannot be unlocked merely by setting a cfg toggle. The GUI also rejects gameplay enable requests until leaderboard acceptance is completed. Their tuning values can be prepared now.

For first runtime testing, start the game yourself, inspect the BepInEx log for **TK2 Mod Garage Pack 0.2.0**, then test one visual/audio module at a time. HUD controls depend on actual canvas names; opacity requires an existing CanvasGroup. Audio capture may require moving an in-game volume slider once. FOV controls only the main perspective camera. Shadow support depends on the rendering pipeline. FrameLimiter temporarily disables VSync and restores prior VSync/frame-rate values when switched off. None of these behaviors has yet been verified in-game.

## Create a mod without another DLL

Open **Create a mod**. It initially displays `src/Reconstructed/KartLogic.cs`, with executable reconstructed jump/velocity behavior. Camera logic is beside it. Choose a pack file or create a recipe with a name such as `MyCameraFocus`.

The template generates `plugins/TK2.Customization/Recipes/<Name>.cs`, implementing Configure, Tick and Restore. It demonstrates a reversible camera change. Edit normal C#, press **Save C#** or Ctrl+S, then **Build pack**. **Build & install** saves any editor changes before building and deploying. Close the game first. External editors can edit these same files. If a file changed externally after opening, saving rejects the stale edit. Reload offers an explicit discard confirmation for unsaved editor contents.

Start the game manually once after adding a recipe so its Configure method creates typed config entries. Then recipe controls appear in My mods. Config changes reload live; source changes need a rebuild/restart. Mark gameplay recipes `ChangesGameplay = true`; those obey the same offline validation lock. Recipes are trusted C# code, not sandboxed scripts, and must be reviewed before distribution.

Keep modules that modify the same property from competing. For example, leave built-in Camera FOV off when using the generated camera recipe. The editor provides normal source editing and Tab indentation; completion, syntax highlighting, debugger, and compiler navigation remain planned.

## Backups and diagnostics

**Game & backups** shows the selected installation, active plugin DLL count, owned-file backups, build output and diagnostics. Install, config write and restore all preserve game files outside their targets. Restoration verifies installation identity and hashes; a later external edit prevents restoration from overwriting newer work. Restoring a newly installed file removes only that file. Source-edit backups stay under `local/source-backups`; game-file backups under `local/backups`.

The runtime log read by diagnostics may describe an earlier game run. A DLL's presence is not evidence it loaded successfully.

## Advanced analysis and command-line tools

The old analysis interface can be opened explicitly with `python -c "from studio.gui import Studio; Studio().mainloop()"`. It retains declaration browsing, local native pseudocode, exact-address Ghidra export, managed DLL recovery, and legacy configuration investigation. Its separate-plugin template is an advanced workflow; ordinary recipes use the unified pack.

```powershell
python -m studio.cli diagnose
python -m studio.cli index
python -m studio.cli build
python -m studio.cli deploy
python tools/audit_legacy.py
python tools/verify_readable_source.py
python -m unittest discover -s tests -v
node tests/garage-state.test.cjs
python tools/smoke_garage.py
dotnet run --project tests/ReadableLogic/ReadableLogic.csproj -c Release
```

The default launch is the new Garage. Browser visual inspection was blocked by declined browser permission during development; static wiring, API and offline frontend-state checks passed. Visible layout and in-game validation remain pending.
