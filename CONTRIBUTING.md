# Contributing to TK2 Mod Toolkit

This repository supports two different kinds of modding work. Keep them separate so contributors can pick the right tools and reviewers can tell what each result proves.

## Start with the right workflow

### Runtime code mods and reverse engineering

Run `Launch Studio.cmd`, open **Workshop → Game API reference**, and search the declarations or local Ghidra pseudocode. The first pseudocode search builds a local full-text index; later searches reuse it. If the pseudocode pane says no exports were found, follow [IL2CPP exports](docs/IL2CPP-EXPORTS.md) with the matching game build and Ghidra project. Use the declaration browser to find exact types and overloads, then use native evidence to understand behavior. Declarations and Ghidra output are not executable game classes; add readable behavior only as a provenance-linked reconstruction in `src/Reconstructed/` or as an evidence-backed adapter for the plugin.

Toolkit runtime mods use BepInEx Unity IL2CPP, Il2CppInterop, and Harmony. The plugin source is in `plugins/TK2.Customization/`; recipes and module catalog integration are documented in [First mod](docs/FIRST-MOD.md). Keep gameplay changes disabled by default, offline-guarded, and classified under the locked online-protection policy.

### Workshop content mods

For tracks, characters, vehicles, wheels, stickers, and supported audio content, use the developer's separate [official Workshop Unity SDK](https://github.com/PixelEdgeGames/TheKarters_WorkshopProjectRelease) and its wiki. The supplied checkout records Unity **2020.3.47f1** and `ModdingToolsSDK_Version.txt` **v1.08.1 (21.09.2024)**; its README labels the SDK release **1.0 BETA**. Its editor tools build Addressables content and a game-loadable mod package. This is an asset/content pipeline; it does not expose the game's complete runtime C# source or replace the Toolkit's BepInEx workflow. Keep that developer project and its submodules as a separate checkout, and follow its own rules for packaging and Steam Workshop publication.

## Evidence and review

For reverse-engineering or native-hook changes, include:

- supported game build and SHA-256 hashes for `GameAssembly.dll` and `global-metadata.dat`;
- exact managed type and overload, metadata token/native address, and aliases where present;
- links to the local Ghidra function and any relevant callers/callees or strings;
- which observations are direct native evidence and which are interpretation or assumptions;
- focused tests for pure reconstructed logic, plus runtime validation notes for Unity/BepInEx behavior.

Never label a DummyDll body, an Il2CppInterop wrapper, or Ghidra pseudocode as original C#. Decompiled native output is evidence to interpret, not a complete reconstruction. Do not invent unavailable original comments, local variable names, or Unity project structure.

## Keep generated and private data out of Git

Generated declarations, Ghidra projects and exports, game binaries, `global-metadata.dat`, BepInEx interop, mod packages, local captures, settings, credentials, and third-party managed decompilations belong under ignored `local/` or `exports/` paths. Do not add those files to a commit or release. Commit scripts, schemas, documentation, tests, and reviewed original Toolkit source instead. Check `git status` and `git diff --cached --stat` before committing.

## Local setup and checks

On Windows, install Python 3.10+ and run `Launch Studio.cmd`. To author runtime C# modules, also install the .NET SDK and initialize the supported BepInEx IL2CPP interop for the exact game build. See [README setup](README.md#run-from-a-source-checkout).

Before submitting a change, run the relevant focused test and then the repository checks:

```powershell
python -m unittest discover -s tests -p 'test_*.py'
node tests/garage-state.test.cjs
```

Do not launch the game automatically during tests. Record whether an in-game behavior was actually validated; compilation and decompilation output alone do not prove runtime behavior.
