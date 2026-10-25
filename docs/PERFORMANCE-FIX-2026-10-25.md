# Runtime allocation fix, 25 October 2026

## Installed result

A concrete allocation bug in the toolkit has been fixed and verified in an offline race at full texture quality. The fix is automatic; no optimization module needs to be enabled. It changes neither item rules, AI cadence, fixed timestep, render scale, texture quality nor the game's GC policy.

Installed DLL SHA-256: `b52fcc410d6c458faa44f99dd0431607646dd4ce5f25c614d29c4333c972d276` (local development build of runtime 0.6.26).

Game 0.1.4.30 / beta_dev, Unity 6000.0.75f1, Direct3D 11. GameAssembly SHA-256: `1104a3b50e15c4316c90f3f98c0441871140b8ca8eee53a6bd6380c744be6d39`. UnityPlayer SHA-256: `5da2e6c1050924f370b8143256a1705ac3758bb78eb07013a268058edeb6709a`. Hashes identify evidence, not compatibility allowlists.

## Cause and correction

`LegacyMK.Parameter<T>.Apply` called `UnityEngine.Object.GetInstanceID()` before checking whether a parameter was active. The toolkit has 35 kart and 22 boost parameter controls. Their hooks run on physics ticks even when both modules and every override are disabled. Every unnecessary interop identity call creates a boxed Int32 in the game's IL2CPP heap.

The source now returns before that call when the parameter is inactive and its saved-original dictionary is empty. If it previously changed a value, the old restoration path still runs. Active overrides still capture the native baseline once, apply edits, and restore their own original values.

REA inspection of the actual pre-fix installed DLL confirms that `Parameter<T>.Apply` called `GetInstanceID` at IL offset 6 before testing activity. Inspection of the final installed DLL confirms the early condition and empty-original check precede that call, now at offset 36. This is compiled CIL evidence, not an inference from source alone.

## Native before/after observations

Both captures used fixed native counters on the game's existing main thread for approximately six seconds. Counters observed selected allocation helpers and runtime invocations; they did not change GC, physics, textures or item logic. Fixed native counters avoided a JavaScript callback for every allocation. The game was user-driven offline, with the requested 120 FPS limit. These are short observations, not controlled FPS benchmarks.

| Main-thread observation | Before | After |
|---|---:|---:|
| `GetInstanceID` invocations | 345,201 | 12,710 |
| Invocations per nominal second | 57,533.5 | 2,118.3 |
| Boxed Int32 allocation count | 353,872 | 19,161 |
| Boxed Int32 estimated instance bytes | 7,077,440 | 383,220 |
| Estimated bytes from observed object-allocation helper | 9,056,481 | 1,939,103 |

The observed identity-call reduction is **96.3%**. Boxed Int32 estimated bytes fall by about **94.6%**. Estimated instance sizes exclude allocator rounding and overhead; the last row covers only the selected object-allocation helper, not all game allocations. Array/string paths can overlap object allocation and are not added to these totals. Other native calls and toolkit checks remain.

The first exploratory allocation recorder grouped by caller alone. A shared boxing function therefore mixed value types under the first observed type, Boolean. That initial type label was unsuitable for a precise claim. The final before/after recorder groups by caller **and type** and separately identifies runtime-invoked methods and their originating call sites. The table uses those corrected captures.

The live call sites for the high-frequency identity, online-status and ObscuredBool conversions were managed/JIT addresses, whereas ordinary MonoBehaviour updates came from UnityPlayer's native dispatch. This distinguishes interop traffic from ordinary game component dispatch. The source/CIL defect and large post-fix reduction establish a real toolkit contribution; they do not attribute the entire game's RAM footprint to the toolkit.

Raw evidence remains ignored in `local/performance-lab-2026-10-25`: `allocation-origins.json`, `allocation-origins-after.json`, `allocation-fix-comparison.json`, REA evidence and other bounded captures. Native scripts and game-derived pseudocode are private analysis artifacts, not distributed game source.

## Items, physics and engine findings

The full-quality baseline with optimizations disabled measured approximately 0.62 ms/rendered frame in KCC FixedUpdate, 0.12 ms in the nested kart-to-kart collision span, and 0.012 ms in AI weapon decisions. These inclusive spans overlap and omit other engine work. They do not establish physics or weapon decisions as the dominant frame cost. Toolkit prefix work can also fall outside the original-method timing spans.

Additional bounded timing targets now include `Ant_Player.Update`, `PixelKartPhysics.FixedUpdate`, AI input FixedUpdate/ProcessRacingInput and five projectile/weapon update paths. In the experimental capture, missile visual updates were small. `WeaponProjectileCollision.Update` and `WeaponSceneController.Update` had no dispatches in that interval, so their costs remain unmeasured. No item behavior, collision system or global timestep was changed on the strength of these observations.

A separate native probe successfully read CPU/GPU frame counters and GC state. The shipped collector reported **non-incremental** operation. It was disabled during ordinary racing and enabled in the menu. For the four Double frame-time counters, native `CopyTo` produced numeric Int64 nanoseconds, matching `GetLastValueAsDouble`; they are not IEEE-754 bit patterns. The Studio bridge now admits these observed time counters and integer counters, preserving units and rejecting unavailable/zero CPU/GPU timings. Other floating-point metrics remain unsupported.

The bounded stack sampler observed much of the main thread inside UnityPlayer. Stripped symbols, incomplete unwind frames and wall-clock sampling prevent assigning the remaining frame time to one precise engine function.

## Rejected texture/GC experiment

A temporary profile set the global texture mip limit to 2 and permitted race GC. In one warm-menu comparison, the loaded-texture counter fell from **1,287 MiB to 444 MiB**, and private commitment from **4,831 MiB to 3,881 MiB**. Resident RAM changed little.

The user reported visibly unacceptable menu texture degradation and heavy race stutters. After restoring full texture quality and native GC policy, the user reported no stuttering in the next run. Because that experiment changed two things together, it does not isolate which caused each stall. Non-incremental GC is a plausible source of pauses, not a proven attribution for every reported hitch.

**The experimental override code, catalogue controls and temporary settings were removed from the final build.** Its source was retained only as an ignored rejected experiment. Render scale stayed unchanged. No textures, saves or game asset files were rewritten.

The runtime census found **243 loaded mod-info objects and 2,396 linked thumbnail textures** after loading settled. Sampled track thumbnails were 960×540 DXT1 and non-readable. Focused current native decompilation confirms the game's thumbnail downscaler copies/resizes, compresses where appropriate and calls `Apply(false, true)`. This is consistent with the developer's supplied explanation of the October mini-update.

Unity's object-size query returned zero for many non-readable textures even though the aggregate loaded-texture counter was substantial. Zero object-size reports must not be treated as absence of GPU allocation. Thumbnail ownership in this census covers loaded `PTK_ModInfo` sprite links, not every image cache. Only one menu camera was active after loading settled; the other retained camera objects do not establish simultaneous rendering. Race camera count was not measured by that menu census.

## User validation and remaining limits

With the final allocation fix at full quality, the user said the run felt great but reported **one bad pause that dropped the displayed FPS to zero**. That isolated stall remains unresolved. Its exact time/call stack was not captured; live instrumentation may also have affected an observed pause. No claim of completely stutter-free play is made.

A later live process snapshot still showed about **3.79 GiB resident / 5.70 GiB private committed**. This is **not a validated 4 GB-PC solution**. Reducing a major recurring allocation source does not eliminate the game's asset, native allocator and driver requirements. Reliable operation with only 4 GB physical RAM requires further memory ownership work and testing on that hardware or an equivalent constrained environment.

## Validation and rollback

- Plugin compiled against current installed interop/runtime references with zero warnings/errors; deployed DLL hash matched the build artifact.
- Parameter regression harness links the production parameter source and passes nine assertions: no native identity/field calls while untouched and inactive, active baseline capture, live value edits, individual/master disable restoration, multi-target restoration and return to the no-interop path.
- Performance harness passes 114 assertions, including bounded hooks, next-race rearming, observed cadence, integer/Double-time handling, consumed samples and a one-shot census with timing disabled. Stub APIs are not in-game proof; the native captures provide that separately.
- Python settings/API tests and Studio UI smoke checks were run. The catalogue exposes only the new read-only snapshot action under Performance diagnostics; no low-quality optimization profile remains.
- Mandatory online protection is unchanged. All optimization and timing modules were returned to disabled. The parameter fast path requires no toggle.

The pre-change profile/config archive is `local/performance-lab-2026-10-25/before-changes.zip`. Deployment transactions, including the final replacement, are under `local/backups/`. Restore a prior plugin only with the game closed. Nothing was published or sent externally.

Primary references: [Unity 6000.0 recorder source and sample layout](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.0/Runtime/Profiler/ScriptBindings/ProfilerRecorder.bindings.cs), [Unity mip-limit semantics](https://docs.unity.com/en-us/engine/7000.0/script-reference/unityengine/qualitysettings/globaltexturemipmaplimit), [Unity GC mode behavior](https://docs.unity.com/en-us/engine/6000.7/manual/analysis/performance-memory/managed-memory/incremental-garbage-collection).
