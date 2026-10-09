# Race performance module

**Follow-up:** the player tested the installed module, including AI interval 4, and reported no noticeable FPS/RAM improvement. The latest log confirms activation, but does not quantify hook execution or subsystem timings. Runtime 0.6.10 adds a separate bounded [diagnostic capture](PERFORMANCE-DIAGNOSTICS.md). Performance was subsequently re-enabled in the saved config; the diagnostic deployment preserves that state. Turn it off for a baseline capture. The 0.6.9 notes below describe the initial installation, not the current settings or a demonstrated fix.

Race performance is an optional Toolkit Essentials module for local offline races. It addresses repeated draw-distance allocations and the game's managed garbage-collection policy. An additional AI physics option uses the game's existing lower-quality motor path. Improvements depend on the track, camera count, opponents, hardware and current bottleneck; no FPS gain is guaranteed.

## Player controls

Open **Toolkit Essentials → Race performance** in Studio. The module starts disabled. Its two allocation/memory controls start selected, but have no effect until the module is enabled. Start with those controls and leave Lower AI physics off for a matched comparison.

| Config key in `[Performance]` | Default | Effect |
|---|---|---|
| `Enabled` | false | Enable the module for offline play. |
| `CacheDrawDistance` | true | Reuse camera distance settings, avoiding the native repeated per-camera array allocation when Medium/Near draw distance is active. |
| `RaceGarbageCollection` | true | Keep managed garbage collection enabled during offline races instead of suspending it until a safe non-race moment. |
| `LowerAIPhysics` | false | Opt into the native lower-quality AI motor path. Movement and collision accuracy can change. |
| `AIPhysicsInterval` | 2 | Number of physics ticks between selected AI motor updates, from 1 to 4. Used only with Lower AI physics. |

Draw-distance caching is relevant when the game uses Medium or Near draw distance. It does not lower texture quality or unload scene assets. Allowing collection can reduce the accumulation of collectible managed objects, but collection itself may cause brief frame pauses and cannot release referenced textures or all Unity native memory.

AI interval 1 leaves the native AI policy untouched. Higher values update selected motor work less frequently, with the native delta-time scaling. Some motor work, AI steering, weapons and player collision checks still run independently. This is an experimental movement tradeoff, not an overall AI update-rate limiter. Local human players and time-trial ghosts retain normal motor quality.

The module is disabled in online/network play. Disabling it restores the native GC/physics policy and draw-distance handling. Other mods that own the same settings may interact with restoration; test combinations separately.

The 2026-10-09 local installation has the module enabled, Cache draw distance and Allow race garbage collection selected, and Lower AI physics off. Reopen `Launch Studio.cmd` to load the new source-app catalog. A new game launch is needed for the updated DLL. The startup log should report toolkit 0.6.9 and the performance capability, followed by `Race performance active`. If a hook encounters a runtime failure, the module suspends for that game session, attempts independent restoration, logs the failure and falls back to native behavior; restart after correcting the cause.

For a comparison, use the same track, resolution, camera, graphics options, opponent count and race segment. Record FPS/frame time, process working set and private bytes before and after, and note whether a collection pause occurred. Compare more than one race; loading/menu transitions are different from steady racing. Test Lower AI physics separately from the other controls.

## Developer handoff

Runtime ownership is `plugins/TK2.Customization/PerformanceFeature.cs`; catalog ownership is `studio/pack.py`. `[Performance]` bindings must retain the keys, defaults and bounds above. Catalog membership is Toolkit Essentials. The AI interval control is editable only when Lower AI physics is selected. The module's gameplay marker reflects the optional AI movement changes.

Preserve exact full-signature Harmony target resolution, current installed interop references, original-state capture and offline guards. Restore ownership on disable, race exit, online transition and plugin unload. Camera caches must handle new/destroyed cameras and setting changes; do not overwrite gameplay camera framing. GC hooks must not suppress safe transition cleanup or force a synchronous full collection each frame. AI cadence must use the existing native motor flags rather than changing the global fixed timestep or throttling human/ghost simulation.

### Changes suitable for a native game fix

- In the nonzero draw-distance branch, replace the per-frame `Camera.allCameras` array with a reusable buffer populated by `Camera.GetAllCameras`. Cache the 32-float cull arrays for Default/600/300. Keep layers 8 and 31 at zero, the terrain/environment/shadow branches and the existing per-frame camera setter behavior. Clear retained camera references after processing and release scene buffers in the menu. The mod deliberately keeps the setters to preserve interactions with camera owners; a game-owned dirty flag could additionally avoid them when camera membership and effective settings are unchanged.
- Audit allocation rate before suspending GC for an entire race. The mod enables automatic collection in offline races and does not call `System.GC.Collect`. A game fix should measure incremental collection budgets and allocation sources rather than periodically performing full collections in active racing. Referenced textures require a separate residency/ownership audit.
- Profile collision scans, AI plane checks, spline calculations and motor phases separately. The optional cadence control reuses the existing native path; it does not remove the all-player scan before that path. A developer can add a stable broad phase or reuse pair calculations only after validating directional bounce behavior and update ordering. Those collision changes have not been implemented or proved here.
- The toolkit's disabled legacy controller hook now returns before accessing kart/boost wrappers when reserves and air brake are off and there are no reserve originals to restore. This removes avoidable bridge calls from every kart's physics tick. It is a toolkit fix, not an explanation for the stock game's cost.

Current interop signatures were checked directly from Assembly-CSharp.dll (SHA-256 `7287268ca8a540c42f72525e0781e93643617427b109fe40446f347263529337`): all four Harmony targets are unique parameterless void methods. LateUpdate/FixedUpdate are instance methods; the two GC policy methods are static. Evidence is in ignored `local/performance-mod-interop/signatures.json`.

## Native evidence

The investigation used current GameAssembly.dll SHA-256 `e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130`. Read [the investigation](PERFORMANCE-INVESTIGATION-2026-10-09.md) for measured observations, attribution limits, historical memory context and preset differences. Generated pseudocode is in ignored `local/performance-2026-10-09`; it is not original C# source.

| Native target | Observation supporting the control |
|---|---|
| `PTK_GraphicsDetailApplier.LateUpdate` at `0x1806cf040` | `native/pseudocode/1806cf040_PTK_GraphicsDetailApplier__LateUpdate.c:101–140` allocates a new 32-float array per camera on every nonzero draw-distance update; the unchanged-value gate exists only for Default. Ground layer 8 and do-not-cull layer 31 remain unlimited. |
| `Ant_CurrentGameConfiguration.PTK_GC_ApplyCurrentPolicy` at `0x1805020f0` | `native4/pseudocode/1805020f0_Ant_CurrentGameConfiguration__PTK_GC_ApplyCurrentPolicy.c` chooses Disabled while a race-disable request is active and community logging is off. Logging changes this policy, so avoid treating a logging-enabled benchmark as equivalent. |
| `Ant_MainGame.<StartAndInitializeRace_Coroutine>d__91.MoveNext` at `0x180530f20` | `native5/pseudocode/180530f20_Ant_MainGame._StartAndInitializeRace_Coroutine_d__91__MoveNext.c:363–371` calls the race GC-disable request. |
| `PixelEasyCharMoveKartController.FixedUpdate` at `0x1805d77f0` | `native/pseudocode/1805d77f0_PixelEasyCharMoveKartController__FixedUpdate.c:219–240` copies the lower-physics setting and per-controller cadence to eligible motors; human/TT-ghost types are exempt. |
| `KinematicCharacterSystem.Simulate` at `0x180d560c0` | `native2/pseudocode/180d560c0_KinematicCharacterController.KinematicCharacterSystem__Simulate.c:57–81` runs eligible phase-1 motor work every X ticks and scales delta time. Phase 2 still runs every tick. |
| `KinematicCharacterMotor.UpdatePhase2` at `0x180d3e6b0` | `native2/pseudocode/180d3e6b0_KinematicCharacterController.KinematicCharacterMotor__UpdatePhase2.c:164–167,515–558` gates selected collision/velocity work, not the whole opponent. |

These paths support implementation choices. They do not prove that any one path caused the reported 68 FPS race versus 130 FPS time trial, or that this module fixes all RAM use. Asset residency, native allocator reserves and unrelated per-opponent work remain outside its scope.

## Validation and deployment

Runtime **0.6.9** builds against the installed game/loader references with **zero warnings and errors**. Validation passed **86 Python tests**, **133 frontend assertions**, the **96-control static UI smoke**, and **79 assertions** against the actual PerformanceFeature.cs compiled with lightweight API stubs. The harness covers effective graphics branches, array reuse, camera membership, exempt layers, default reset, native fallback, human/ghost exclusions, online/menu/master-disable restoration, current-policy GC restoration, and exceptions while restoring AI state. Stub tests do not prove native marshaling, real Harmony dispatch, race completion or FPS gains.

Installed DLL and build artifact hashes match: `e48e1628cd0586822b5d5daee125bb83e80d0b15ae75eeae2b31d82f2aae54b4`. Deployment used the SDK's integrity checks and rollback transactions. DLL backup: `local/backups/1791519125526067100`; config backup: `local/backups/1791519125533389300`. Receipt: `local/performance-mod-deployment.json`. Other config values were verified unchanged. No original game binaries, save files, portable executable or distribution ZIP were changed/built; the game was not launched automatically.

In-game behavior and FPS/RAM effects still require the controlled player test above. The build is a tested experimental workaround, not a demonstrated recovery from 68 to 130 FPS. To roll back with the game closed, restore the two receipt transactions through `studio.core.restore_backup`; it rejects restoring over newer file edits. Source-app 0.6.4 upgrades old source backends on launch so the new module appears; portable apps need their own requested rebuild.
