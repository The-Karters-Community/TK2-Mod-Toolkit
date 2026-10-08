# Portable Windows distribution

The build is a self-contained Windows x64 folder with TK2 Mod Garage.exe. Python is bundled; player installation uses the compiled DLL and needs no .NET SDK. The app uses Edge app mode when available and a default-browser fallback. Author builds still need the .NET SDK. Unzip into a writable location and keep the folder together.

Build tooling is isolated in local/portable-build-env. To reproduce:

```
python -m venv local/portable-build-env
local/portable-build-env/Scripts/python.exe -m pip install pyinstaller==6.22.3
python tools/build_portable.py --rebuild-plugin
```

Packaging includes the provided BepInEx IL2CPP x64 distribution, editable SDK source/templates, UI assets, compiled plugin and a function-signature catalog. Original game binaries, generated interop assemblies, local recovered third-party sources, credentials, logs and session tokens are excluded. Build/packaging output stays ignored in artifacts/. The bundled build receipt has local provenance paths, but player deployment compares relative binary/loader hashes so a different installation path works.

PyInstaller's one-folder/windowed settings follow [its official usage documentation](https://pyinstaller.org/en/stable/usage.html). This build is not yet tested on another PC; relocated executable and HTTP smoke checks are recorded in local/portable-smoke.json. The browser layout and game feature behavior still require manual acceptance.

Steam discovery checks registry paths and all libraryfolders.vdf entries, then the game's app manifest. Saved selection is project-local preferences, never a baked user-name path. No-game startup still opens setup. BepInEx readiness checks critical/bundled runtime files, enabled Doorstop target, generated managed interop and chainloader evidence. DLL replacement is blocked while the game runs. Missing/broken loader files can be prepared from the bundle, with backups and preserved user mods/configs.

Unsigned executable distribution, loader license notices, clean-PC installation/uninstallation and updated Steam branches need a release review before treating this as a broadly certified public release. That is separate from the completed local build.
