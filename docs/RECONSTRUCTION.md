# From native logic to readable C#

## What the first export establishes

Ghidra reports 184,138 functions, of which 155,180 do not have default `FUN_` names. A name is not necessarily an original game-specific method: runtime/library symbols and imported/generated labels are included. There are 56 local pseudocode files after a broad camera sample and a targeted gameplay/audio export. All 26 functions selected in the targeted export completed without decompiler errors. We have not decompiled/reviewed the bodies of every function.

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

This explains why velocity accumulation is a candidate mod seam. The starter uses the controller's existing `AddVelocity` API, following the old fast-fall mod; it does not replace the game's physics implementation.

The current `JumpInput(bool)` at `0x1805e0270` is more involved: it ignores false input, handles extra jump effects, resets the pre-ground input history, considers replay grounding state, buffers airborne clicks, and supports a grace interval after leaving ground. This evidence makes a direct rename of the old jump hook unsafe without testing its event timing.

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
