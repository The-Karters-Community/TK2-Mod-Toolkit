# Portable Windows package

The portable package runs the Toolkit on Windows x64 without installing Python. It includes the app's Python runtime, web UI, Toolkit source used for Workshop editing, prebuilt plugin, catalogs and update helper. It does not include The Karters 2 or generated game interop assemblies.

## Player prerequisites

- The Karters 2 Turbo Charged **0.1.4.18**, Windows x64.
- BepInEx Unity IL2CPP x64 **6.0.0-be.788**, commit `5b766a3b7f6c164d4798924a93f3acf4db769d06` for the validated prebuilt plugin.
- A browser. Edge app mode is used when available, with a normal browser fallback.

The default ZIP does not bundle BepInEx. The Installation page can prepare the loader only when its distribution is available in `vendor/BepInEx`; `-IncludeLoader` adds it to the package. The larger package still preserves the user's existing plugins/configs when installed. See [README player setup](../README.md#install-and-use-the-application).

## Build

On the maintainer machine, install Python 3.10+ x64 and a .NET SDK. For plugin compilation, also install the supported loader, start the game once to generate interop assemblies, and close the game. Then run:

```powershell
# Reuse an existing compiled plugin if present
.\tools\build_toolkit.ps1

# Recompile plugin source before packaging
.\tools\build_toolkit.ps1 -RebuildPlugin

# Optional loader-included package
.\tools\build_toolkit.ps1 -IncludeLoader
```

The build script creates an isolated packaging environment at ignored `local/portable-build-env/`, unpacks a runnable folder under `artifacts/portable/TK2 Mod Toolkit <version>/`, and creates `artifacts/portable/TK2-Mod-Toolkit-<version>-win-x64.zip`. Build/packaging never deploys to the game. Close a running portable app before replacing that app folder. Build details and release checks are in [RELEASING.md](RELEASING.md).

## What the app validates

The Installation page detects Steam libraries and checks the game, loader fingerprint, Doorstop configuration, generated interop and BepInEx startup evidence. It backs up files before preparing or replacing them, preserves existing plugin/config files, and blocks DLL replacement while the game runs. Candidate plugin/config conflicts are not assumed to be definite conflicts: the player can disable one candidate reversibly and retest.

The bundled prebuilt plugin is fingerprinted against the supported game and loader. An unknown/older loader does not get treated as compatible merely because its version string looks close. Use the exact supported BepInEx build or rebuild and validate the plugin against a different installation before distributing it.

## Package contents and exclusions

The package includes the Toolkit UI and authoring sources, module templates, function-signature catalog, compiled Toolkit plugin, app runtime, `README.md`, `LICENSE`, version manifest, and a rollback-safe update script. The optional loader-included package adds the supported BepInEx distribution but omits user `plugins`, `config`, interop and cache folders.

Original game binaries, saves, generated interop, Ghidra project databases, raw game dumps, local logs, session tokens and recovered third-party plugin sources are excluded. Maintainer-local Ghidra data stays under ignored `local/` or `exports/`; see [readable-source limits](READABLE-SOURCE.md).

Unsigned distribution, third-party license notices, clean-machine setup/uninstall, and game/loader changes still need release acceptance. A local build or relocated-folder smoke check alone is not broad public compatibility certification.
