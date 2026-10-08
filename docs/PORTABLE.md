# Portable Windows distribution

The build is a self-contained Windows x64 folder with TK2 Mod Toolkit.exe. Python is bundled; player installation uses the compiled DLL and needs no .NET SDK. The app uses Edge app mode when available and a default-browser fallback. Author builds still need the .NET SDK. Unzip into a writable location and keep the folder together.

Build tooling is isolated in local/portable-build-env. Install Python 3.10+ x64, then double-click **Build Toolkit.cmd** or run:

```
.\tools\build_toolkit.ps1
.\tools\build_toolkit.ps1 -RebuildPlugin
```

Packaging includes the provided BepInEx IL2CPP x64 distribution, editable SDK source/templates, UI assets, compiled plugin and a function-signature catalog. Original game binaries, generated interop assemblies, local recovered third-party sources, credentials, logs and session tokens are excluded. Build/packaging output stays ignored in artifacts/. The bundled build receipt has local provenance paths, but player deployment compares relative binary/loader hashes so a different installation path works.

PyInstaller's one-folder/windowed settings follow [its official usage documentation](https://pyinstaller.org/en/stable/usage.html). This build is not yet tested on another PC; relocated executable and HTTP smoke checks are recorded in local/portable-smoke.json. The browser layout and game feature behavior still require manual acceptance.

Steam discovery checks registry paths and all libraryfolders.vdf entries, then the game's app manifest. Saved selection is project-local preferences, never a baked user-name path. No-game startup still opens setup. BepInEx readiness checks critical/bundled runtime files, enabled Doorstop target, generated managed interop and chainloader evidence. DLL replacement is blocked while the game runs. Missing/broken loader files can be prepared from the bundle, with backups and preserved user mods/configs.

Unsigned executable distribution, loader license notices, clean-PC installation/uninstallation and updated Steam branches need a release review before treating this as a broadly certified public release. That is separate from the completed local build.

The script installs pinned PyInstaller, discovers Steam, accepts optional `-GamePath` and `-LoaderPath`, and exports ten native icon sizes from the supplied PNG. A missing plugin artifact is built automatically; `-RebuildPlugin` includes later C# changes. An application-only build reuses the existing artifact. The packaging script never deploys to the game. See README for full examples.

Version 0.5.0 outputs `artifacts/portable/TK2 Mod Toolkit 0.5.0/TK2 Mod Toolkit.exe` and a versioned ZIP. A running executable cannot be replaced. Repeated builds retain the entire previous output in `local/portable-backups/` so edited sources and preferences can be recovered. Build failures preserve previous output, and detailed packaging output is written to `local/portable-build.log`. Copy portable source edits into the checkout before using its packaging script if you want them in the new distribution.

The executable and favicon share assets/TheKartersLogoModified.ico, with 16, 20, 24, 32, 40, 48, 64, 96, 128 and 256 pixel frames exported directly from the supplied PNG. The bundled runtime plugin is 0.4.1, with the current parameterless Auto boost drift-stop hook. Packaging does not deploy it; Installation → Update plugin installs it with the game closed. Config remains local.tk2.customization.cfg. Each cold launch uses a free loopback port; reopening a copy reuses its authenticated service.
