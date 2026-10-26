# TK2 Mod Toolkit

**A Windows toolkit for installing, configuring and authoring mods for The Karters 2 Turbo Charged.** It combines a local desktop-style app, one BepInEx IL2CPP plugin, a C# workshop and a growing set of evidence-backed reverse-engineering notes.

<p align="center"><img src="assets/Logo.png" alt="The Karters 2 Turbo Charged" width="260"></p>

The 0.6.26 plugin is rebuilt against **0.1.4.30 beta_dev (Windows x64)**. The validated loader is **BepInEx Unity IL2CPP x64 `6.0.0-be.788`**, commit `5b766a3`. Compatible game updates no longer block installation solely because the native game hash changed; individual hooks still need to work, and live Mirror Race rendering on the updated game remains unverified. Module hooks are attempted on updated builds without binary-hash or version allowlists; only actual hook/runtime failures stop the affected feature. This is a community project; gameplay-changing modules are intended for local/offline use. Online protection is mandatory and locked on.

## Standalone camera and inputs

The toolkit also includes experimental **Air Dash**, **Rewind / Practice
Scrubbing**, and **Mine Traps** modules, plus expanded Practice Snapshots.
They default off and still need in-game validation. Ricochet Items was removed
after its bazooka shots failed in play. See [controls, evidence and
limitations](docs/AERIAL-PRACTICE.md).

Experimental **Antigravity** adds offline road adhesion for steep banks and
continuous loops, with bounded drift-hop continuity. It defaults off and keeps
mandatory online/submission protection. See [driving rules and native evidence](docs/ANTIGRAVITY.md).

[Bobby's Race Tools](standalone/README.md) separates Camera setup and the Gamepad
viewer into a small Windows executable that runs without BepInEx. Build it with
`python standalone/build.py`; the rest of this README describes the full Toolkit.

![Standalone camera controls](docs/images/race-tools-camera.png)

## Toolkit screenshots

The screenshots below show the current Mod library page and Workshop editor. The web UI adapts to the app window; controls and module catalog may change between toolkit builds.

![Mod library overview](docs/images/mod-packs.png)
![Workshop editor](docs/images/workshop.png)
![Workshop sharing](docs/images/workshop3.png)
![Installation checks](docs/images/installation.png)

## Install and use the application

For a ready-to-run copy, download the Windows x64 ZIP from [GitHub Releases](https://github.com/The-Karters-Community/TK2-Mod-Toolkit/releases), extract it to a writable folder and run **TK2 Mod Toolkit.exe**. Keep the extracted folder together. The app bundles Python and needs no Python or .NET installation to configure the prebuilt plugin. The game itself is not bundled.

**Updating from 0.6.24:** close the Toolkit and manually extract the new ZIP into your application folder, preserving `local/`. The new ZIP contains application files directly at its root; the updater shipped in 0.6.24 cannot read this layout. Later versions accept both flat and older wrapped archives.

In **Installation**, select the folder containing `TheKarters2.exe` and `GameAssembly.dll`. Install or repair the supported BepInEx build if needed, start the game yourself once so BepInEx can generate IL2CPP interop, then close the game and run **Check**. Install/update the Toolkit plugin only after the app reports the installation is ready. Existing plugin and config files are preserved; the app identifies possible conflicts and offers reversible disable/restore actions.

Open **Mod library**, expand a module, enable it, adjust settings and select **Save changes**. Ordinary settings reload from `BepInEx/config/local.tk2.customization.cfg` while the game is running. Changing C# source is different: close the game, use **Build pack** or **Build & install**, then restart the game to load the rebuilt DLL. The module catalog, search, filters, resets, advanced settings and Workshop are described in [Application usage](docs/USAGE.md).

### Troubleshooting and uninstalling

If plugin installation is blocked, first check that the selected folder contains the expected game binaries, that BepInEx is the supported `6.0.0-be.788` build, and that the game is closed. After installing or repairing BepInEx, launch the game once to generate interop and a startup log, close it, then select **Check again**. For load errors, open **Installation → Build output & diagnostics** or inspect `BepInEx/LogOutput.log`.

To remove the Toolkit plugin, close the game and use **Installation → Backups & restore** to restore the transaction created when the Toolkit DLL was installed. This restores the previous DLL, or removes the Toolkit DLL if there was no previous file. Keep or remove the Toolkit config according to whether you want to retain settings; removing the plugin does not require deleting BepInEx or other mods. The app refuses to restore a file that changed after the backup.

Release ZIPs and in-app updates include the compiled interface and application runtime. Players do **not** need Node.js, npm, Python or a build step. The commands below are for contributors.

## Run from a source checkout

On Windows, install **Python 3.10 or newer, 64-bit**, **Node.js 20.19 or newer with npm**, and clone the repository. Vite builds the frontend into static assets served by the authenticated local backend. Packaged builds use a pywebview desktop host with WebView2; source checkouts use it when `pywebview` is installed and otherwise open in Edge app mode.

```powershell
git clone https://github.com/The-Karters-Community/TK2-Mod-Toolkit.git
cd TK2-Mod-Toolkit
python --version
python -m pip install -r tools/requirements-app.txt  # optional native WebView2 desktop window
npm ci
npm run build
python launch.py
```

You can also double-click **Launch Studio.cmd**; it installs missing frontend dependencies and rebuilds the Vite frontend before opening. A failed build stops startup instead of serving stale assets. Install `tools/requirements-app.txt` to use the native WebView2 window; without it, the launcher opens Edge app mode. For frontend changes that appear immediately while editing, use the two-terminal Vite workflow in [Contributing](CONTRIBUTING.md). The [frontend ownership map](docs/ARCHITECTURE.md) explains where each view, control and stylesheet belongs. The app does not install or launch the game by itself.

To compile or install the plugin from source, also install a **.NET SDK that can target `net6.0`**, install the supported BepInEx build in the game, run the game once to generate interop assemblies, then close the game. Select the game in Installation and use **Build & install** in Workshop, or run `python -m studio.cli --game 'D:\Games\The Karters 2 Turbo Charged' build`. The plugin project requires game-specific interop references; build it through the Toolkit or this CLI rather than invoking `dotnet build` directly. A normal player using the prebuilt plugin does not need the .NET SDK.

## What is in this repository?

| Path | Purpose |
| --- | --- |
| `studio/` | Toolkit app: local HTTP backend, settings/catalog logic, release checker, and React interface in `studio/ui/`. |
| `plugins/TK2.Customization/` | The BepInEx plugin and its C# modules, hooks, online-safety policy, configuration handling and recipe host. |
| `src/Reconstructed/` | Small, readable C# semantic reconstructions of selected native game methods, adapters and provenance records. |
| `templates/` | Starter projects and examples for user-authored modules and recipes. |
| `tools/` | App packaging, release, Ghidra export, source audit and validation scripts. |
| `tests/` | Python backend tests, JavaScript UI-state tests, and focused C# logic/plugin-stub checks. |
| `assets/` | Toolkit branding and UI assets. |
| `docs/` | User guides, module notes, release/portable details, reverse-engineering evidence and known limitations. |
| `artifacts/` | Ignored build output: compiled plugin and portable app packages. Regenerate these locally; they are not checked into Git. |
| `local/`, `exports/` | Ignored machine-specific work: Ghidra projects, generated game data, captures, logs, backups and recovered third-party sources. Keep these out of commits. |

The **`.agents/context/PRODUCT.md`** describes the application scope and **`.agents/context/DESIGN.md`** captures design direction. See [the documentation index](#guides-and-reference) for the main guides.

## Build the plugin and portable app

Double-click **Build Toolkit.cmd**, or run from PowerShell:

```powershell
# Build/reuse the plugin and create the default portable app ZIP
.\tools\build_toolkit.ps1

# Recompile the plugin from current C# source before packaging
.\tools\build_toolkit.ps1 -RebuildPlugin

# Optional: include the BepInEx distribution in the package
.\tools\build_toolkit.ps1 -IncludeLoader

# Non-default Steam/library or loader locations
.\tools\build_toolkit.ps1 -GamePath 'D:\Games\The Karters 2 Turbo Charged' `
  -LoaderPath 'D:\Modding\BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788+5b766a3'
```

The build requires Python 3.10+ x64, Node.js 20.19+ with npm, a .NET SDK and an initialized supported game installation when the plugin must be compiled. Packaging dependencies are installed in the ignored `local/portable-build-env/` environment. The build does not deploy into the game or launch it. Build artifacts reuse the configured release version; they never increment it. The checkout displays the next unreleased version with `-dev`; successful release creation automatically advances and commits the next patch version. `tools/set_version.ps1` selects a different release number when needed.

The default artifact is a compressed, self-contained **application** ZIP at `artifacts/portable/TK2-Mod-Toolkit-<version>-win-x64.zip`; it bundles the app's Python runtime and prebuilt Toolkit plugin, but not the game or BepInEx. Users install the validated loader separately. `-IncludeLoader` creates the same package with BepInEx included. The portable build step also produces an unpacked app folder beside the ZIP.

## Tests and checks

From the repository root:

```powershell
python -m unittest discover -s tests -p 'test_*.py'
npm test
npm run format:check
npm run build
python tools/smoke_garage.py
```

The plugin's focused C# checks are separate projects under `tests/` (for example `dotnet run --project tests/ReadableLogic/ReadableLogic.csproj` and `dotnet run --project tests/OnlineProtection/OnlineProtection.csproj`). They compile against local stubs or extracted logic where documented. Use `python -m studio.cli build` with the initialized game's interop to validate a real plugin build. Passing automated checks proves only the behavior they cover; it does not replace in-game rendering, race, online-hook or clean-machine validation.

`python tools/smoke_garage.py` verifies built interface assets and the UI size budget. It does not launch the game. `python -m studio` opens the same application as `python launch.py`.

## Releases and in-app updates

The updater is already configured for **`The-Karters-Community/TK2-Mod-Toolkit`**. At startup, a packaged copy checks that repository's latest stable GitHub Release. Source checkouts only point to the release page. The current installer accepts a newer release only when it has the exact asset name `TK2-Mod-Toolkit-<version>-win-x64.zip`, the matching portable manifest and updater script, and a valid SHA-256 digest from GitHub. It preserves the user's `local/` data, updates standard versioned folder names to the new release, and relaunches from the new path. Custom unversioned folder names stay unchanged. It checks the restarted app before removing temporary rollback files; successful updates leave no permanent app backup. A draft or prerelease is not offered as a stable update.

**The updater currently supports one full portable ZIP; it does not apply compact/delta updates.** Every release intended for in-app updates needs that exact ZIP and its `v<version>` tag. `tools/publish_release.ps1` checks that the versioned archive exists, the worktree is clean and the exact commit is pushed to `origin/main`; by default it creates a draft. Review the draft and publish it on GitHub to make it visible to the stable updater:

```powershell
# Create a draft release (recommended first step)
.\tools\publish_release.ps1

# Publish immediately instead of creating a draft
.\tools\publish_release.ps1 -Publish
```

The release version is shared by `studio/version.py` and `plugins/TK2.Customization/ToolkitVersion.cs`; set both through `tools/set_version.ps1` while preparing a release. Build the archive after finalizing and committing the source. The script tags the current commit and uploads the ZIP. A release may use the default slim package or the larger loader-included package under the same required filename; the updater does not currently distinguish or offer two variants. See [release and update details](docs/RELEASING.md) and [portable packaging](docs/PORTABLE.md).

Release 0.6.26 adds the inactive-parameter allocation fix, aerial/practice modules and mine-trap corrections. See [release notes](docs/RELEASE-0.6.26.md). After publishing, source development advances to the next patch version; the release tag and portable ZIP retain their published version.

## Decompiled code and reverse-engineering evidence

There is **no complete, fully decompiled C# source tree** in this repository. The original game is a Unity IL2CPP build: generated DummyDlls provide browseable declarations with placeholder method bodies, while Ghidra analyzes native `GameAssembly.dll` code. Native pseudocode is not original C# or the original Unity project. The local workflow joins declarations, Ghidra function names/addresses and pseudocode so modders can investigate implementations and contribute reviewed C# reconstructions subsystem by subsystem.

The useful committed material is:

- [`src/Reconstructed/`](src/Reconstructed/) contains the selected readable C# behavior models, plugin-facing adapters and `provenance.json` linking work to binary hashes and addresses.
- [`docs/READABLE-SOURCE.md`](docs/READABLE-SOURCE.md) lists currently reconstructed methods, evidence and limitations.
- [`docs/RECONSTRUCTION.md`](docs/RECONSTRUCTION.md) explains the reconstruction workflow and incomplete areas.
- [`docs/IL2CPP-EXPORTS.md`](docs/IL2CPP-EXPORTS.md) explains the declaration, native pseudocode and reviewed C# layers, with commands to regenerate local exports.
- [`CONTRIBUTING.md`](CONTRIBUTING.md) separates runtime-code modding from the developer's Workshop content SDK and gives evidence, review and local setup expectations.
- [`tools/il2cpp_source_map.py`](tools/il2cpp_source_map.py) builds an ignored local mapping from DummyDll type declarations to candidate Ghidra functions and native addresses.
- [`tools/ghidra/targets.txt`](tools/ghidra/targets.txt) and [`tools/ghidra/migration-targets.txt`](tools/ghidra/migration-targets.txt) define reproducible function export targets; `tools/ghidra/ExportTk2.java` is the Ghidra exporter.
- [`docs/REA-MIGRATION.md`](docs/REA-MIGRATION.md) summarizes recent REA findings and migration decisions.

The Workshop **Game API reference** is a searchable method/signature catalog; a listed signature does not mean that its body has been recovered. The browser can also full-text search local Ghidra pseudocode exports when present. The bulk Ghidra project, native pseudocode exports, generated interop, raw dumps and third-party managed decompilations live under ignored `local/` or `exports/` paths. In the maintainer's current checkout, the native index and provenance are `local/ghidra/functions.jsonl` and `local/ghidra/summary.json`; selected and broad Assembly-CSharp Ghidra C-like pseudocode is in `local/ghidra/**/pseudocode/`. These game-derived files are not distributed in Git. The declaration export is for browsing and signature lookup, not game execution. Follow [the local export guide](docs/IL2CPP-EXPORTS.md) to generate matching evidence on your own installation; do not treat the function index as a full source release.

## Guides and reference

| Topic | Guide |
| --- | --- |
| App setup and normal use | [Application usage](docs/USAGE.md), [application flow](docs/APPLICATION-FLOW.md) |
| Create a C# recipe | [First mod](docs/FIRST-MOD.md) |
| Share/import modules | [Share modules](docs/SHARING-MODULES.md), [community packs](docs/MOD-PACK.md) |
| Portable package and release process | [Portable packaging](docs/PORTABLE.md), [releases](docs/RELEASING.md) |
| Community setup and modding routes | [Contributing](CONTRIBUTING.md) |
| Reverse engineering and source status | [Readable source](docs/READABLE-SOURCE.md), [local IL2CPP exports](docs/IL2CPP-EXPORTS.md), [reconstruction notes](docs/RECONSTRUCTION.md), [REA migration](docs/REA-MIGRATION.md) |
| Online safety | [Online protection policy](docs/ONLINE-PROTECTION.md) |
| Track-boundary inspector | [Track boundaries](docs/TRACK-BOUNDARIES.md) |
| Mirror camera | [Mirror race](docs/MIRROR-RACE.md) |
| Race FPS/RAM investigation | [Performance module](docs/PERFORMANCE-MOD.md), [investigation](docs/PERFORMANCE-INVESTIGATION-2026-10-09.md), [diagnostics](docs/PERFORMANCE-DIAGNOSTICS.md), [engine capture](docs/PERFORMANCE-ENGINE-CAPTURE.md) |
| Test history and current limitations | [Validation log](docs/VALIDATION.md), [roadmap](docs/ROADMAP.md) |

## Safety, scope and contributions

Online protection stays on and cannot be disabled in the app. It blocks leaderboard uploads and joining online rooms while unapproved mods are active. Only exact reviewed modules are allowlisted; physics, item, race-rule, information-advantage and user-authored code remain protected. The protection is a local client safeguard, not server-side anti-cheat or proof of publisher approval. Read [the policy](docs/ONLINE-PROTECTION.md) before changing the allowlist.

When adding a module, keep user settings and implementation in the existing catalog-driven structure, provide defaults/ranges and reset behavior, add focused regression coverage, and review its online-safety classification. Keep generated game data, decompiled third-party sources, captures and machine-local paths out of commits. Record binary version, hashes, evidence sources and limitations when adding native reconstruction. `AGENTS.md` contains repository-specific implementation and validation rules.

See [CONTRIBUTING.md](CONTRIBUTING.md) before opening a change. See [LICENSE](LICENSE) for the terms applying to this repository. Game files and any bundled third-party software keep their own licenses and are not relicensed by this project.

## Credits and disclosure

The Toolkit uses BepInEx Unity IL2CPP, Il2CppInterop and Harmony APIs; those projects and their licenses remain separate from this repository. The repository's [LICENSE](LICENSE) is All Rights Reserved and does not grant general permission to redistribute or modify the Toolkit. Game names, logos and game content are not owned by this project; the default portable build does not include the game or BepInEx distribution.

