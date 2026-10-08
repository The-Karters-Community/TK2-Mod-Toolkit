# Editable reconstruction, version 0.1.4.18

The workshop now opens **C# with executable method bodies**, not dump declarations. `src/Reconstructed/KartLogic.cs` and `CameraLogic.cs` are compiled into the same pack DLL. `ReadableGame.cs` adapts the reconstructed logic to current game interop objects. Changing these files, then pressing Build & install, changes the pack behavior. It does not rewrite GameAssembly or automatically replace every original method.

| Native method | Virtual address | Readable implementation | Evidence / assumptions |
|---|---|---|---|
| PixelKartPhysics.AddVelocity | 0x1805dd8a0 | KartLogic.AddVelocity | Three component sums; used by fast fall when its validation gate is eventually opened |
| PixelKartPhysics.JumpInput(bool) | 0x1805e0270 | KartLogic.JumpInput and ReadableGame.JumpInput | False input, extra trick, history reset, replay grounding, airborne buffer, inclusive grace boundary; initialized objects and stable grounding assumed |
| PixelGameKartCamera.GetCamera | 0x1804ee5b0 | CameraLogic.GetCameraIndex and ReadableGame.GetCamera | Ghost returns null, normal/local-spectator player slot, nonlocal spectator slot zero |
| PixelGameKartCamera.IsCameraASpectatorCamera | 0x1804f05a0 | CameraLogic.IsSpectator | Boolean field read |
| PixelGameKartCamera.IsIntroCameraActiveAndRunning | 0x1804f05b0 | CameraLogic.IsIntroActive | Strict comparison against 0.95f; constant bytes `33 33 73 3f` at VA 0x183707e34 / file offset 0x3706634 |
| PixelGameKartCamera.EnableTribuneCamera | 0x1804ed620 | CameraLogic.EnableTribune | Boolean assignment |
| PixelGameKartCamera.ForceInstantTeleport | 0x1804ed730 | CameraLogic.ForceInstantTeleport | Set forced teleport, clear tribune camera |

`src/Reconstructed/provenance.json` records input hashes, addresses, source files, and the local native evidence. The C# console tests check branch combinations, boundary cases, state preservation, slot selection, NaN and velocity accumulation. These are semantic tests, not native runtime equivalence proof.

Native IL2CPP metadata initialization and error-helper assembly are omitted from the pure models. Null/error behavior is not fully reconstructed. The jump adapter snapshots grounding once; the native method queries it twice. A side effect between those native queries would require a more exact adapter. The adapter is opt-in and does not patch JumpInput automatically.

## Editing and authoring

1. Open Create a mod. Select readable game logic or create a named recipe.
2. Edit the C# in the app, or use an external editor on the same files.
3. Save C#, then Build pack. Build & install also saves an edited file first.
4. The DLL always remains `TK2.Customization.dll`. Recipe classes are discovered inside that assembly.
5. Start the game manually once to create recipe-specific settings. Enable/configure those recipes under My mods. Settings reload without rebuilding; C# changes require a rebuild and game restart.

Recipes implement `IModRecipe`: Configure, Tick, Restore, and a gameplay declaration. The starter recipe demonstrates a reversible camera FOV change. Keep the built-in FOV module off if that recipe controls the same camera. Source edits have local backups and reject an external edit conflict. This interface is intentionally a small C# editor; it does not yet provide IntelliSense or a debugger.

## What remains to reconstruct

Seven methods are reconstructed from the 56 exported bodies. There are 184,138 native function entries in the local index, including engine and library code. Fully recovering the original C# source, comments, variable names, stripped code, and original Unity editor project from optimized IL2CPP binaries is not achievable by simply translating all Ghidra listings.

Continue subsystem by subsystem: boost state machine, complete jump motion, input dispatch, damage overloads, race rules, replay/save states, UI controller lifecycle, and native mod-content formats. For each method, record exact signature and aliases, resolve constants/call targets, reconstruct state transitions, test pure behavior, then compare against runtime observations. Do not substitute generated wrappers or fabricated stubs for a completed reconstruction.
