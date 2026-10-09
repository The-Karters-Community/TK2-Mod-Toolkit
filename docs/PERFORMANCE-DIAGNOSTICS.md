# Performance follow-up and developer capture

The player has completed the capture. [Measured results and revised developer priorities](PERFORMANCE-CAPTURE-2026-10-09.md) establish that the selected kart/AI timers account for only a small part of race frame time, Default draw distance is active, and managed memory accumulates with GC disabled in both modes. The dominant FPS source is still outside the current probe's coverage.

The player tested runtime 0.6.9 with Lower AI physics enabled at interval 4 and reported no noticeable FPS or RAM improvement. The latest BepInEx log confirms the runtime loaded, the performance module was available and its active settings included AI interval 4. It then reports that the module was disabled. There is no logged module suspension. Those entries establish installation and selected settings, not per-hook execution or a quantified benchmark.

The first workaround has not demonstrated a performance fix. Its target allocations are limited to Medium/Near draw distance; GC cannot free assets that remain referenced. The AI motor option leaves collision scans, AI steering/weapons and other updates running. Their relative costs remain unknown. Do not assume collision scans are dominant merely because time trial bypasses them.

## Capture the next run

Runtime 0.6.10 adds a separate **Toolkit Essentials → Performance diagnostics** module. Source-app 0.6.5 refreshes the catalog when `Launch Studio.cmd` is reopened. Defaults are disabled and 60 capture seconds. The local diagnostic installation enables capture and preserves other settings. Performance was re-enabled while the update was being prepared; its saved setting was preserved at deployment (enabled, AI interval 4). Turn that module off explicitly for the baseline below.

1. Restart the game after installing the DLL and reopen Studio if it was already running.
2. Keep Race performance off. Drive about 75 seconds in time trial, then about 75 seconds on the same track against 7 CPUs. Keep graphics, camera and resolution the same. Keep the game focused while driving.
3. Exit the game normally. Results are automatically saved beneath the game's `BepInEx/diagnostics`, in `tk2-performance-<UTC timestamp>-<PID>.jsonl`. BepInEx/LogOutput.log names the file and records capture completion/failures.
4. Disable Performance diagnostics before a clean FPS comparison. Its hooks add overhead. Returning to the menu, pausing or losing focus stops the current capture; an uninterrupted race segment rearms after a five-second warmup. Each capture stops and removes its timing hooks after the configured 10–180 seconds.

No screenshot, debugger attachment, automatic game launch or gameplay change is needed. This capture is diagnostic instrumentation, not another claimed optimization. Captures remain local; no files are uploaded.

## Read the evidence

Each JSON line contains one approximately ten-second window (or a partial window at exit), mode/map identity, time-trial collision-bypass flag, fixed timestep, draw-distance mode, optimization state, average/p95/max frame intervals, resident/private process memory and engine memory counters. The p95 is a histogram upper bound quantized to 0.25 ms; `null` with `p95OverflowAt200Ms=true` denotes the >=200 ms bin. IO/reporting frames are excluded from the next frame-interval sample; this slightly changes the reported FPS relative to an external overlay.

Each method records dispatch count, total inclusive wall milliseconds and its longest call. Normalize the total by `elapsedSeconds` to obtain milliseconds per second of capture. **Do not add nested totals**: the controller includes its collision call, and KCC may include kart work. Times include detour/interop overhead and other patches inside the measured span. They are not GPU times or exclusive CPU samples. Zero dispatch count means that hook was not observed in the window; it does not prove the subsystem costs nothing. Compare equivalent windows from the two modes before assigning blame.

AI motor fields are sampled after the original controller at every 127th observed controller call. This rotates through normal racer counts instead of repeatedly selecting the same ordinal in an eight-kart loop. The sampled flag and interval establish the observed motor state, not how many phase-1 updates actually executed. No object reference is retained by sampling.

`Profiler.GetMonoUsedSizeLong` covers live and uncollected managed objects; this is deliberately read without forcing a collection, so it is not a live-object-only measure. [`GetTotalAllocatedMemoryLong`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Profiling.Profiler.GetTotalAllocatedMemoryLong.html) covers allocated engine memory and differs from process memory. The mod's .NET CLR heap is measured separately. Release-build counters can be unavailable or zero; null/zero must not be interpreted as proof of no allocations. [Unity 6 managed memory API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Profiling.Profiler.GetMonoUsedSizeLong.html).

No GPU/render-thread, audio, native physics jobs or asset residency breakdown is captured. If these ten spans account for only a small part of the race slowdown, a developer needs a Unity CPU/GPU profiler capture or native sampling trace, rather than throttling these methods speculatively.

## Targets and identity

Current game hash remains `e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130`. The ten exact targets are unique instance, parameterless void methods in the installed Assembly-CSharp interop assembly. Its checked signature bytes are `20 00 01`; ignored `local/performance-mod-interop/ProfileCheck.cs` verifies them without loading game implementation.

| Timed target | Current native address |
|---|---|
| PixelEasyCharMoveKartController.FixedUpdate | 0x1805d77f0 |
| PixelEasyCharMoveKartController.UpdatePlayerPlayerCollision | 0x1805dc2d0 |
| KinematicCharacterController.KinematicCharacterSystem.FixedUpdate | 0x180d54e80 |
| AILogicController.Update | 0x1804cd100 |
| AIWeaponLogicController.Update | 0x1804d13c0 |
| AIDistToTargetBehavController.Update | 0x1804b22d0 |
| PlayerBezierFollower.Update | 0x1806061d0 |
| PixelKartPhysics.Update | 0x1805e6180 |
| PixelEasyCharMoveKartController.Update | 0x1805dcd80 |
| PTK_GraphicsDetailApplier.LateUpdate | 0x1806cf040 |

These are the existing read-only Ghidra exports indexed in `local/performance-2026-10-09/evidence-index.json`. No new game implementation has been inferred from interop wrapper bodies. REA rechecked the currently installed 0.6.9 artifact before the diagnostic update: SHA-256 `ae4078b5521218711ebdbe7af0a4eabb9f17a654ada9cacd57f6b256b4a4899c`, MVID `067f309b-09d9-4cf1-a19c-6150745d8917`, evidence `ev_456f1f16900705036f040fc52de141f5815cc584d0fc2d1d90e055107ad8d62f`; full static evidence is retained in ignored `local/performance-mod-interop/loaded-plugin-rea.json`. That hash differs from the original deployment receipt after the player's toolkit rebuild; it does not itself indicate a runtime failure.

Diagnostic hooks use their own Harmony owner. They are installed only for a bounded capture and removed independently; disabling them does not unpatch other modules. A diagnostic failure logs the reason and removes its hooks on the next main-thread tick. Failed partial installation rolls back its hooks. Original method arguments/results and simulation settings are left intact. Sources: `PerformanceDiagnostics.cs`, `PerformanceSamples.cs`, and the read-only settings snapshot in `PerformanceFeature.cs`.

## Validation limits

Production compilation, settings tests and the production-source stub harness validate setup, counter math and lifecycle cleanup. They cannot validate real native dispatch, release-build counter availability, race completion or timing attribution. The game was closed while this update was prepared. The next player capture is required before a further performance fix can be justified.
