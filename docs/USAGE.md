# Using TK2 Mod Studio

## Start the GUI

Double-click `Launch Studio.cmd`, or run `python launch.py` from this repository. This machine already has Python 3.14 with tkinter and .NET SDK 9.0.203. No Python packages or downloaded plugin NuGet packages are needed. `pythonw` avoids a persistent console window. If launch fails, run `python launch.py` in a terminal to see the error.

The selected game folder defaults to the existing Steam installation. The GUI does not launch the game or replace the installed loader. Inspection reads the latest existing log, which may describe an earlier run. Unknown DLLs are displayed as files, not inferred to be healthy plugins.

## Inspect readable code

1. Open **Code browser** and choose **Game declarations**. Search e.g. `PixelKartPhysics`, `HpBarController`, `PTK_AudioListenerManager`, or `PTK_ModGameplayDataSync`. These declarations describe available fields/signatures but contain stub bodies.
2. Choose **Native function index** to search all exported native functions. Select one to see its address, signature, and any exported pseudocode.
3. **Export selected method** asks for the installed Ghidra home and `.gpr` project and exports the selected name **and address**, so two overloads sharing a name can be inspected separately. **Export Ghidra** exports the standard target set from `tools/ghidra/targets.txt`. Both use read-only processing with analysis disabled.
4. **Native pseudocode** shows the existing local `.c` exports. These are approximate reconstructed native logic. An export with zero errors does not mean all inferred types or indirect-call arguments are correct.
5. **Recovered mod C#** shows managed decompilation from MKsKartersMods. **Recover mod C#** can recover another managed DLL using the installed dnSpy console. This does not work on native `GameAssembly.dll`.

Local analysis output is already generated on this machine. To regenerate declarations, use **Index dump.cs** after rerunning Il2CppDumper for a new game build. To regenerate all native function names, use **Export Ghidra** after opening a correctly updated Ghidra project. The Java script reads the existing database; it does not rerun the user's import pipeline or repair missing symbols automatically.

## Create and edit a mod

**C# editor → New mod** creates a readable project in `local/projects/<name>`. The template hooks the exact `Ant_MainGame.Start()` signature and logs race initialization when its `General.Enabled` setting is true. It defaults off. It is intentionally a simple learning example.

Choose a `.cs` file, edit it, **Save C#**, then **Build**. The editor starts on `Plugin.cs`. External editors can edit the same files; reopen the source in Studio before editing if another editor has changed it. The initial editor provides normal source editing and undo, without IntelliSense or a debugger.

**Open project** supports the generated project format and the provided starter. Existing community projects may need csproj/dependency migration; the GUI does not automatically rewrite arbitrary project files. Generated projects import local `References.props`, set `Private=false` on runtime/game dependencies, and compile as .NET 6 class libraries. The machine's .NET 9 SDK is the compiler host; the produced plugin targets the game's .NET 6 API surface. Supply hidden nullable metadata attributes because implementation DLLs are used in place of a downloaded reference pack. A conventional .NET 6 targeting pack is a future packaging option.

Successful builds stage a DLL and `build.json` under `artifacts/<project>`. The receipt records game and reference hashes, artifact hash and `runtimeTested=false`. Game dependencies are never copied into the plugin package. Compile diagnostics appear in **Logs & roadmap**.

## Deploy and configure the starter

The customization DLL is built at `artifacts/TK2.Customization/TK2.Customization.dll` and has **not been installed automatically**. It is ready to deploy through the GUI:

1. Close the game and select the starter project in **C# editor**.
2. Click **Deploy build**. The target is `BepInEx/plugins/TK2-Mod-Studio/TK2.Customization.dll`. Hash checks reject an updated game or modified build input. An existing target is backed up first.
3. Start the game yourself. BepInEx generates `BepInEx/config/local.tk2.customization.cfg`. All feature toggles default false.
4. In **Customization**, click **Load from game**, change the desired settings, and **Save to game config**. The plugin polls/reloads settings on Unity's main thread roughly once per second. The tab can also create the initial config before first run.
5. Read the log and verify each feature individually. The starter is compiled but has not yet been verified in the running game.

### Starter behavior and practical limits

- **UI:** multiply the captured scale of matching root, non-world-space canvases. The default name filter is `HUD`; real canvas names must be checked in-game. A competing CanvasScaler can overwrite the value. Future work is a game-specific HUD adapter and hierarchy inspector.
- **Audio:** intercept the exact `PTK_AudioListenerManager.SetVolume(string,int)` method and multiply the game's requested Wwise volume levels. The plugin restores captured levels on disable. If no calls were captured after startup, change an in-game audio slider once. It does not control every Wwise bus/event independently.
- **Camera:** override `Camera.main` perspective FOV in LateUpdate; restore captured FOV on disable or camera change. This first adapter does not select every split-screen or VR camera and may conflict with other camera modifiers.
- **Physics:** hold Down Arrow after minimum air time to add downward velocity to a local human kart during an offline running race. This is an experiment ported from the old mod's approach. It requires the known GameAssembly hash and successful exact-target installation. Failure disables the feature. The known leaderboard upload path stays blocked after any physics modification until game restart. This is not a claim that all possible upload paths are blocked. Input mapping, complete offline transition testing and leaderboard coverage remain runtime acceptance work.

## Manage old mods and configs

**Mods & backups → Enable / disable selected** renames `.dll` to `.dll.disabled` and back. BepInEx ignores the disabled suffix. Changes require game restart. Some DLLs are dependencies (GameOverlay, SharpDX, etc.), so disable those only with the dependent plugin. This first version does not resolve arbitrary community dependency graphs.

**Configs → Setting controls** reads the existing BepInEx setting descriptions/types/ranges/choices and displays each setting as a row. Select a row, choose/edit its value, **Apply to editor**, then **Save config**. Your current MKsKartersMods config supplies 90 controls, including 15 mod enable toggles. Booleans, finite numeric values, integer limits and declared acceptable ranges/choices are validated. Unrecognized custom setting types remain textual and require the mod's own validation.

**Configs → Raw config** also allows direct editing. Both views preserve existing content/comments and back up the file on save. Saving rejects a file that changed since it was opened. Live application depends on each old mod's own reload support. When changing raw comments/types, use **Refresh fields** to regenerate setting controls. The starter's separate customization tab validates its own supported settings.

**Restore selected backup** checks installation identity and file hashes, then restores only the file Studio wrote. It rejects a newer external edit instead of overwriting it. Restoring a newly deployed DLL removes only that DLL. Backups can contain private config values and stay in ignored `local/backups`.

## Command-line equivalents

```powershell
python -m studio.cli diagnose
python -m studio.cli index
python -m studio.cli build
python -m studio.cli build --project local\projects\MyMod\MyMod.csproj
python -m studio.cli deploy
python tools\audit_legacy.py
python -m unittest discover -s tests -v
python tools\smoke_gui.py
```

Supply `--game "<installation>"` **before** the subcommand to select another installation. The hidden/off-screen smoke check tests widget layout and local data loading; it is not a user interaction or in-game test.
