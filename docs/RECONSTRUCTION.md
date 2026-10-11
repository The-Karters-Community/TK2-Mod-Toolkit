# From native logic to readable C#

Generated DummyDll declarations and the local source-map/pseudocode workflow are documented in [IL2CPP exports](IL2CPP-EXPORTS.md). Those artifacts make more of the game inspectable, but they remain distinct from the selected, evidence-reviewed C# translations maintained by this repository.

## What the first export establishes

Ghidra reports 184,138 functions, of which 155,180 do not have default `FUN_` names. A name is not necessarily an original game-specific method: runtime/library symbols and imported/generated labels are included. The maintainer's exact-build local export now contains 14,169 Assembly-CSharp-class-name pseudocode files plus one function timeout; the run used a broader, unanchored matcher, so some substring matches may be included. `local/ghidra/assembly-csharp-full/export-report.json` records the scope, limitation, and timeout. The complete function index is `local/ghidra/functions.jsonl`. The app's pseudocode search can inspect the local export; no game-derived files are committed or distributed. Exported pseudocode has not all been semantically reviewed.

Metadata catalog: 23,785 types across game and dependency assemblies. Assembly-CSharp's image starts at type index 0; the next image starts at 4,753. Imported metadata also includes networking, Wwise, UI, Rewired, platform APIs and framework libraries. Treat the index as a browser, not a list of 23,785 gameplay classes.

`GameAssembly.dll` SHA-256:

`e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130`

The Ghidra program's stored hash agrees with the current installed file. Its image base is `0x180000000`, x86 little-endian 64-bit. The metadata dump hash is `5a099f06192d449a5f53914be85957a4956c26785590e3bc5e14e1fc9f298109`.

## A readable reconstruction example

At virtual address `0x1805dd8a0` (RVA `0x5dd8a0`), `PixelKartPhysics.AddVelocity(Vector3)` reads the existing `vFrameVelocityAddOverride` x/y/z components and adds the input vector component-wise. The equivalent readable operation is:

```csharp
// Handwritten semantic reconstruction from the matching native method.
// Not the original developer source, and not a replacement for the game class.
internal void AddVelocity(Vector3 velocity)
{
    vFrameVelocityAddOverride += velocity;
}
```

This initial example has now become actual compiled, editable code in `src/Reconstructed/KartLogic.cs` and `plugins/TK2.Customization/ReadableGame.cs`. The pack's fast-fall path calls that reconstructed accumulator when gameplay validation is eventually opened. The repository now has 34 reviewed normal-state semantic translations and one behavior slice; see [readable source](READABLE-SOURCE.md) for the method list, per-method evidence hashes, executable tests and limits. The game's physics implementation as a whole is not replaced.

The current `JumpInput(bool)` at `0x1805e0270` is more involved: it ignores false input, handles extra jump effects, resets the pre-ground input history, considers replay grounding state, buffers airborne clicks, and supports a grace interval after leaving ground. This evidence makes a direct rename of the old jump hook unsafe without testing its event timing.

The first `Ant_BoostManager` slice now has nineteen C# translations for boost-source predicates, pad triggers, boost reset, fill-time setting, manual boost timing, the AI-only physical-boost gate, braking state, reserve floors and collision transitions. The typed adapters expose reserve addition, the race-state-gated reserve floor and `GetCurrentBoostMaxSpeedIncrease`, which sums four current decoded bonuses and conditionally adds configured Booster, Star and boost-pad bonuses. `GetCurrentBoostAccelMultiplier` applies the configured multiplier while a boost-pad timer is positive and projected speed is strictly below its configured minimum; otherwise it returns `1.0f`. `WhatZoneIsThePlayerIn` classifies the active boost using the three fill slots, other completed slots, and thresholds at `0.55`, `(0.94, 1.05]`, plus the one-full/two-low special case. Those four float constants were read from the matching binary and are recorded with the native pseudocode hash. The acceleration adapter's velocity comes from the controller motor's base velocity; its called getter has its own provenance hash. The native forward vector is expected to be normalized. `IsBoosting` checks reserves, the just-triggered flag, boost-pad time and Booster time, while the Star timer has a separate predicate but is absent from that aggregate. Wall collisions reset loaded lateral velocity, reduce positive reserves by 30% unless the tank weapon is active, and invoke the collision event when subscribed. The boost-pad trigger applies the native strict cooldown boundary, updates the timestamp on suppressed calls, and delegates reset/fire side effects to the game's own methods. `StopBoost` clears the reserve, triple-boost bonus, three fill slots, air time and booster count. `SetWheelBurningBoostFillTime` writes one selected boost slot directly. Diagnostic log calls are omitted. Pure tests cover reserve arithmetic/floors, zone-combo branches, slider timing thresholds, pad cooldown boundaries, boost reset state, selected fill slots and every valid `EBoostType` under the AI gate. `BoostInput` and `FireSliderBoost` share native helper `FUN_18039d450`. Its normalized-fill power curve is recorded as a separate behavior slice; the rest of `FireSliderBoost`, including reserve, physics, event and fill-slot side effects, remains to be reconstructed. Larger timer/update state machines still need focused reconstruction and call-site review.

## Why the C-like export needs review

Native initialization boilerplate, marshaling, hidden MethodInfo arguments, register passing, indirect calls and shared code can obscure behavior. The current `PTK_AudioListenerManager.SetVolume` decompilation reaches a dynamically resolved native Wwise call but does not correctly display all arguments. It would be inaccurate to invent the missing argument flow from that listing alone. Metadata verifies its `(string,int)` signature and the managed bridge allows a typed hook.

`Ant_MainGame.GetGameModeRequiredLapCount` contains game-mode-specific branches and ObscuredInt/ObscuredBool conversions. Some shared getter/call targets have misleading names/types in the native listing (for example framework class names where game configuration values are expected). Review aliases against dump signatures and call sites before assigning semantics to those labels. Changing one generic method label does not recover its original managed specialization.

## Reconstruction workflow for the full project

1. Fingerprint the exact binary/metadata; preserve native analysis read-only.
2. Identify a type and exact overload in metadata, find all native addresses, and record aliases/shared implementations.
3. Export the native body, strings, callers and callees needed for that feature. Prioritize inputs, outputs, branch behavior and state mutation over cosmetic removal of compiler boilerplate.
4. Write a small C# semantic reconstruction with provenance and confidence; keep unknowns explicit. Do not fabricate missing bodies.
5. Validate pure logic with representative tests and native/runtime observations. Keep lifecycle/Unity assumptions separate from pure arithmetic.
6. Wrap verified operations in the SDK adapter, then expose them through the GUI and recipes. A mod author should normally use readable adapters and generated C#, with Ghidra reserved for maintaining those adapters after updates.

Recovering complete original source, a Unity editor project, and assets are distinct tasks. Asset reconstruction needs serialized assets/bundles/import settings; native code analysis alone cannot provide those. The game already exposes `PTK_ModsContentLoader`, `PTK_ModsGameContentConfigsCreator`, `PTK_ModGameplayDataSync` and trigger/command types. Investigate and reuse this content pipeline before inventing a parallel content format.
