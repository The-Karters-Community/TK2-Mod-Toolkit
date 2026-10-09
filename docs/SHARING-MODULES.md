# Share selected modules

## Export

Open **Workshop → Share modules**, name the package, tick the modules you want and choose **Export selected modules**. The downloaded `.tk2mod` ZIP includes:

- Current values for those modules, including unsaved settings.
- Their readable C# files and required local helpers.
- A versioned manifest with module identities, bounds, dependencies and file checksums.

Save unsaved C# before exporting. The package is for another **TK2 Mod Toolkit 0.6.3+**, using the same supported game build. It is not a standalone DLL; the recipient's Toolkit supplies BepInEx, runtime host and compilation.

Packages containing **Race performance** require runtime **0.6.9+** and a Studio catalog that includes Performance (source-app 0.6.4+). The exported helper relies on the recipient's Plugin and StudioBehaviour integration; older installations cannot import this new built-in module.

**Performance diagnostics** requires runtime **0.6.10+** and source-app **0.6.5+**. Its exported helpers include the PerformanceFeature settings snapshot dependency; Plugin.Install and StudioBehaviour.Tick integration is required. Disable diagnostics before an uninstrumented performance benchmark.

Built-in modules share implementation files. Their package may contain code for other built-in modules, but only selected config sections are exported. Game binaries, interop DLLs, the loader and unrelated custom recipes are excluded.

## Import

1. Choose a `.tk2mod` file and click **Review package**.
2. Inspect the module and file list. Existing files with different content are marked as conflicts. Replacing changed files requires the explicit checkbox; originals are backed up under `local/source-backups`.
3. Choose **Import reviewed package**. Settings are staged for review and every imported module starts off. Unrelated settings are preserved.
4. Review source in **Module editor**, then **Build & install** with the game closed if code changed. Restart the game to load it.
5. Enable the modules you want and **Save changes**. Later settings edits reload live.


Packages contain executable C# source. Import does not execute or compile it. Review supplied code before choosing Build & install. Path traversal, links, unexpected file types, oversized expansions and checksum mismatches are rejected before source replacement.

## Where the files live

- Authored code: `plugins/TK2.Customization/Recipes/` beside the Toolkit.
- Runtime: one `BepInEx/plugins/TK2-Mod-Studio/TK2.Customization.dll` in the game.
- Config: `BepInEx/config/local.tk2.customization.cfg`.
- Export copies: `local/module-exports/`.

Native game code is not recovered or included by the export workflow. Shared SDK source changes can affect other modules in the same compiled pack; use the preview and backups when reviewing changes.
