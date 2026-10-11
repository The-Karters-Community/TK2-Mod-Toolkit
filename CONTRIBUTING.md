# Contributing to the Toolkit

Start with the part you want to change. The desktop host, web interface and game plugin are separate: a frontend rebuild does not reload C# inside a running game.

| Change | Start here | Verify with |
| --- | --- | --- |
| Mod controls, filters and settings | `studio/web/features/mods/` | Frontend assertions and browser smoke check |
| Workshop editor, API reference, sharing | `studio/web/features/workshop/` | Frontend assertions and affected Workshop tab |
| Installation and update interface | `studio/web/features/installation/` | Python tests and Installation smoke check |
| Page markup and visual styling | `studio/web/views/`, `studio/web/styles/` | Vite build and narrow/wide window checks |
| Backend and desktop lifecycle | `studio/webapp.py`, `studio/core.py` | Python tests |
| Existing game module | Its C# file in `plugins/TK2.Customization/` | Relevant harness, plugin build, then in-game validation |
| New editable recipe | Workshop → Create recipe; `templates/` | Build & install with the game closed |
| Evidence or readable game behavior | `docs/`, `src/Reconstructed/` | Native evidence, interop signatures and documented limits |

## Local setup

Install Python 3.10+ x64, a Vite-compatible Node version (20.19+ or 22.12+; use a current LTS release), npm, and .NET SDK 6+ if changing C#. From the repository root:

```powershell
python -m pip install -r tools/requirements-app.txt
cd studio/web
npm ci
npm run build
cd ../..
python launch.py
```

`Launch Studio.cmd` also prepares missing frontend dependencies and builds before opening. The native window uses pywebview/WebView2; Edge app mode remains the fallback.

For immediate frontend feedback, use two terminals. In the first, from the repository root:

```powershell
python launch.py --no-browser
```

In the second:

```powershell
cd studio/web
npm run dev
```

Open the local URL printed by Vite. CSS edits update live; JavaScript and view-template edits reload the page. Save your pending settings or source edits before a reload. The Vite proxy reads the ignored backend session file and retains the backend's loopback authentication. Restart Vite if you restart the backend, because its session token changes.

For in-game settings, **Apply as you adjust** saves eligible settings for already-enabled modules after a short debounce. Enable/disable changes still need **Save changes**. C# code requires **Build & install** with the game closed, followed by a manual game restart; this is not hot code replacement.

## Frontend ownership

See [the frontend map](studio/web/README.md). Add behavior to the owning feature, not `app.js`. Keep markup in readable view fragments and shared state explicit. Formatting is checked with Prettier; avoid compressed source HTML or embedding whole pages in JavaScript strings.

## Plugin changes

Runtime code mods and Workshop content use different workflows. For runtime behavior, open **Workshop → Game API reference** and follow [IL2CPP exports](docs/IL2CPP-EXPORTS.md) if local pseudocode is missing. The declarations identify types and overloads; native pseudocode provides approximate implementation evidence. Neither is original recovered C#. Recipe integration is described in [First mod](docs/FIRST-MOD.md).

For tracks, characters, vehicles, wheels, stickers and supported audio assets, use the separate [official Workshop Unity SDK](https://github.com/PixelEdgeGames/TheKarters_WorkshopProjectRelease). Keep it and its submodules as a separate checkout and follow its own packaging/publication instructions. It does not replace the BepInEx runtime workflow.

Use the current game's generated BepInEx interop references. Build from the repository root:

```powershell
python -m studio.cli --game 'D:\Games\The Karters 2 Turbo Charged' build
```

Choose the same game directory in Installation, close the game and install the rebuilt plugin. Deployment creates a reversible backup. Validate behavior in the game yourself; compilation and mocked tests do not prove a native hook works. Record the native method/address, interop signature and remaining limits in `docs/MODLOG.md` or a focused evidence document. Keep game binaries, generated exports and third-party recovered code outside Git.

New gameplay-changing modules default off and need offline guards. Online protection stays mandatory; physics, item grants and arbitrary recipes are not allowlisted.

Native-hook contributions should include the supported game build, hashes of `GameAssembly.dll` and `global-metadata.dat`, the exact managed overload and native address, relevant callers/callees, and a clear separation between direct evidence and interpretation. Never invent original comments, variable names or Unity project structure. Generated declarations, Ghidra projects/exports, game binaries, interop assemblies, mod packages, captures, settings, credentials and recovered third-party code belong in ignored `local/` or `exports/`, outside commits and releases. Check staged files before committing.

## Checks and versions

```powershell
python -m unittest discover -s tests
node tests/garage-state.test.cjs
dotnet run --project tests/MirrorRace/MirrorRace.csproj -c Release
cd studio/web
npm run format:check
npm run build
```

Run the relevant checks for your change and include the result in your commit. Plugin changes also need a plugin build and a visible game test. Commit focused milestones. Normal edits/builds keep the current toolkit version and use the stable local plugin artifact folder; they do not create a new release. Only set the next version when preparing an explicitly requested release with `tools/set_version.ps1`. See [Releasing](docs/RELEASING.md) for full and compact release assets.
