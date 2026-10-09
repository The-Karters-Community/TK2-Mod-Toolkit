# Engine and GPU capture, runtime 0.6.11

The next capture adds Unity frame, render, physics, animation, skinning, wait and draw-count metrics to the existing ten game-method timers. This is observation, not an FPS fix. Use the same track, resolution and graphics settings for a CPU race and time trial, with **Race performance** and **Track boundaries** off. Enable **Performance diagnostics** before entering each mode and drive through its bounded capture. The JSONL uses schema 2 and includes resolution, VSync, frame cap, graphics API/device, Unity version and whether the boundary overlay was visible. `trackBoundaryViewObservedDuringWindow` records visibility observed on any diagnostic tick, including mid-window toggles; reject such windows from an ordinary performance comparison.

## Current binary evidence

The installed `UnityEngine.CoreModule.dll` lacks the main `ProfilerRecorder`, handle, description and sample types. It restores `ProfilerRecorderOptions` and a partial `FrameTimingManager`, whose usable public `FrameTiming` data type is missing. The current game dump alone therefore cannot establish availability of these APIs.

The current `UnityPlayer.dll` still registers the native profiler internal calls. SHA-256: `5da2e6c1050924f370b8143256a1705ac3758bb78eb07013a268058edeb6709a`. The bridge checks this exact file hash once before binding any call and disables engine telemetry if it changes. It resolves call names through the installed `Il2CppInterop.Runtime.IL2CPP.il2cpp_resolve_icall(string): IntPtr`; it uses no hardcoded function address at runtime.

The native registry's parallel function/name arrays were inspected and the corresponding Win64 instructions were checked with Capstone and Ghidra in a **new ignored scratch project**. The original game binaries and existing GameAssembly database remain unchanged. Local evidence: `local/engine-native-abi.json`, `local/engine-ghidra-evidence/`, `local/engine-ghidra-export.log`. Pseudocode parameter types are inferred; the registers, structure loads and writes are the ABI evidence.

| Registered name suffix | Current preferred image address | Observed ABI |
| --- | --- | --- |
| `ProfilerRecorderHandle::GetByName_Unsafe_Injected` | `0x18005da80` | category pointer, UTF-16 pointer, length, out 8-byte handle |
| `ProfilerRecorderHandle::GetDescriptionInternal_Injected` | `0x18005dab0` | handle pointer, out 24-byte description |
| `ProfilerRecorder::Create_Injected` | `0x18005cf80` | handle pointer, capacity, options, out 8-byte recorder |
| `ProfilerRecorder::CopyTo_Pointer_Injected` | `0x18005d530` | recorder pointer, sample buffer pointer, capacity, one-byte reset; returns count |
| `ProfilerRecorder::Control_Injected` | `0x18005cff0` | recorder pointer, control option; release is 4 |
| `ProfilerRecorder::GetValid_Injected` | `0x18005d1f0` | recorder pointer; returns one-byte validity |

All six use the Win64 ABI; delegates are cached. Official Unity 6000.0 source specifies an 8-byte handle/recorder, 24-byte sample (value, count, reference value), 24-byte description with category at offset 0, flags 2, data type 4, unit 5, name length 12 and name pointer 16. Built-in category `Any` is 65535. Recording options are StartImmediately (1), WrapAroundWhenCapacityReached (8), SumAllSamplesInFrame (16). These source declarations were crosschecked against the installed native instructions; they are not recovered game C#.

Primary sources: [Unity 6000.0 recorder bindings and layouts](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.0/Runtime/Profiler/ScriptBindings/ProfilerRecorder.bindings.cs), [category constants](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.0/Runtime/Profiler/ScriptBindings/ProfilerUnsafeUtility.bindings.cs), [unit definitions](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.0/Runtime/Profiler/ScriptBindings/ProfilerMarker.bindings.cs), [release Player frame timing behavior](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/FrameTimingManager.html), [sample-buffer reset behavior](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.CopyTo.html).

## Availability and interpretation

The bridge looks up 20 exact names, including `CPU Total Frame Time`, `CPU Main Thread Frame Time`, `CPU Render Thread Frame Time`, `GPU Frame Time`, `Physics.Simulate`, animation/skinning candidates, graphics waits, draw calls, batches, SetPass calls, triangles and vertices. Registered strings are evidence that a native path exists; **runtime lookup and nonempty capture determine whether a metric actually works**. Release builds may omit markers. Each metric records its status, numeric native category/data type/unit, valid/invalid/zero sample counts, averages and maxima. Unsupported data types and units remain unavailable.

Only signed Int64 samples with nanosecond, byte or count units are interpreted. Nanoseconds become milliseconds; bytes and counts retain their original unit. A missing average is `null`. Zero CPU/GPU frame timings are rejected as unavailable measurements. Zero samples for other markers or counters are retained and counted explicitly.

The fixed 8-sample native buffer is drained with `CopyTo(reset:true)`, so a stale GPU result cannot be counted repeatedly. Samples can arrive four frames late and are not paired with a specific Harmony-method frame. Full-buffer counts indicate potential sample loss. Native timers overlap and may include waits; summing them does not establish total CPU work. Missing physics/animation markers do not prove these systems are cheap. GPU timing does not attribute individual shaders, cameras or assets.

Recorders start only after the existing warmup, during bounded offline capture. They are released on completion, live disable, race/menu/focus/online transition, unload, read failure and partial initialization failure. The polling loop allocates no managed arrays, strings or reflection arguments per frame; report serialization still allocates every reporting window. The recorder itself and existing Harmony timers add observation overhead.

## Validation

The installed-reference plugin build passes with zero warnings/errors. The performance harness passes 113 assertions, including consumed samples, nanosecond conversion, count units, valid zero physics work versus invalid zero GPU timings, missing markers, empty-window nulls, read-failure cleanup, idempotent disposal and partial-initialization cleanup. Stub timings are not game measurements. The native bridge has not yet been exercised in a new in-game capture, and no performance improvement is claimed.

After the next paired capture, inspect `engine.status` and each metric's `status` before drawing conclusions. Compare CPU/GPU frame means and graphics wait markers to the FPS gap; compare draw/triangle counts to opponent count. If relevant release markers are unavailable, the developer needs a profiling-enabled build or a native GPU/CPU trace to attribute that part of the frame.
