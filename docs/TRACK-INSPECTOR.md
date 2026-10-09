# Track Inspector

Enable **Track Inspector** for a local offline race or time trial. Press **F10** to show or hide the game's existing physics-trigger meshes. The key is configurable. The Toolkit sets the native debug flag before map setup, then updates the trigger renderers after the game's setup completes. Mid-race scans happen only when the visibility setting changes.

This uses the game's `bForceDebugShowTriggerCollisionMeshes` path. It only exposes trigger meshes that the loaded track provides; it does not visualize every ordinary kart wall or arbitrary collider. It leaves collision and trigger behavior unchanged.

## Current-build evidence

Read-only Ghidra analysis matches the installed `GameAssembly.dll` SHA-256 `e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130`.

| Native method | Address | Observed behavior |
| --- | --- | --- |
| `Ant_MapData.InitMapDataFrom_ModTrack(PTK_ModTrack)` | `0x1805D18E0` | Copies `PTK_ModTrack.bForceDebugShowTriggerCollisionMeshes` to the active map. |
| `Ant_MapData.Start()` | `0x1805D4080` | Searches for `PTK_ModPhysicsCollisionTriggerType` with inactive objects included, creates or gets each `PTK_ModTKLogic_PhysicsTrigger`, initializes it, and collects its trigger colliders. |
| `PTK_ModTKLogic_PhysicsTrigger.Init()` | `0x1805E9CE0` | If the active map's flag is true, enables the collider object's existing `MeshRenderer`. |
| `PTK_ModTKLogic_PhysicsTrigger.Start()` | `0x1805EAB40` | Applies the same debug-visibility check during trigger startup. |

The previous implementation searched only active `PTK_ModTKLogic_PhysicsTrigger` objects and could scan before `Ant_MapData.Start()` had finished creating them, producing zero triggers. The Toolkit now sets the flag in the map-start prefix, applies visibility after native map setup, and includes inactive trigger components when toggling mid-race. Existing Ghidra pseudocode is analysis output, not recovered original C#. In-game appearance still needs a manual check; the game is not launched automatically.
