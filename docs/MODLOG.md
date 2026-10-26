## 2026-10-26 - Explicit ground/wall surface mode

- User isolated the native crash to surface-camera following. Disconnected that
  call path and removed its Studio control; camera following remains unresolved.
- Added opt-in offline **Drive on ground and walls**. Follow native solid
  contacts, including wall collision layers, without the road-history gate.
  Restore stable-ground masks on release; retain KCC collision movement and
  exclusions for respawn layers, triggers, rigidbody objects and racers.
- Per user instruction, compiled for an in-game test without writing or running
  more harness tests. This candidate is not gameplay-validated.

## 2026-10-26 - Preserve Antigravity contact history

- User's follow-up recording exposed a frozen 1/4-sample arm history despite
  valid preview hits and sufficient speed. Fixed the lifecycle check that
  compared a kart's owning scene to the active scene. Track both identities
  independently so persistent/additive-scene karts retain history across render
  updates, while scene changes still release state.
- Anticipate a validated 36-to-55-degree facet transition instead of waiting for
  a nonexistent 47-degree intermediate face. Preserve geometry, layer, speed,
  history and abrupt-corner guards. Added precise 5 Hz arm-block diagnostics.
- Read-only native samples confirmed sufficient speed and upright motor state;
  the native upward cap was 60 and is not established as this failure's cause.
- New live gameplay validation remains required. See `ANTIGRAVITY.md` for the
  recording evidence and the corrected physics/render lifecycle test.

## 2026-10-26 - Antigravity curved-road redesign

- Recorded the second tunnel test and supplied video as failed gameplay
  validation. The earlier rotation correction alone did not solve climbing.
- Found and corrected a local/world coordinate mix-up in road probes. Both
  continuity and lookahead now transform the capsule-bottom offset using the
  motor's transient position and rotation.
- Split passive road history from active adhesion: require gradual eligible
  curve progression, forward travel and matching road ahead; engage only within
  a calibrated engagement band and release below a hysteresis band. The
  world-angle threshold is capped at 47 degrees, eight before the user's
  observed 55-degree stop; native motor-relative stability is checked separately.
  Accept modest 20-degree facet steps independently of physics tick rate.
  Ordinary slopes, jumps and sharp floor-to-wall transitions cannot acquire it.
- Target the native main velocity boundary for scoped upward-cap handling and
  native-delta gravity redirection. Align grounded velocity before KCC movement,
  preserving magnitude and leaving accepted native jumps outward.
- Added optional surface camera alignment with transported loop orientation,
  native pitch/FOV retention and bounded near-plane volume obstruction checks.
- Luna implemented movement/camera changes and automated tests; the main task
  reviewed current native evidence and the user's recording. See
  `ANTIGRAVITY.md` for provenance, reference designs and validation limitations.
- Passed: 271 policy / 89 adapter / 59 camera assertions, 149 Python and 23
  frontend tests, 18 installed-interop hook signatures; zero-warning plugin
  build and source UI smoke. Installed artifact hash `afee55a27101b59a80412a2cf77817c29f3591f393604cc60e3b03420c362d89`,
  backed up at `local/backups/1792993683201624800`. New live tunnel test pending.

## 2026-10-26 - Antigravity motor-frame correction

- User's first live tunnel test failed: policy status said active but climbing
  stopped at the ordinary angle limit. Recorded it as failed gameplay validation.
- Current Ghidra slices prove `KinematicCharacterSystem.Simulate` commits cached
  transient rotation after the controller's after-update callback. The original
  transform-only change could be overwritten. Changed the adapter to set native
  `TransientRotation`, refreshing collision axes and preserving interpolation
  baselines. Compare native cached rotation, not just the displayed transform.
- Luna implemented the correction and expanded the tests before reaching its
  usage limit; main task finished clock-stub/guard regression checks and final
  verification. Adapter tests now model actual motor-up-derived ground support,
  distinct transient state, staged slopes and the final native-style commit.
- Passed: 120 adapter / 393 policy assertions, 147 Python tests, 18 actual
  interop hook signatures; plugin compilation has 0 warnings/errors. HUD reports
  road/policy/motor tilt and disagreement at 5 Hz. Repeat driving test pending.
- Corrected installed DLL hash:
  `4caffc63f2525e1dc5710567fd3040eb4211564edc481315eb2297feb523d141`.
  Installed with the game stopped, backed up at
  `local/backups/1792991626496802500`; existing settings preserved.

## 2026-10-26 - Experimental Antigravity

- Added a default-off first-class Community Mods driving module for continuous
  road banks/loops, with mandatory online/submission protection and local-human
  eligibility. Luna agents implemented the module and tests under main-task
  design/native review.
- Freshly inspected the installed 0.1.4.30 GameAssembly/metadata with Ghidra
  function slices and current interop signature checks. Corrected surface axes,
  native yaw mapping, local acceleration, drift-hop impulse/gravity direction,
  and already-projected velocity handling. Kept native collisions, slope limits,
  jumps/drift side effects, checkpoints and recovery rules.
- Added finite drift-hop grace from accepted native jumps, tangential-speed and
  curvature limits, wall/respawn/trigger/body exclusions, status display and
  disable/recovery/rewind cleanup. Detailed constraints and evidence are in
  [ANTIGRAVITY.md](ANTIGRAVITY.md).
- Verification: clean plugin build; 393 policy and 59 headless production-adapter
  assertions; 147 Python and 23 frontend tests; 18 real-interop hook/signature
  resolutions. Source UI assets/catalog served correctly; visual automation was
  unavailable. No in-game handling claim.
- Installed DLL hash `f492c0e137aeb44722bea4925cc0751e0e169b7bb4feb8db46a10be8a59d394d`
  verified, with the game stopped. Rollback backup:
  `local/backups/1792990481967402300`. Module left off; game not launched.
  User-proposed tunnel test: Workshop item `3268524772`, pending live feedback.

## 2026-10-18 - Standalone race tools 0.1.1

- Fixed apparently inactive controls after a saved player choice stopped matching
  the local racer. Reproduced Player 3 yielding zero callbacks; Player 1 resumed
  immediately. Added automatic local-player detection, active choices on both
  settings pages, and a visible fallback for unavailable saved slots.
- F8 opens an interactive camera panel over the game in windowed/borderless mode.
  Back to game closes the panel without exiting; Desktop returns to full settings.
  The overlay follows app/game focus and has contrasting controls and resets.
- Added selection/expiry regression assertions. Native core and Windows UI checks
  passed; live FOV 75 -> 80 -> 75, repeated reconnects and continued sampling
  passed with zero bridge faults. User confirmed the F8 overlay is visible over
  the game. Python suite: 136 passed. Details: STANDALONE-PRESENTATION.md.
- Preserved camera/input preferences and selected Automatic for this user's saved
  player. No game files were changed.

## 2026-10-18 - Standalone race presentation app (unreleased)

- Added Bobby's Race Tools: a native Win32 executable with embedded x64 bridge,
  camera controls, individual resets, an input overlay and portable settings.
  Defaults keep camera overrides off. No BepInEx or bundled managed/web runtime.
- Resolves exact game contracts from IL2CPP metadata at connection time; no fixed
  game addresses or game-build allowlist. Main-thread detours exchange bounded
  snapshots without per-frame metadata walks or scene scans. Heartbeat expiry and
  disconnect return to native camera behavior without unloading active detours.
- Preserved mapped keyboard/controller actions and local-input priority when
  opponent ghosts are present. Sample replay click pulses before native clearing.
- Native core tests and real Win32 UI smoke checks passed, including resets,
  tab switching, narrow scrolling, saved settings and relocated-folder launch.
  Existing Python suite: 136 passed.
- User-opened local race with BepInEx disabled passed
  connection, live input HUD and camera FOV on/off (75 -> 65 -> 75), zero reported
  faults. Restored original loader after game exit, SHA-256 verified. Detailed
  current native evidence and remaining runtime checks are in
  STANDALONE-PRESENTATION.md. No game was launched by the agent.

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
  tested afterward, then installed with backup `1792348676629098500`; its installed
  DLL checksum matches the verified build. A live race against a ghost after that
  final installation remains unverified. No game was launched by the agent.

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
# Mine trigger, Air Shift and ricochet follow-up (2026-10-19)

- Restored the mine prefab's enabled `MineBehavior`, trigger and animator. The
  prior custom overlap scan missed the kart damage owner in practice and the
  immediate `Destroy` after `ExplodeMine` could cancel the native effect. Traps
  now arm with `ActivateMine`, route actual Unity trigger contacts through the
  native explosion/damage method, and leave its effect object alive to clean up.
- Air Shift now defaults to a more visible 35 m/s lateral correction for 0.4 s,
  with the total-speed budget off. Ground testing is enabled and repeatable;
  airborne one-shift-per-jump behavior is retained. Suggestions remain uncapped.
- Removed Ricochet Items and its UI/catalog bindings after the reported bazooka
  ground explosion and failed wall bounce. The normal bazooka remains unchanged.
- The user-provided gameplay clip could not be opened by the desktop browser's
  local-file policy; diagnosis used the attached gameplay screenshot and current
  native mine evidence. Visible acceptance of the new collision path still needs
  a fresh offline game test.
- Validation: 143 Python tests passed; plugin build completed with zero warnings
  and errors; all 9 current Air Shift and Mine Trap Harmony signatures and
  handler arguments resolve against installed interop. DLL installed after the
  game closed; artifact and installed SHA-256 match
  `ED5E19C00BD9ACCD1CE76392DFE10FB8D49203BF0992D6CA30F7E7A0235C8590`.
  DLL backup transaction: `local/backups/1792442040843668100`. The user's
  existing Air Shift values remain; only `LimitHorizontalSpeed` was switched
  off, with the prior config copied to
  `local/backups/1792441949483888900/airshift-config-before.cfg`.

# Air Dash and mine trigger retry (2026-10-19)

- Replaced the ground-test shift with an airborne-only lateral burst at the
  final KCC velocity boundary, preserving heading and total horizontal speed.
  A short cyan streak marks the direction. Suggested start: 3 m over 0.12 s;
  distance and duration are freely tunable. Teleport Dash portals and item
  logic are not invoked.
- Added an offline proximity fallback around armed mines that calls the native
  `MineBehavior.ExplodeMine` entry, while keeping native trigger contacts. The
  UI now reports and logs placement, arming and trigger attempts separately.
  Owner triggering is enabled for offline practice.
- Filtered retired config entries before producing the UI's extra-settings
  cards. This prevents the old MineTraps `TriggerRadius` value from creating a
  second non-collapsible module card. Retired Air Shift tuning fields are also
  hidden; the new Air Dash distance replaces them.
- Preserved the user's settings and changed only the saved `OwnerCanTrigger`
  value to true. Config backup: `local/backups/airdash-mine-config-before-20261019.cfg`.
- Validation: 143 Python tests, 23 UI tests, UI production build, 30,012
  deterministic math/history assertions, and all 9 current runtime hook
  signatures passed. Plugin build has zero warnings. The plugin was redeployed
  with final backup transaction `local/backups/1792443636185269700`. The game was not
  launched; mine damage/effects and the Air Dash streak still need in-game
  confirmation.

# Mine throw and Air Dash tuning (2026-10-20)

- Mine test drops now offer both native `MineBehavior.ThrowMine()` from in front
  of the kart and stationary surface placement behind it. The trap scales to
  1.8x, with separate forward-force and rear-distance controls.
- Mine damage defaults to 80, proximity trigger radius to 4 m, and owner contact
  is always enabled. At 45 seconds, the native explosion targets the nearest
  kart within a configurable 10 m timed blast radius; without a nearby kart, the
  native target-requiring method cannot detonate and the trap is removed. The
  obsolete behind-distance and owner-toggle settings are retired.
- Replaced Air Dash's distance target and cyan LineRenderer streak with the
  requested 65 m/s lateral impulse held for 0.25 seconds. The velocity hook
  keeps kart heading and vertical velocity and caps total horizontal speed.
  Suggested values remain editable without an upper ceiling.
- Build, UI, math/history and exact hook-signature checks passed. The plugin was
  installed after the game process exited; visible mine and Air Dash behavior
  still needs a fresh in-game validation.

# Mine throw, scale and timer correction (2026-10-20)

- Replaced the native backward-directed throw path with a Rigidbody launch from
  four metres ahead of the kart. The mine now raycasts for the track while
  descending and settles as a stationary armed trap; rear surface placement
  remains available. The mesh root scales to 5x and primitive colliders are
  inversely scaled to preserve native collider size.
- Removed the tunable arming delay (fixed at 0.6 s), grouped remaining controls
  into item-box, placement and trap-behavior sections, and retained the 45 s
  timer, 10 m timer target radius, 4 m proximity radius and 80 damage defaults.
  The shipped explosion prefab has no `EffectSettings` component, so the native
  splash-damage radius cannot be changed through that field.
- Optimized the mine scan to reuse a kart list refreshed twice per second rather
  than searching the whole scene once per mine. The forward flight uses one
  lightweight surface raycast per tracked mine every 0.05 s.
- Validation: 143 Python tests, 30,012 deterministic math/history assertions,
  23 UI tests, UI production build, C# build with 0 warnings/errors, and all 9
  runtime hook signatures passed. Deployment completed with TheKarters2.exe
  closed. Artifact and installed DLL SHA-256 match
  `4C73A4C466816DCBB2ACBC9156034CF1CF5FDE246984AEC316C184449B73BA78`; final
  backup transaction: `local/backups/1792446359041903300` (the prior build is
  also recoverable from `local/backups/1792446283786536200`). These checks do not
  validate mine appearance, landing, area damage or timer detonation in game.

# Mine placement, size and timeout fallback correction (2026-10-20)

- User testing and the current BepInEx log showed the timeout fallback passed the
  owner's kart to `ExplodeMine`; that native method anchors the effect to the
  supplied kart, so a remote player appeared to explode. The empty-radius path
  now spawns the mine explosion prefab at the mine and never targets the owner.
  Timed detonation applies the game's native HP hit to every kart within the
  configured radius.
- User logs also confirmed that forward mines spawned but never reported a track
  landing. The launch now starts six metres ahead, corrects a reversed kart
  forward vector against horizontal motor velocity, and falls back to all normal
  raycast layers if the game's environment mask misses the track. Mine scale is
  re-applied during the runtime scan in case native startup resets the root
  transform. A one-time flight diagnostic records position and velocity if it
  still has not settled.
- Timed detonation plays the mine prefab at its position and calls native
  `HpBarController.Hit` for all karts within 10 m. This avoids native
  `ExplodeMine(target)`'s target-anchored effect. The prefab itself still has no
  `EffectSettings` radius; the configured damage radius is applied by the module.
- Validation: 143 Python tests, 30,012 math/history assertions, plugin build
  with 0 warnings/errors and all 9 Harmony hooks passed. Deployment completed
  with the game closed; artifact and installed DLL hashes match
  `08652BB01D5F9D737C20419F60501F13C07EFA9D0982DD6FB1CD0A54B2D61DFF`.
  Backup: `local/backups/1792446930281748800`. In-game forward movement, model
  size, mine-position VFX and damage still need confirmation.
