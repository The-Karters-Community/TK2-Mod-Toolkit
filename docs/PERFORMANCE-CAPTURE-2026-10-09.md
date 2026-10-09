# Measured race and time-trial comparison — 2026-10-09

The current capture does **not** establish AI steering, the kart-to-kart scan or the selected KCC motor routines as the dominant FPS bottleneck. Their measured costs are small relative to the complete frame interval. It does establish steady managed allocation while race GC is disabled in both modes. That growth is much smaller than the total process footprint and is not evidence of a permanent retention leak.

The first workaround has not demonstrated an FPS or RAM improvement on this machine. Further changes to collisions or AI cadence are not justified by these measurements alone. The next diagnostic scope is the native engine/player presentation/rendering work and waits not covered by these ten method timers.

## Capture identity and comparisons

Player-run game session: toolkit 0.6.10, PID 27188. Source: `BepInEx/diagnostics/tk2-performance-20261009-082359-27188.jsonl`, SHA-256 `55e7930ffbdd7484e150d94e037c2fa1b75bcea533531cf0236af534921bf3b6`. All 16 records parse successfully and identify map **265600**. Performance is inactive in every recorded context. Although the subordinate AI toggle/interval retain true/4, the disabled master makes them inactive; the sampled race motors confirm lower-quality=false and interval=1. The capture is therefore a valid baseline for those optimizations, not a new test of their effectiveness.

The saved diagnostic capture duration is **80 seconds**. Race records contain six full windows, two partial windows and a gap/rearm. Time trial has eight approximately ten-second windows. The primary comparison uses the first six full windows in each mode, approximately 60 seconds each. The two partial race windows are excluded. The all-full-window time-trial result is 100.92 FPS over 79.96 seconds, consistent with the matched subset.

Raw capture and a reproducible numerical summary are retained in ignored `local/performance-capture-2026-10-09`. The calculation script is `local/analyze-capture-20261009.py`. Startup/capture logs confirm the plugin version, successful samples, and final timer removal; no diagnostic suspension is logged. The game was closed during analysis. No game settings or runtime files were changed for this analysis.

| Metric, matched first six windows | Race against 7 CPUs | Time trial |
|---|---:|---:|
| Weighted average FPS | **84.59** | **100.70** |
| Average frame interval | **11.821 ms** | **9.930 ms** |
| Captured frames | 5,072 | 6,040 |
| Window p95 upper bounds | 12.75–15.00 ms | 11.00–11.50 ms |
| Longest observed frame | 45.34 ms | 20.13 ms |
| Fixed timestep | 0.008329996 s, about 120 Hz | 0.004999993 s, about 200 Hz |
| Draw-distance index | 0, Default | 0, Default |
| GC mode at all reports | Disabled | Disabled |

Race FPS is about **16% lower** in these matched windows. The measured mean frame difference is **1.891 ms**. This capture does not reproduce the previously reported 68/130 FPS split; it cannot disprove that observation or be treated as a stock-game FPS benchmark. Instrumentation, active time-trial objects, race segment, graphics, camera, frame pacing and other conditions can differ. Resolution, graphics preset and GPU utilization were not recorded by this probe.

The race has eight controller invocations per KCC fixed-update invocation, and seven AI-logic updates per rendered frame. Time trial has two controller invocations per KCC fixed-update invocation and no AI-logic dispatches. The two time-trial controllers are consistent with an additional replay/ghost controller, but this capture does not record their types, so that identity remains unconfirmed. Mode-specific timestep differences make a simple player-count scaling assumption invalid.

## Timed methods

Inclusive wall milliseconds per captured rendered frame, averaged across the matched windows:

| Native target | Race | Time trial |
|---|---:|---:|
| KinematicCharacterSystem.FixedUpdate | 0.5531 | 0.2625 |
| PixelEasyCharMoveKartController.FixedUpdate | 0.3185 | 0.1361 |
| ↳ UpdatePlayerPlayerCollision, included in controller above | 0.0875 | 0.0092 |
| AILogicController.Update | 0.0406 | 0, no dispatch |
| AIWeaponLogicController.Update | 0.0117 | 0, no dispatch |
| AIDistToTargetBehavController.Update | 0.0205 | 0, no dispatch |
| PlayerBezierFollower.Update | 0.0143 | 0.0049 |
| PixelKartPhysics.Update | 0.0086 | 0.0077 |
| PixelEasyCharMoveKartController.Update | 0.0105 | 0.0091 |
| PTK_GraphicsDetailApplier.LateUpdate | 0.0485 | 0.0393 |

Kart-to-kart scan cost is roughly **0.0875 ms per race frame**; it is not remotely large enough in this run to explain a multi-millisecond slowdown by itself. The three AI decision/weapon/target update timers total about **0.073 ms per race frame**. KCC is the largest measured span, but at **0.553 ms** still represents only a small part of an **11.821 ms** frame interval. Lower AI cadence can change only parts of that work and will not eliminate it all.

Summing all ten timers, including the known nested collision span, yields **1.114 ms/frame** for race and **0.469 ms/frame** for time trial. Excluding that collision double count gives 1.026 and 0.460 ms/frame. These sums are illustrative upper bounds for the measured spans; they are not a complete or exclusive CPU accounting. Other nesting/patch ordering may exist. The difference in the measured outer spans is about **0.566 ms**, much smaller than the complete **1.891 ms** frame difference. No specific unmeasured function has been proved responsible for the remainder.

The timers include synchronous original-method work and some hook costs. They omit work before/after the timer hooks, other game methods, animation/skinning jobs, general Unity/PhysX processing, render submission, GPU work, audio and engine/driver waits. Both modes averaged about 2.9 logical CPU cores of total process CPU use; that aggregate does not identify the critical thread or establish a CPU/GPU limit. Earlier GPU/thread samples were from another time and must not be presented as synchronized with this run.

## Memory observations

GiB below means bytes divided by 1,073,741,824. Counters overlap and must not be added.

| Metric, matched windows | Race | Time trial |
|---|---:|---:|
| Resident process working set | **4.432–4.485 GiB** | **4.546–4.547 GiB** |
| Private committed process pages | **9.537–9.556 GiB** | **9.095–9.099 GiB** |
| Unity allocated memory | 2.613–2.616 GiB | 2.538–2.539 GiB |
| Unity reserved memory | 3.087 GiB | 3.127 GiB |
| Game managed used bytes, including uncollected objects | 252.17 → 374.35 MiB | 294.53 → 430.33 MiB |
| Game managed heap size | 497.19 MiB | 572.38 MiB |
| Toolkit .NET used-object sample range | 9.36–39.69 MiB | 13.33–17.38 MiB |

The original used-byte readings are 264,417,280 → 392,536,064 for race, and 308,834,304 → 451,231,744 for the matched time-trial subset. These are snapshot values at the ends of windows, so their growth interval is about 50 seconds, not the full 60-second capture.

Across the first contiguous race segment, used bytes rise from 264,417,280 to 407,142,400 over 55.877 seconds: **2.436 MiB/s**, about 146 MiB/minute. Across the full time-trial segment, they rise from 308,834,304 to 507,760,640 over 70.005 seconds: **2.710 MiB/s**, about 163 MiB/minute. GC is Disabled at all snapshots. Allocations therefore accumulate in both modes; CPU opponents are not necessary for this effect. Allocation owners are unmeasured.

The managed heap size and Unity allocator totals stay stable within each segment, as do private process pages. Growing used bytes can occupy existing heap space without raising process commitment immediately. Used managed bytes drop between modes while heap capacity rises; the capture contains no transition sample, so it does not establish the exact cleanup event or prove complete collection.

The ~9.5 GiB private figure is committed private memory, not an observation that all 9.5 GiB is physically resident. The resident working set is ~4.4–4.5 GiB. The entire process footprint cannot be attributed to the game's ~0.5 GiB managed heap. Toolkit .NET used-object readings are small, but its total CLR heap commitment/native runtime ownership were not measured. Other native allocation/reservation, assets and graphics-driver ownership also remain to be measured. Current texture memory was **not** sampled; the earlier September texture inventory remains historical evidence only. One session and two modes do not establish a persistent leak.

## Why the first changes were ineffective here

- **Draw-distance caching:** Default was active. The original Default branch already avoids the repeated Medium/Near array path. The cache had little work to eliminate under these settings.
- **AI cadence:** Even the complete measured KCC span is only ~0.55 ms/frame; selected phase reduction cannot recover the previously reported ~7 ms/frame gap by itself. The current baseline proves normal motor flags, not activation of the earlier interval-4 trial.
- **Automatic race GC:** It can reclaim eligible managed objects and curb the measured accumulation. It cannot release still-referenced assets or all native/driver allocation reserves. Because the used objects fit inside existing heap capacity during this run, a large immediate RAM drop was not assured. This baseline does not quantify the control's enabled effect or hitch tradeoff.

## Developer next steps

1. Capture the native player loop with per-thread CPU attribution and synchronized GPU frame time. Separate simulation, render submission, animation/skinning, particles and synchronization waits. This is the missing evidence needed to choose the next FPS fix.
2. Include Ant_Player.Update, PixelKartAIInput.FixedUpdate/ProcessRacingInput, player visual Reconcile, boost/player presentation workers and main Unity physics processing. A small decision-loop timer does not cover all opponent-related work. Existing native visual handling changes animators, wheels and particles; its relevance is a candidate to measure, not an established bottleneck.
3. Record frame cap/VSync, resolution, preset, opponent visuals, rendered cameras and time-trial replay/ghost state alongside timings. Run a matched loader/plugin-free comparison to quantify toolkit detour/interop overhead, which remains present when feature toggles are off.
4. Attribute the ~2.4–2.7 MiB/s managed growth to allocating call stacks. Reduce those sources or evaluate incremental GC under a measured budget. Avoid forcing full collection during each gameplay frame.
5. Take a current texture/mesh/audio and native/graphics allocation census at menu, race, time trial and return-to-menu. Track retained references and allocation pools across repeated transitions before calling the footprint a leak or changing asset release behavior.

Status: the paired capture and these measurements are complete. The dominant FPS subsystem and owners of most committed memory remain unresolved. No additional gameplay optimization has been installed on the basis of this analysis.
