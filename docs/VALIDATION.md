# Validation

## Automated evidence

The plugin compiles against this installation's current .NET 6, BepInEx and IL2CPP interop assemblies. Every build/deployment is fingerprinted. Python tests cover config merge conflicts, comments/unknown keys, recipe/source backups, deployment/rollback, Steam secondary library discovery, loader readiness and portable prebuilt installation into another path. JavaScript tests cover dirty-only saves, concurrent edits, focused inputs, module/pack grouping, advanced dependencies and default reset. Native pure logic tests cover jump branching, camera selection, FOV framing and immunity flags; community parser tests cover all six command families and permissions.

Static GUI smoke checks validate unique controls, wiring, JavaScript syntax, themes, assets and reduced motion. Browser visual verification remains pending because the previous browser access request was declined; no retry or alternate access method was used.

The portable executable is packaged with Python and the supplied BepInEx loader. A relocated-folder API smoke test checks that it starts independently of the source checkout, serves its assets, lists all packs/options and searches its exported function catalog. This is not yet an installation test on another person's computer.

## Recorded 0.3.0 local results

- Plugin build: 0 warnings, 0 errors.
- Python regression tests: 44 passed, including exclusive server-port ownership on Windows.
- Frontend state: 67 assertions passed.
- Readable behavior/camera/immunity: 71 assertions passed.
- Community commands: 62 assertions passed.
- Static GUI: 58 unique controls across three views, valid JavaScript, themes and assets.
- Relocated executable: auto-detected Steam installation, bundled loader located under the relocated directory, 26 modules/231 settings, 32,350 indexed declarations, 12 reconstructed functions, UTF-8 assets and editable source served. No browser or game launched. Evidence: `local/portable-smoke.json`.
- Deployment: current 0.3.0 DLL installed with backup, exactly one active DLL, all existing setting values preserved. Evidence: `local/deployment-0.3.0.json`.

## Game log reviewed

The latest available BepInEx log ended after chainloader initialization and the warning `Class::Init signatures have been exhausted, using a substitute!`. It did not contain a 0.3.0 plugin-load marker or plugin exception. The warning is surfaced in diagnostics; it does not prove the new plugin loaded or ran. The user's earlier camera result relates to the earlier raw FOV implementation.

No game was automatically launched. After installation, start it manually, reach the menu/race and then check Game setup again. The current-pack log check should pass; inspect runtime errors before enabling more features. The DLL remains one physical plugin. Existing Camera Enabled/FOV choices are preserved rather than silently reset.

## Manual acceptance

Test camera distance/height/FOV changes and toggles, menu/intro/spectator transitions, HUD filters/opacity, each of the five audio buses, configuration reload while racing, and frame/VSync restore. Then test each legacy module alone, pause/local/split-screen behavior, scene resets and offline-to-online transitions. Advanced overrides must restore their captured values; competing tuning modules use documented priority. Test reverse and whole-race rewind last because their current checkpoint histories are not fully reconstructed.

The requested test build installs no leaderboard blocker. Full original source recovery, broad native equivalence, every migrated runtime feature, another-machine setup and visible resizing are not certified by compilation.

## 0.4.0 Toolkit and live-camera update

Branding is TK2 Mod Toolkit; the supplied ICO is passed to PyInstaller and served as the app-window favicon. Community sources share one Community Mods pack; Close Garage and the mod-page install action are removed. Installation/build remain explicit separate actions.

Tests prove settings saves never call build/deploy/require-game-stopped and leave DLL bytes unchanged. The actual installed BepInEx ConfigFile is exercised with a temporary fixture: SHA-based hot reload despite equal timestamps, immediate panel preview, debounce, disjoint-key merge, precise same-key conflict, close flush, and no leftover transaction files. This does not execute the game. Camera geometry now includes bounded distance/height/lateral/rotation/aim plus frame-rate-independent smoothing. The panel is drawn by the injected behaviour's OnGUI and scopes visibility to observed local racing-camera output.

New in-game panel appearance, cursor restore, race/pause/intro/spectator transitions and native hook behavior need a manual game run. No game is launched by the tools. Version 0.4.0 is one code update; its initial DLL load requires one restart, subsequent settings changes do not. The prior Steam warning was investigated separately: local logs showed sync failures across apps and No Connection, with no original game/loader changes or save checksum mismatch.

0.4.0 checks: plugin build 0 warnings/0 errors; 45 Python tests; 72 frontend assertions; 76 readable/geometry assertions; 12 config-merge assertions; 10 actual-BepInEx hot-reload assertions; 56 static controls across three views. Pack catalogs expose 26 modules and 241 settings. Portable executable/resource and deployment evidence is recorded separately in local/portable-toolkit-0.4-smoke.json and local/deployment-0.4.0.json.

## 0.5.0 Workshop, icon and packaging

- 52 Python tests passed, including module source navigation, safe source-folder opening, failed-build non-deployment, API category filtering/pagination and transient Windows publication locks.
- 106 frontend assertions passed. New checks exercise Edit code/file filtering, editor search, reference/editor/tutorial separation, scroll pagination without duplicate requests, loading skeletons, search failure/retry and reconstructed-source opening.
- Static UI smoke: 82 unique controls, three main views, valid JavaScript and preserved theme/reduced-motion controls.
- The tutorial's MyKartHop recipe, including its changed 8f default, compiled together with the full pack against current interop: 0 warnings, 0 errors. This used a temporary source copy; no demonstration recipe was installed. Evidence: `local/workshop-0.5-validation.json`.
- The public `tools/build_toolkit.ps1` script produced the executable and ZIP. A repeated build preserved its previous entire output under `local/portable-backups/`. An initial Windows access error while publishing fresh files was resolved by bounded retry of the same rename operation; persistent failures retain the old package.
- Headless packaged executable checks passed: all 26 module-to-source mappings, 844 categorized methods, 32,350 full declarations, camera category/detail/source endpoints, tutorial assets and ZIP integrity. All ten embedded icon image resources exactly match the exported ICO frames and favicon. Evidence: `local/portable-toolkit-0.5-smoke.json`.
- The latest available log showed an Auto boost capability warning: the existing hook requested StopDrifting(bool), while both dump metadata and the actual current interop assembly declare parameterless StopDrifting(). Bundled plugin 0.4.1 corrects that signature. All three Auto boost targets were checked directly in the shipped interop PE metadata, without loading game code. Plugin build: 0 warnings, 0 errors. No runtime hook execution is claimed.
- The packaged DLL matches the new build artifact. The installed 0.4.0 DLL remains untouched; the user can update it through Installation. The game EXE, GameAssembly, UnityPlayer, installed DLL and config hashes were unchanged during tutorial validation. These tools performed no installation.

The visible browser layout and small-icon appearance still need user inspection; the earlier rejected browser permission was not bypassed. No game was launched, and no new runtime behavior is claimed tested by these checks. Another-machine installation remains a separate manual acceptance check.

## 0.6.1 resets, Race Lab, models and sharing (2026-10-09)

- Backend: **83 Python tests** passed. New coverage exercises literal recipe catalogs, compiled default preservation, selective packages, explicit conflict replacement/rollback, checksum/range/path validation, model dependencies checked before source writes, geometry bounds, texture dimensions and asset-only deployment into a fake game.
- Frontend: **122 offline state assertions** passed, including per-value reset, numeric slider synchronization, module header resets, Models/Share modules tab separation, module selection and staging imported presets without config writes. Static smoke checks **111 unique controls**, three main views, valid JavaScript and theme tokens.
- Runtime 0.5.0 compiled against the current installed BepInEx/IL2CPP interop: **0 warnings, 0 errors**. Seven Race Lab recipes and CosmeticModel are discovered by the existing recipe host. All gameplay recipes default off and retain local/offline guards; leaderboard upload behavior is unchanged.
- Durable managed harnesses: `tests/Mechanics/MechanicsTests.csproj` passed **29 transition/guard checks** on actual recipe files with Unity/game stubs. `tests/ModelParser/model-parser-tests.csproj` passed **23 parser/path/image-dimension checks**. These are simulations, not native runtime tests.
- Installed **Blender 3.5.1** performed actual headless FBX conversion of a generated textured sample: eight vertices, twelve triangles, OBJ/MTL and PNG texture output; geometry preview bounds and dependencies validated. The newer 4.4 folder has no executable, so discovery correctly selects 3.5. Creator-provided FBX files still need individual compatibility checks.
- Export/review/import roundtrip of all **35 modules / 323 settings** passed against actual Toolkit sources. All imported toggles were off; unchanged source was not rewritten. A single mechanic exports its helpers without unrelated custom recipes. Packages do not contain game binaries or loader/interop DLLs.
- The public build script produced **TK2 Mod Toolkit 0.6.1** and its ZIP. The open 0.6.0 app was preserved; Windows correctly refused overwriting its running executable, so this release uses a new output directory.
- Headless packaged-executable checks passed: all 35 source mappings, the compiled recipe catalog, 844 categorized APIs, 32,350 declarations, authenticated endpoints, all ten embedded icon resources matching the supplied ICO, and ZIP integrity. The installed game's plugin DLL stayed at its original SHA256 `ef3cc5b65201b573b731f4e927ae8c1aa983f08a8dd2c1ccbcbf0d2d9cc2bb08`; no plugin or model was deployed into the real game.

The visible layout, handling, camera/model appearance and Unity bundle compatibility remain manual acceptance checks. The earlier denied browser preview was not bypassed. The game was not launched automatically. FBX is a static cosmetic shell: animations, driver skeleton, collision and full PBR reconstruction are outside this importer.

Reproduce the checks:

```powershell
python -m unittest discover -s tests -p 'test_*.py'
node tests/garage-state.test.cjs
python tools/smoke_garage.py
```

## Settings cleanup (2026-10-09)

The Models importer and seven Race Lab recipes have been removed from active runtime source, UI, API, package support and tests. Removed config sections are preserved on disk but hidden in the Toolkit; importing retired modules is rejected. Portable builds now require an explicit user request, recorded in AGENTS.md.

The checkbox/hotkey regression was caused by missing input/change listeners after the slider refactor. A new test first reproduced the missing boolean handler. All controls now receive handlers, including dropdowns and numeric fields without sliders. Regression checks verify typed boolean/key values, unsaved edits surviving background refreshes, save-button activation and saved payloads.

Validation: **75 Python tests**, **133 offline frontend assertions**, static UI smoke (**96 controls**) and runtime **0.5.1 compilation with zero warnings/errors**. No portable executable/ZIP was built, no real game files were changed, and the game was not launched. Source changes are available through Launch Studio.cmd; old portable executables retain their embedded code until the user requests a new build.

## Previous native track tools (superseded 2026-10-09)

This entry records an earlier implementation. The camera mirror and custom collider overlay described below were replaced after the first offline validation pass.

The old mirror projection/culling/steering hooks are retired. Mirror Race reflects supported authored track geometry before the exact parameterless `Ant_MapData.Awake()` builds road/progress and minimap caches. Mesh channels/winding, collider centers, Bezier widths/points and minimap bounds are handled together. Unsupported geometry is rejected before scene edits; camera/UI hierarchies remain separate. This is a new implementation guided by native evidence, not recovered original mirror source. [Support limits](MIRROR-RACE.md) include static batching, animated geometry and GPU-only instancing; no stock track has been runtime-certified.

Track Inspector uses current native wall/respawn masks and initialized executor-to-trigger kill links. It follows the map's additive scene and local racing cameras, requires registered physics-trigger collider membership, and releases its own outlines on disable. [Coverage limits](TRACK-INSPECTOR.md) distinguish potential kill triggers, conditional respawns and approximate collider bounds.

The module catalog places Mirror Race in Community Mods and Track Inspector in Toolkit Essentials. Their helpers are editable/exportable, defaults remain off, and ordinary settings stay in the same hot-reloaded config. Old mirror settings remain preserved but hidden. Installation diagnostics now recognize the actual plugin version instead of hard-coded 0.4.1. Source-app version 0.6.3 prevents reuse of an older Toolkit backend.

Final checks: **81 Python tests**, **133 offline frontend assertions**, **3,021 mirror geometry assertions**, **26 inspector rules assertions**, static UI smoke (**96 controls**), and full runtime **0.6.0 compilation with zero warnings/errors**. Existing game logs still show installed 0.4.0. No game installation/config/save files were written, no game was launched, and no portable executable or distribution ZIP was built. Real rendering, collision, AI race completion and GPU recovery require manual in-game validation after installing the new DLL.

## Current native track fix (2026-10-09)

The current Mirror Race uses a per-camera clip-space horizontal flip and restores projection and culling state after rendering; it no longer rewrites track meshes. The matrix order was corrected for off-center camera projections. In-race rendering still needs manual verification.

Track Inspector now toggles the game's `Ant_MapData.bForceDebugShowTriggerCollisionMeshes` flag and existing trigger `MeshRenderer`s. It performs no per-frame collider scan, geometry generation, or custom drawing. Coverage is game-authored physics-trigger meshes, including trigger-based kill volumes; ordinary track walls are outside this debug flag.

Current native evidence: `Ant_MapData.InitMapDataFrom_ModTrack` copies the debug flag from the track asset, and both `PTK_ModTKLogic_PhysicsTrigger.Init` and `.Start` enable the trigger object's `MeshRenderer` when that flag is set. The current plugin compiles with **0 warnings and 0 errors**; **83 Python tests**, **133 frontend assertions**, and the **96-control static UI smoke check** pass. Plugin 0.6.2 is installed with the Toolkit's rollback backup, and its SHA-256 matches the build artifact. The previous log still records 0.6.1 because no new game run was started; camera rendering and inspector appearance await the next manual launch. No portable executable or distribution ZIP was built.

## Camera mirror and trigger setup correction (2026-10-09)

Mirror Race now suppresses the native local-camera steering/drift orbit only during its mirrored framing call, then restores the original flag. This targets the camera branch visible in current Ghidra pseudocode and avoids changing normal camera behavior when the recipe is off. Track Inspector sets the native debug flag before trigger `Init`/`Start` and retains its single list-based renderer update for mid-race toggles; it no longer waits until `E_RACE_RUNNING` to expose meshes.

Validation: plugin **0.6.3** compiles with **0 warnings and 0 errors**; **83 Python tests**, **133 frontend assertions**, and the **96-control static UI smoke check** pass. The new DLL is installed with rollback backup at `local/backups/1791503556440265400`; artifact and installed SHA-256 match (`435df8bd99ba16a4d2ef559d6478eef7ffe3d99e9eee790a130e92a4c5979e82`). The game was not launched automatically, so the final visual effect still needs manual offline verification. No portable executable or distribution ZIP was built.
