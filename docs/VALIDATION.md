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
