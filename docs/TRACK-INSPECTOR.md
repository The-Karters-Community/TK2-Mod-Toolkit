# Track Inspector

Enable **Track inspector** in Mod Toolkit during an offline race or time trial. Press **F10** to show or hide the game's own trigger-collision meshes. The key is configurable. The Toolkit only sets the game's existing debug flag and updates the already-registered trigger mesh renderers when the setting changes; it does no per-frame track scan, ray sampling, mesh generation, or custom drawing.

This exposes the physics-trigger meshes the game authored for debugging, including trigger-based kill zones. It does not visualize every ordinary kart wall or arbitrary collider. It leaves collision and trigger behavior unchanged.

## Current-build evidence

Read-only Ghidra analysis matches the installed `GameAssembly.dll` SHA-256 `e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130`.

| Native method | Address | Observed behavior |
| --- | --- | --- |
| `Ant_MapData.InitMapDataFrom_ModTrack(PTK_ModTrack)` | `0x1805D18E0` | Copies `PTK_ModTrack.bForceDebugShowTriggerCollisionMeshes` to the active map. |
| `PTK_ModTKLogic_PhysicsTrigger.Init()` | `0x1805E9CE0` | If the active map's flag is true, enables the collider object's existing `MeshRenderer`. |
| `PTK_ModTKLogic_PhysicsTrigger.Start()` | `0x1805EAB40` | Applies the same debug-visibility check during trigger startup. |

The Toolkit uses that flag and renderer path directly. Existing Ghidra pseudocode is analysis output, not recovered original C#. In-game appearance and frame-time impact still require a manual check after installing the updated plugin; the game is not launched automatically.
