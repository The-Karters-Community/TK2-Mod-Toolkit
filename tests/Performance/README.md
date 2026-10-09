# Performance and diagnostic regression harness

Run from the repository root:

```powershell
dotnet run --project tests/Performance/Performance.csproj -c Release
```

The project links the actual `plugins/TK2.Customization/PerformanceFeature.cs`. It invokes production hooks through reflection and supplies small controlled stubs for Unity, current-game methods, config and Harmony. It verifies observable branches, native buffer identity/reference release, graphics values, AI ownership/restoration, fail-open behavior, current native GC policy restoration and absence of a compiled call to `System.GC.Collect`.

`SimulateNativeAi` models the native controller assignments established by Ghidra; it allows testing restoration of changed motor fields without claiming the engine executed. The stubs model Unity destroyed-object null behavior for saved-controller pruning.

Setter-failure regressions acquire AI, camera and GC ownership before making a saved controller or motor setter throw. They verify that hooks contain the exception, later saved AI still restore, native GC policy is consulted and camera buffers are released. A direct `Restore` failure also checks its `finally` cleanup.

These checks do not establish runtime Harmony success, IL2CPP array marshaling/copy behavior, GPU/CPU performance gains, GC hitch severity, native fixed-step timing or in-game opponent movement. No game is launched or modified by this harness.

The project also links production PerformanceSamples.cs and PerformanceDiagnostics.cs. It checks percentile ranking/overflow, invalid frame rejection, actual callback aggregation, bounded capture/next-race rearming, online/unfocused/live-disable removal, actual motor-field sampling, JSON report output, partial patch cleanup and report-write failure containment. Temporary reports contain stub engine measurements and must not be mistaken for game evidence.
