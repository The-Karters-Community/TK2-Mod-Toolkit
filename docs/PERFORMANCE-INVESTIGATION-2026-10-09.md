# Performance investigation — 2026-10-09

The strongest current explanation for the reported 68 FPS with seven CPU opponents versus 130 FPS in time trial is additional CPU work from opponent simulation and kart interactions. The graphics presets leave much of that work intact. This is a supported hypothesis, not a timed attribution to a particular function. High memory use is established; a memory leak is not.

## Evidence and scope

Installed game: 0.1.4.18. Current GameAssembly.dll SHA-256 matches the symbolized Ghidra project: `e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130`. Five read-only export passes recovered 52 selected native bodies, with zero decompilation failures. Original binaries and the project were preserved. No game restart, settings change, installation, or optimization patch was performed.

REA MCP is connected. Its Windows Ghidra bridge explicitly rejects shared-library targets (`target_role_unsupported`), so it could not analyze GameAssembly.dll. Native evidence below came from the SDK's existing ExportTk2.java exporter against the existing project using `-readOnly -noanalysis`. REA inspected the installed toolkit's actual managed metadata and CIL, without loading or executing the assembly. Its Evidence ID is `ev_2cd7bf819d71d75464febed3ccc7b9ac26f0f32adb59488a9f06dd64ede16a35`; plugin SHA-256 is `c1b7dfbf2680ee6fbee76949140f0d162c128c608bf9ce5922650051c522b188`, also matching the SDK's built DLL.

Raw evidence stays in ignored [local/performance-2026-10-09](../local/performance-2026-10-09/): native through native5, export logs, target lists, process/GPU samples, REA bundle, constant decoding and three detailed finding reports. Native pseudocode is not original C#; IL2CPP shared-address aliases sometimes show misleading type/callee names. Conclusions use current layouts and concrete control flow rather than those names alone.

## Live observations

The user reported approximately 68 FPS in a race against seven CPUs, 130 FPS in time trial, and fluctuating FPS. These imply approximately 14.7 versus 7.7 ms/frame, a difference of 7.0 ms. They are user observations, not a captured matched benchmark.

Hardware inspected: i5-11600KF, six cores/twelve logical processors, RTX 4060 Ti, approximately 32 GiB system RAM. Available physical RAM was approximately 11.2 GiB at the initial snapshot; that snapshot does not indicate system RAM exhaustion.

| Metric | Observation | Limits |
|---|---|---|
| Process working set | 3.74–4.27 GiB | Resident process pages, 28.30-second sample |
| Process private committed memory | 6.78–7.97 GiB | Not all physically resident; do not add to working set |
| Process CPU time | Average 2.34 logical CPUs | About 19.5% of twelve logical CPUs |
| Busiest persistent thread | 91.3% of one logical CPU | Thread role and sampled call stacks were not captured |
| Game GPU 3D engine | 31.8–32.2% | Separate, later three-sample window, not synchronized frame timings |
| Game dedicated GPU memory | 2.82 GiB | Windows process counter; separate from system RAM |
| Game shared GPU memory | 37.5 MiB | Not evidence of a major VRAM spill in this window |

Process sample: 06:46:54–06:47:22 Helsinki. GPU sample: 06:48:09–06:48:11. The game was already running; no automation controlled its race or camera. Samples could include changes in game state. Ghidra exports ran concurrently, another reason these are observational evidence rather than a clean benchmark. Private memory dropped sharply during the process window, so this window does not show monotonic growth. These counters support investigating CPU limits first, but do not establish which thread or function is the bottleneck.

## Why presets can make little difference

The complete native preset setter at `0x1806b82d0` / `0x1806baf00` produces:

| Setting | Performance | Balanced | Quality |
|---|---|---|---|
| Shadow quality | Low | Low | Medium |
| Anti-aliasing | FXAA | FXAA | FXAA |
| Terrain | Low | Low | Medium |
| Detail density | Low | Low | High |
| Shadow detail | Low | Balanced | High |
| Opponent effects | Low | Low | Medium |
| Postprocessing | On | On | On |
| Ambient occlusion | Off | Off | Off |
| Bloom | Off | On | On |
| Anisotropic textures | All | All | All |

**Performance and Balanced differ only in shadow detail and bloom.** The setter does not change resolution, render scale, draw distance, FPS limit, VSync, or texture residency/mip quality. The screenshot shows Custom, High opponent effects and High (Original) terrain; no built-in preset selects those last two values.

Settings do have application code: `PixelKartersSettingsManager.Update` at `0x1806e4a00` updates AA, shadow cascades, postprocessing, AO and bloom. There is no evidence that the entire settings menu is a no-op. Map flags, initial effect state and nullable overrides can change the effective result; these were not read live.

Low environment density at `0x1806ce1e0` disables selected Renderer.enabled flags. It preserves objects, mesh/texture references and colliders, and protects gameplay structures. Custom tracks and certain map types bypass this reduction. Hiding rendering is not asset unloading; [Unity documents Renderer.enabled as a visibility control](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Renderer-enabled.html). This explains why less visible detail need not substantially lower RAM.

Opponent quality at `0x1806d4600` and `0x1806d6e10` reduces animation, particles, wheels, shadows and suspensions, with local/viewed/replay exceptions. It does not remove AI steering or kart physics. Low caps stable selected AI/network visuals to three by default; it does not reduce the actual racer count.

Detailed evidence: [graphics-findings.md](../local/performance-2026-10-09/graphics-findings.md).

## Concrete costs that grow with opponents

1. **Kart interaction scans.** `PixelEasyCharMoveKartController.FixedUpdate` at `0x1805d77f0`, line 62, calls `UpdatePlayerPlayerCollision` before configuring AI motor throttling at lines 219–240. The collision routine at `0x1805dc2d0`, lines 111–112, bypasses its scan when the time-trial collision-disable flag is set. Otherwise each eligible kart scans the racer array, filters self/disabled entries, checks distances, and tests nearby capsules. Eight eligible karts can produce 56 directional other-kart distance checks per fixed tick. These are not 56 unconditional capsule collisions. This is a confirmed race/TT work difference; its elapsed time remains unknown.
2. **AI driving and weapons.** `AILogicController.Update` at `0x1804cd100` calls trigger-plane checks and steering. Trigger checks at `0x1804ccd60` iterate cached jump/block planes and transform kart coordinates. Eligible weapon AI at `0x1804d13c0` also refreshes information and makes usage decisions. These paths are separate from lowered motor cadence.
3. **Per-kart physics and spline work.** The character system simulates registered motors; active kart followers calculate Bezier distance and select four target points. AI motor quality really does skip some heavier work every X ticks, but phase2 still enters each tick and controller collision scans remain outside that throttle. Capsule casts/overlaps use NonAlloc APIs, so they are not evidence of allocating a new query array every call.

Current native timing policy at `0x1806e1660` / `0x1806e29e0` uses `0.00833` seconds for standard gameplay (about 120 Hz), and `0.005` for selected modes or leaderboard-enabled configurations (200 Hz). The floats were decoded from the current PE at VAs `0x1837082a4` and `0x183707d9c`; metadata agrees. Current effective timestep and AI cadence were not read live. Graphics presets do not change this policy. At 68 FPS, 120 Hz implies approximately 1.76 physics ticks per rendered frame on average when normal time progression is maintained; 200 Hz implies 2.94. Unity can perform multiple fixed updates within a rendered frame, which can amplify fluctuations when physics work is expensive. [Unity timing documentation](https://docs.unity3d.com/2022.3/Documentation/Manual/TimeFrameManagement.html).

Detailed evidence: [opponent-findings.md](../local/performance-2026-10-09/opponent-findings.md). This evidence justifies profiling those routines first; it does not justify replacing them or disabling collisions without measurements and behavioral validation.

## Memory mechanisms and limits

**Race GC policy is explicit.** Race-start coroutine `0x180530f20`, line 371, invokes `PTK_GC_DisableForRace`. That method at `0x180502280` sets the race flag. `PTK_GC_ApplyCurrentPolicy` at `0x1805020f0` selects GCMode Disabled when that flag is true and Community logging is off; it selects Enabled otherwise. Current enum metadata confirms Disabled=0 and Enabled=1. Actual current GC mode was not queried. Continuing managed allocations while GC is disabled can accumulate memory until collection is restored. This is documented Unity behavior, and a concrete mechanism to measure rather than proof of the full RAM footprint. [Unity GCMode documentation](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Scripting.GarbageCollector.GCMode.html).

Cleanup exists. The native cup transition unloads the gameplay Addressables scene, loads its transition scene, then requests unused-asset cleanup. `PTK_SafeMemoryCleanupCoroutine` at `0x18051a6d0` enables GC, optionally yields Resources.UnloadUnusedAssets, then performs Collect, WaitForPendingFinalizers, Collect. This synchronous work is a possible transition hitch; execution time during the reported race was not measured. Unused-asset cleanup preserves still-referenced assets, as described by [Unity's API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Resources.UnloadUnusedAssets.html).

A concrete small allocation issue exists in graphics `LateUpdate` at `0x1806cf040`: Medium/Near draw distance allocates a fresh float[32] for each camera every rendered frame and reapplies layer cull distances. The unchanged-setting guard applies only to Default. Presets leave draw distance unchanged. This is at least 128 bytes payload per camera/frame, approximately 60 MB/hour/camera at 130 FPS if unreclaimed, plus array headers and other camera API costs. It is worth fixing after confirming the active draw setting, but cannot alone explain several GiB in a short session.

Historical September 23 diagnostics recorded roughly 3 GiB texture memory even in a menu with zero motors. They support investigating texture residency independently of AI. They predate this binary, do not identify individual assets, and are not a current heap census. Their logging-enabled GC behavior may differ from normal gameplay under the current policy. A past community-track AO exception flood is also documented, but no current recurrence was established. Disk asset sizes are not treated as RAM measurements.

Detailed evidence: [memory-findings.md](../local/performance-2026-10-09/memory-findings.md).

## Toolkit contribution

The current config has all 27 Enabled entries false, including frame limiter and rendering overrides. The startup log lists one plugin and has no runtime exception flood. Nevertheless, the toolkit installs Harmony hooks while features are disabled, and runs StudioBehaviour.Update and config file reads/hashing on the main thread. REA's installed-DLL CIL confirms these call paths; this is not inferred solely from SDK source. The disabled toolkit remains an unmeasured benchmark confounder. No evidence assigns it the 7 ms difference or the large texture footprint.

## Next measurements and optimization order

Capture the same track, resolution, camera and warmed-up interval in TT and races with 1, 3 and 7 opponents. Record frame-time distribution, CPU/GPU frame times, collision/AI/KCC/spline durations, actual fixed timestep/cadence, GC mode, managed allocation rate, and texture/native allocator totals. Repeat one matched run without BepInEx to quantify hooks. Do not enable Community diagnostics without recording that it changes GC policy. No baseline restart or mod removal was performed here.

First optimization targets after timing: cache the repeated draw-distance array; reduce duplicated kart-pair work while preserving collision behavior; verify and tune the existing AI cadence rather than adding a second incompatible throttle; reduce expensive spline/AI scans with measured reuse; audit actual retained textures and asset ownership separately. Broadly changing the physics timestep would affect racing behavior and leaderboard consistency and is not the first proposed fix.

| Original question | Status |
|---|---|
| Why presets make little difference | Answered structurally: similar presets and rendering-focused controls |
| Why opponents add work | Answered structurally; leading CPU hypothesis supported by process counters |
| Which function consumes the extra 7 ms | Unresolved: no native sampled stacks or timed method capture |
| Why RAM remains high | Partially answered: renderer retention, GC policy and historical texture footprint |
| Is there a leak | Unresolved; no matched long-term heap/residency evidence |
| Has an optimization been installed and tested | No; this task produced investigation evidence only |
