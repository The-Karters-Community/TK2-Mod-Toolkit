# Mod log

## Scalable module library, fair-play allowlist and release updates 0.6.21 (2026-10-09)

- Generated module filters from catalog metadata and grouped cards by their existing category inside each pack. Added per-category counts and moved expand/collapse controls into a separate responsive toolbar row so controls remain usable in narrow windows.
- Expanded the explicit Online protection allowlist to HUD scale/opacity, vignette disable, audio mix, local camera setup, local visible character-name replacements and performance diagnostics. Shadow-distance changes, track boundaries, boost information, AI physics and recipes remain protected.
- Unified the desktop and portable Toolkit version. Portable startup checks the latest stable GitHub Release in the background; updates require a newer semantic version, the exact expected archive name and GitHub's SHA-256 asset digest. Extraction is bounded and validates paths, manifest and executable. The updater keeps user data and a rollback folder.
- Added a draft-first GitHub release helper. No release was published in this change; startup update detection will show an update after the first matching release is published.
- Validation: UI state tests, release parsing/archive safety tests, Python web-app tests and Online protection policy checks. Actual portable self-update and runtime in-game behavior require the first public release and a portable Windows test.

## IL2CPP-safe online protection hook and install diagnosis 0.6.20 (2026-10-09)

- Fresh game log showed BepInEx `6.0.0-be.788` initialized but rejected Toolkit 0.6.19 during `Plugin.Load`: the leaderboard Harmony target used original managed parameter types that IL2CPP interop transforms.
- Replaced exact CLR parameter matching with a unique static `void` method/name/five-parameter shape check. Reflection against this installation's `Assembly-CSharp.dll` confirmed one target whose parameters include `Il2CppStructArray<float>`, `Il2CppSystem.Collections.Generic.List<CCheckpointTimes>` and `Il2CppSystem.Action`.
- Setup readiness now requires the plugin's post-load marker and surfaces explicit plugin load failures; the chainloader's pre-load `Loading` message alone no longer counts as success.
- Current plugin build succeeded against installed references with zero warnings/errors. After the game closed, 0.6.20 was installed with backup transaction `local/backups/1791576377687095000`; installed SHA matches artifact (`d619d1d46307a5e5fc79b170c095751d7fb83980e2019b5fb4add143e4fa8c7c`). A subsequent game start logged the post-load marker and confirmed one leaderboard plus ten Photon room-operation hooks. Policy behavior is covered by tests; actual blocked online requests were not exercised.
- Mandatory online protection still blocks leaderboard and room operations when unapproved modules or plugins are active; only the explicitly reviewed HUD-only modules remain allowlisted.

## Mandatory online protection 0.6.19 (2026-10-09)

- Locked the Online protection module and both of its actions in the app UI; reset controls cannot turn it off.
- The game plugin always evaluates the unapproved-mod policy and ignores attempts to disable protection in config. Settings reads, config seeding and saves enforce true values; explicit disable requests are rejected.
- Only HUD size and HUD transparency remain allowlisted. Plugin build and policy checks pass; live online interception remains unverified in-game.

## 2026-10-09 — Track inspector 0.6.15

- Kept the existing offline-only native-mask collider discovery, nearest-first cap, camera culling, X-ray and pinned-object readout.
- Added default-off translucent fills with back-face culling, scene depth testing, no depth writes, and an opacity control (default 0.12; range 0.03–0.35). Wireframe outlines draw after surfaces.
- Added an F7 clickable in-race settings window for wall/respawn visibility, surfaces, opacity, X-ray, distance and outline cap. It temporarily unlocks the cursor, offers reset/save, auto-saves live edits through the existing conflict-aware config manager, and restores the prior cursor state on close/Escape.
- Kept the compact HUD and keyboard shortcuts; made the view cap and range adjustable without leaving the race.
- Changed only the mod's visual helper objects/materials and inspector UI; physics colliders, masks and native renderers remain untouched.
- Evidence: plugin compiled against current installed references with 0 warnings and 0 errors; artifact hash matches its build manifest. In-game GUI/overlay appearance and performance remain unverified.
- Deployment is pending while the game process is active; do not overwrite its loaded plugin DLL.

## Track inspector UX simplification 0.6.16 (2026-10-09)

- Removed X-ray and the F9 control. F10 now opens/closes the visual inspector and its settings together; F7 is no longer a separate options mode. The inspector starts closed and restores the previous cursor state on close.
- Clarified that orange marks colliders selected by the kart's native respawn masks. F8 only pins a displayed collider for inspection and does not affect classification or physics.
- Added a scope note for respawn areas absent from the view: separate generic trigger volumes are not automatically labeled respawn because the available native evidence does not establish which trigger types respawn the kart.
- Replaced default translucent IMGUI backgrounds and toggles in the inspector and camera panels with a solid dark panel and high-contrast custom checkbox states.
- Plugin version 0.6.16 built against current installed references with 0 warnings and 0 errors. `git diff --check` passed. The game is still running, so the new DLL was not installed; live UI behavior remains to be confirmed in an offline race.

## Toolkit interface and vignette 0.6.17 (2026-10-09)

- Removed the fixed 1450px application content cap and made the main view fill the app window. Wide settings panes use three columns; smaller windows retain the existing one/two-column breakpoints.
- Replaced the font glyph accordion indicator with a centered SVG chevron that rotates in place. Shifted the light/dark app palette toward the game's lavender, charcoal and orange interface colors, and display the Toolkit app version beside the supported game version.
- Added the default-off `DisableVignette` visual module. It suppresses `Vignette.IsEnabledAndSupported` while active and leaves profile intensities untouched, so the game's aspect-adaptive vignette settings resume when disabled.
- Native evidence: read-only Ghidra 12.1.4 analysis of the existing `GameAssembly.dll` program found `Ant_MapData$$RefreshAspectAdaptiveVignette` at `0x1805d3e80`, which finds `PostProcessVolume` instances, retrieves `Vignette` settings from their profiles, and caches intensity values. `UnityEngine.Rendering.PostProcessing.Vignette$$IsEnabledAndSupported` at `0x1830aae30` takes a `PostProcessRenderContext` and checks the effect's enabled state and mode parameters. The mod prefixes that exact method signature; source and interop build validation is separate from runtime verification.
- Plugin built against the installed interop references with 0 warnings and 0 errors. No live game was launched, so vignette rendering and UI appearance still need an in-game check before installing this DLL.

## Accordion visibility and bulk controls (2026-10-09)

- Expanded accordion chevrons now use a bright amber in dark mode for stronger contrast.
- Added Expand all and Collapse all controls for displayed module and recipe panels plus nested parameter sections. The state also stays consistent with individual accordion toggles.

## Responsive module controls and online protection 0.6.18 (2026-10-09)

- Reworked the module toolbar into a responsive filter/search grid and moved accordion actions onto their own labeled row so narrow windows no longer crowd or overlap controls.
- Added default-on global Online protection with independent upload and online-room switches. The allowlist is limited to HUD size and HUD transparency. All other active Toolkit modules/recipes and non-Toolkit BepInEx plugins are treated as unapproved.
- Read-only Ghidra evidence from the matching current `GameAssembly.dll` hash identifies leaderboard submission at `0x180529170` and Photon lobby/room API entrypoints at `0x182589580`, `0x18258d580`, `0x18258d660`, `0x18258d720`, `0x18258dc40`, `0x18258e180`, `0x18258e670`, `0x18258e6e0`, `0x18258e740`, and `0x182596180`. The plugin resolves and patches complete current interop `MethodInfo`s for the target method names and all overloads.
- Runtime lobby and leaderboard behavior remains unverified; install/test manually after the plugin and Toolkit update. The user has explicitly superseded the prior test-build policy against upload blocking.
