## 2026-10-18 - Input viewer and camera presentation (unreleased)

- Replaced the 440x196 input card, repeated headings, nested outlines and
  per-action colors with a 400x126 display, steering rail and consistent amber
  pressed states. Input sampling and replay behavior are unchanged.
- Camera panel now separates framing from advanced aim/rotation/smoothing.
  Numeric values align separately from labels; the body scrolls on small screens
  instead of reducing text size to fit. Reset and save status remain reachable.
- User testing exposed the old enable-state visibility guard closing the editor
  when overrides were disabled. The editor now stays open with overrides off;
  individual rows have resets. A headless harness executes the real panel through
  both toggle transitions, numeric/toggle/advanced resets, small-screen scrolling,
  Escape and scene exit (10 behavior assertions). It does not prove rendering.
- Read the current interop's Rewired player property directly for device detection
  instead of looking for a CLR field which is absent from that wrapper.
- Racing against a ghost keeps local driving inputs. Replay inputs are used only
  when the local kart is replay-controlled or there is no local kart. The game's
  `IsPlayerControlledByReplayInput(Ant_Player)` check selects the source; ghost
  pulse state is cleared across source changes. Input-model harness: 22 assertions,
  including opponent ghosts, replay viewing and disabled ghost inputs.
- Native evidence: installed GameAssembly SHA-256
  `55f5c88972fe1ccf4c9d5f3871b71bcfa261b3e42a379e18438e92623fc4d6a2`;
  freshly generated metadata locates that method at RVA `0x6C6D60`. Ghidra 12.1.4
  decompiled its extracted 0x120-byte body and confirmed player-type/playback-mode
  branches. External callees were not analyzed by this focused extraction. Native
  bytes, pseudocode and metadata stay in ignored `local/input-source-evidence`.
  REA's Windows adapter rejected raw x64 input, so the installed Ghidra headless
  importer was used directly. Production code calls the named API, not that RVA.
- Desktop settings for these two modules use flat field groups, compact numeric
  controls, accessible labels and a two-column layout that stacks on small
  screens. Other modules retain their existing setting layout.
- Standalone feasibility is recorded in STANDALONE-PRESENTATION.md. Existing
  native discovery can be reused, but per-frame input/camera hooks are a new
  implementation. No standalone executable has been built.
- Plugin compilation passed with zero warnings/errors; Python suite: 136 tests;
  React suite: 20 tests; input model: 22 assertions. Desktop settings were checked
  in the browser at normal width and 760px (no horizontal overflow). GPU captures
  confirmed the compact input HUD and corrected camera panel with per-value
  resets in the running game. User reported the original toggle-close issue;
  the corrected panel was installed with backup `1792347920261217100` and its
  installed hash verified. Ghost-source selection was built and regression
  tested afterward; its installation/runtime result is recorded below when done.

## 2026-10-15 — Garage cosmetic preview compatibility

- Approved exactly `community.tk2.garage.preview`, the separate Garage companion
  which renders detached cosmetic copies in MainMenuScene. No physics, item,
  profile mutation or lobby commands. User explicitly requested online use of
  cosmetic rendering; all other protection stays mandatory.
- Policy harness: 31 assertions, including unknown-plugin and physics-mod blocking
  even with the companion loaded. Plugin build: 0 warnings, 0 errors. Installed
  with normal backup receipt `local/backups/1792060271413232900` after game exit.
- User reopened the game; both plugins loaded. Garage captured correct modded,
  original and rotated images; save bytes and all PresetIcons hashes unchanged.
  Actual online-lobby rendering/join/upload behavior remains untested.
- No version bump, portable build or release.

# Mod log

## Mirror presentation correction and frontend ownership (2026-10-11, unreleased)

- Restored the camera/steering mirror during track countdown; no race-start delay. Countdown numbers use per-instance atlas UV compensation rather than delaying the entire mirror effect.
- Native `PTK_MarkersSingleAndCoopManager.CreateMarkerForOtherPlayer` at `0x18052bd50` passes the local camera as `transformForDistCalculate`; `Waypoint_Indicator.ChooseTargetTransforms` at `0x180a75f00` assigns this to `distCalTarget`. It is not the opponent tracking target. The previous projection therefore followed local camera motion. Installed interop also exposes waypoint members as CLR properties, so the earlier field-reflection path silently skipped correction.
- The revised hook restores the prior native marker placement before Update, then reflects the game's finished UI position within its own camera viewport. It retains native tracking, height offsets and off-screen clamping instead of re-projecting an assumed racer transform. Full native Waypoint Update decompilation timed out; that body has not been reconstructed.
- CounterBoardController Awake at `0x1808b7190` creates the number material instance; ChangeTileMatOffsets at `0x1808b73e0` selects the atlas tile; ScreenIdleUpdate at `0x1808b79d0` updates the same offsets. The compensation reflects X UVs inside that tile and restores native values before updates and on disable.
- Mirror regression harness: 66 assertions. Plugin compiled against installed references without warnings/errors. Actual number rendering and CPU nameplate attachment still require in-game visual confirmation; mocked placement tests do not establish that.
- Split the frontend into feature-owned ES modules, readable HTML fragments and responsibility-based stylesheets. Bootstrap is now small; backend source serving and Vite use matching template composition. Added explicit shared state, formatting checks, authenticated development proxy and contributor ownership/setup guides. Normal startup rebuilds the frontend and stops on build failure.
- Preserved the confirmed gamepad scaling fix. Toolkit stays at 0.6.23; no release or portable distribution is produced for these edits.
- Installed the rebuilt plugin after the game exited; backup transaction `local/backups/1791728883877367900`. Installed DLL matches the build SHA-256 `40bd4d587f93a75ba7846a76efe6fdb98d71468f89e0fb09534dd710b5718bab`. Browser checks passed for production Mod packs/Workshop and the authenticated Vite development interface/Installation. No game was launched by the agent.

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
