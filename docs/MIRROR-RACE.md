# Physical Mirror Race

Enable **Mirror Race**, save, then leave and load an offline track again. The module has one switch. Enabling or disabling it during a race applies to the next track load; changing a live track would invalidate the game's race-progress and AI caches.

This module reflects authored track geometry and collision geometry across world X, including start positions, checkpoints and Bezier paths. It clones meshes, reflects vertices/normals/tangents, reverses triangle winding and preserves UVs/material assignments. It reflects positions and proper rotations, rather than introducing negative scale. The minimap image and world bounds are reflected together. Ordinary steering, camera projection, culling and HUD rendering remain in use.

The entire scene is inspected and mesh/minimap copies are prepared before changing scene objects. Unsupported geometry refuses the operation and reports a reason in the plugin log. An apply failure attempts to restore original poses, mesh references, collider centers and renderer state. In-memory copies are released when the original track unloads; original game assets are never overwritten.

## Supported path and limits

- Authored offline map scenes with triangle meshes, primitive/mesh colliders and the game's `BezierSolution` path points. Native map initialization builds progress/checkpoint caches after reflection.
- CPU-readable meshes, or GPU vertex/index buffers whose position format is Float32/Float16 and normal/tangent formats are Float32/Float16/SNorm8/SNorm16. UV/color data stays unchanged. GPU readback happens only while loading, before scene edits. **Unity's synchronous `WaitForCompletion` cannot be cancelled or given a timeout; a faulty GPU driver may stall loading.** Missing APIs/readback errors refuse that track.
- GPU-instanced prefab scenes can use ordinary mesh renderers/LODs when every runtime instance has a registered source GameObject in the same scene. Only sources actually instanced by active managers are re-enabled. This preserves geometry but can reduce frame rate. The native renderer helper is deliberately avoided because it can add rigidbodies.
- Non-readable minimaps are copied through a temporary RenderTexture; the previous active target is restored in `finally`.
- Allocation limits: 128 MiB of unique cloned mesh data, 64 MiB per GPU buffer, 30,000 transforms, two million vertices per mesh, 256 submeshes and 4096-pixel minimaps.

The reflector refuses custom/generated tracks, static batches, terrain, skinned/animated track geometry, dynamic track rigidbodies, directional baked lighting, overlapping submesh ranges, GPU terrain/detail/tree managers, matrix-only GPU instances without source objects, maps whose progress cache already exists, and gameplay collisions/paths inside protected camera or UI hierarchies. These need a dedicated mirrored asset or authoring pipeline. Custom shaders with world-coordinate effects, baked reflection/light-probe behavior and third-party scripts remain visual compatibility limits; this is not a claim of universal track support. No stock map has been runtime-certified in this change because the game was not launched automatically.

## Native evidence

Read-only export of the supplied `TK2_1_4_18` Ghidra project matches current `GameAssembly.dll` SHA-256 `e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130`.

| Native method | Address | Observed implementation |
| --- | --- | --- |
| `Ant_MapData.Awake()` | `0x1805cf0a0` | Assigns map instance; initializes roads, checkpoint associations, `PTK_RaceProgressTopology.Build` and minimap originals. This is the exact parameterless prefix hook. |
| `Ant_MapData.CreateBezierPath(PTK_ModTrack, List<PTK_ModPathPoint>, bool, int, int)` | `0x1805d00a0` | Copies authored world position/rotation to new AI path points, then refreshes and constructs spline caches. Custom tracks therefore require a separate earlier hook. |
| `Ant_MapData.CreateRacePosCalcPathsFromModTrackData(...)` | `0x1805d0540` | Independently clones authored world positions/rotations into race-calculation paths. |
| `PTK_RacePositionCalc_Point.InitPoint()` | `0x1805eeb60` | Caches transform position/scale, reduces orientation to yaw, then caches rotation. |
| `GPUInstancerManager.OnDisable()` | `0x18201f770` | Removes the manager from the active list, disables LOD fade and calls its virtual clear method. |
| `GPUInstancerPrefabManager.ClearInstancingData()` | `0x182055db0` | Calls base clear and optionally restores registered prefab renderers. |
| `GPUInstancerPrefabManager.SetRenderersEnabled(GPUInstancerPrefab,bool)` | `0x18205a780` | Changes MeshRenderer/LOD state and can add a Rigidbody when restoring sources. |

Generated native bodies remain ignored under `local/mirror-evidence`, `local/mirror-camera-evidence` and `local/mirror-gpu-evidence`. A bounded search of the dump and 184,138-function native inventory found no native race-mirror flag; water reflections and generic/UI mirror utilities are unrelated.

## Validation

`dotnet run --project tests/MirrorRace` tests actual byte helper code and the rotation identity `R' = Sx R Sx`: 3021 assertions cover random orientations, channel preservation, signed/half formats, winding, neighbor ranges and malformed inputs. Full current-interop compilation passes. Actual track rendering, collision, AI completion and GPU recovery still require deliberate in-game validation.
