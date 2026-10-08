# Validation and remaining acceptance work

Verified on this workspace, 2026-10-08:

| Check | Result |
|---|---|
| Metadata catalog | 23,785 types; source hash recorded; namespaces, generics and byte spans indexed |
| Ghidra read-only export | 184,138-function index; stored executable hash matches installed binary; 56 pseudocode files |
| Targeted Ghidra pass | 26 bodies exported, zero decompiler errors/timeouts reported |
| Managed recovery | MKsKartersMods 1.6.0 recovered to 37 local C# files, including metadata attributes/helpers |
| Legacy target audit | 93 C# files scanned, 81 named Harmony declarations: 79 present by name, one overload review, one absent method |
| Customization plugin build | .NET 6 library against current local runtime/interop, zero warnings/errors |
| New mod template build | Fresh SmokeMod project built with zero warnings/errors |
| Python core tests | 20 passing tests including config preservation/typed controls, containment, lifecycle checks, artifact integrity and deploy/rollback |
| GUI smoke | Seven tabs laid out off-screen at 1168 × 651; declarations/pseudocode/native overloads loaded; 90 MK config controls edited in memory; external source changes preserved |
| Selected native overload | Second Hit overload exported by exact address `0x18056dae0`; one export, zero errors; binary hash matched |
| Python compilation | studio, tools and launch.py pass compileall |

Source and signature audit are not runtime compatibility certification. The Ghidra error count measures successful decompiler completion, not correctness of every inferred type.

Still unverified: actual starter plugin loading in this game's process, Unity component registration and callbacks, HUD canvas selection and scaler behavior, Wwise hook effectiveness, FOV behavior for all cameras, fast-fall behavior, online/offline transitions, and complete leaderboard upload coverage. The original installed game files and old mods have not been replaced by this investigation. Build artifacts have `runtimeTested=false`.

The visible GUI still needs normal user interaction testing and a visual design pass. IntelliSense, a debugger, dependency resolution, version diff UI, live scene inspection, recipe generation and packaged distribution remain roadmap milestones.
