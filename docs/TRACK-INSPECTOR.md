# Track Inspector

Enable **Track Inspector** for a local offline race or time trial. Press **F10** to show or hide nearby wall colliders; the key is configurable. The overlay uses the current kart's collision-layer masks, shows mesh, box, sphere, and capsule collider shapes, and colors wall and respawn layers differently. It draws at most 48 colliders within 150 m and refreshes its collider scan every two seconds. The overlay is hidden until F10 is pressed.

The game's `bForceDebugShowTriggerCollisionMeshes` path remains enabled for tracks that provide mod-trigger markers. That native path only exposes markers authored by the loaded track; it does not cover ordinary kart wall layers. The Toolkit overlay adds those wall and respawn collider layers without changing collision behavior. Unsupported collider shapes are skipped rather than replaced with misleading bounds.

## Current-build evidence

Read-only Ghidra analysis matches the installed `GameAssembly.dll` SHA-256 `e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130`.

| Native method | Address | Observed behavior |
| --- | --- | --- |
| `Ant_MapData.InitMapDataFrom_ModTrack(PTK_ModTrack)` | `0x1805D18E0` | Copies `PTK_ModTrack.bForceDebugShowTriggerCollisionMeshes` to the active map. |
| `Ant_MapData.Start()` | `0x1805D4080` | Searches for `PTK_ModPhysicsCollisionTriggerType` with inactive objects included, creates or gets each `PTK_ModTKLogic_PhysicsTrigger`, initializes it, and collects its trigger colliders. |
| `PTK_ModTKLogic_PhysicsTrigger.Init()` | `0x1805E9CE0` | If the active map's flag is true, enables the collider object's existing `MeshRenderer`. |
| `PTK_ModTKLogic_PhysicsTrigger.Start()` | `0x1805EAB40` | Applies the same debug-visibility check during trigger startup. |

The previous implementation searched only native mod-trigger components. Official track walls use the kart controller's wall and respawn collision layers instead, so the trigger query could return zero even when the track had death barriers. F10 now toggles a separate low-cost collider overlay, and the in-game panel/log reports whether the key was received and how many shapes were found. Ghidra pseudocode is analysis output, not recovered original C#. In-game appearance still needs a manual check; the game is not launched automatically.
