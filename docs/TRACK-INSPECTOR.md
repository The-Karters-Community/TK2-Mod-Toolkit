# Track Inspector

Enable **Track Inspector** in Mod packs, save, and enter a local race. **F10** hides or shows outlines. Change its hotkey, category checkboxes, distance, opacity and labels live through the Toolkit. The module never changes collisions or respawn behavior.

| Outline | Detection |
| --- | --- |
| Cyan: wall | The local kart's `wallLayerToCollDetect` layer mask; includes invisible physical walls |
| Orange: respawn wall | `wallRespawn_Always_ColliderLayer` |
| Yellow: conditional respawn | `wallRespawn_Flat_ColliderLayer`; native tilt conditions still apply |
| Red: kill-linked trigger | An enabled trigger executor links this trigger to an enabled `KillPlayer.bExecute` command |
| Purple: other trigger | Optional game logic trigger; **not** assumed lethal |

Box, sphere and capsule outlines use collider geometry and Unity's scale rules. Readable non-convex collision meshes use unique triangle edges (maximum 1,800 edges per collider). Convex meshes, unreadable meshes and other collider types show explicitly labelled **bounds**, which are an approximation; the input mesh is not mistaken for Unity's cooked convex hull. At most 96 nearby outlines and 24 labelled colliders are drawn; lowering Draw distance reduces clutter. Labels are screen annotations; **Show through track** changes only outline depth testing.

Disable the module or leave the race to release its own meshes, materials and label object. The source colliders, layers, transforms, materials, triggers and kill conditions remain unchanged. No kill/respawn callbacks are suppressed. It stays hidden in online games and menus.

## Current-build evidence

Ghidra's saved project and installed `GameAssembly.dll` both identify SHA-256 `e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130`. Read-only headless export used `-readOnly -noanalysis`; exported pseudocode is analysis evidence, not original C#.

| Native method | Address | Observed behavior |
| --- | --- | --- |
| `PixelEasyCharMoveKartController.ICharacterController.OnMovementHit` | `0x1805DA290` | Checks the hit object's layer against the actual wall and respawn masks; always-respawn contacts call the validator; flat-respawn contacts have an additional angle predicate |
| `PTK_VehicleOutsideMapValidator.VehicleCollidedWithOutsideMapResetCollider(GameObject)` | `0x1805CCD70` | Starts native respawn processing after player/tutorial/transition guards |
| `PTK_TriggerArrayCommandsExecutor.Start()` | `0x180A688A0` | Expands configured command parents into command behaviours and trigger parents into actual trigger components; retains explicit trigger/behaviour lists |
| `PTK_PlayerLogicModCommands.ExecuteEffect_KillPlayer(...)` | `0x1805BEE80` | Applies current-HP damage after `CanExecute` |
| `PTK_PlayerLogicModCommands.CanExecute(...)` | `0x1805BE200` | Contains immunity and other game conditions; a linked kill command is therefore a potential hazard |

Local exports: `local/ghidra-walls/pseudocode` and `local/ghidra-walls-graph/pseudocode`. Current interop metadata confirms the accessed layer-mask and trigger-executor properties. The module reads the executor's initialized lists instead of guessing associations from names or proximity.

## Limits and verification

This identifies physical walls and the native layer-based respawn walls, plus kill-linked **physics** triggers. Collider scope follows the active `Ant_MapData` track scene and its registered trigger-collider scenes, independently of the local player's additive gameplay scene. Labels use the local player's `gameplayCamera.GetCamera().unityCamera`. Scripted hazards without such a collider/trigger link, movement-dependent falls, procedural conditions and disabled volumes are outside this overlay's coverage. It cannot promise that every place where a kart can die is a visible volume. Scene geometry is rescanned every four seconds; rapidly changed trigger associations can temporarily retain their previous category.

The standalone rules test covers signed layer masks, category precedence, non-lethal generic triggers, malformed triangle indices, shared-edge deduplication and edge budgets. The plugin is compiled against the current installed IL2CPP interop assemblies. Rendering, material availability and real-track coverage still require an in-game check; compilation does not establish those observations. No automatic game launch is part of verification.

```powershell
dotnet run --project tests/TrackInspector/TrackInspectorTests.csproj
```
