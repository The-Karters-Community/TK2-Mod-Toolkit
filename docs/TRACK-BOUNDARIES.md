# Track boundaries

The **Track boundaries** module shows invisible-wall and respawn collider geometry during an offline race. Enable it, then press **F10** to hide/show the view. Cyan marks walls; orange marks unconditional and flat/angle-dependent respawn boundaries. Separate switches hide either category. The default range is 200 metres from a local kart, adjustable from 25 to 1,000 metres.

## Zero-count correction in 0.6.12

The player's 0.6.11 screenshot showed the module ON with zero walls and zero respawn meshes. That version required a `PTK_HideMeshRendererInPlayMode` component, a mesh renderer/filter, an original material, and a collider in a particular subtree. It also restricted scene membership. Those requirements were stronger than the game's collision rules and excluded valid boundaries.

The corrected module inventories active colliders across loaded scenes, including additive scenes. The local kart's actual three wall/respawn layer masks determine selection; names, tags and debug-hiding components are unnecessary. Respawn masks take precedence over wall effects. Player-owned colliders, disabled/inactive objects and unrelated collider layers are excluded. A wall whose enabled renderer already draws the same collision mesh is skipped as visible scenery. Flat respawn boundaries are displayed even when their angle condition would not currently cause a reset.

For a MeshCollider the view shares `MeshCollider.sharedMesh`, including when there is no renderer, filter or original material. It does not read vertex arrays from an unreadable native mesh. Box, sphere and capsule colliders get generated visual meshes from their collider dimensions and center. Round volumes account for nonuniform world scale and capsule direction. They are polygonal visualizations, not a reconstruction of PhysX's cooked collision surfaces; convex MeshColliders can also have a cooked hull different from their source mesh.

Native IL2CPP downcasts use `TryCast<T>`: a base Collider wrapper need not be a managed MeshCollider instance. Each collider has its own view, so multiple colliders on one object are not collapsed into a single renderer.

## Drawing and lifecycle

The 0.6.12 player screenshots confirmed detection on two tracks but showed cyan/orange washes across the entire scene. Filled double-sided volumes with depth testing set to Always accumulate over scenery, especially while the camera is inside a large respawn volume. The native scenery was not made transparent; the added filled overlays obscured it.

Runtime 0.6.13 draws mesh edges instead of filled surfaces. The added MeshRenderer is disabled; the shared geometry is drawn explicitly in the existing StudioBehaviour's OnRenderObject callback, for Game cameras that include Default. Mesh/box views follow the source transform; sphere/capsule views follow the source world center, orientation and effective radius. All native submeshes are drawn, including unreadable meshes, without extracting CPU vertex/index data. Cyan/orange line opacity is 0.85, depth testing is LessEqual, and depth writing is disabled where the shader exposes these properties. Boundaries hidden by scenery are now occluded instead of tinting everything in front of them. Missing geometry/material support is counted.

Wireframe rasterization is scoped around only the module's immediate mesh draws. A finally block restores the prior GL.wireframe value on success or failure, so native objects and the HUD keep their existing raster state. Rendering failure removes the owned visuals and logs once. Unity documents this state as global until restored; the current player's log confirms Direct3D 11. The implementation uses [GL.wireframe](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/GL-wireframe.html), [OnRenderObject](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour.OnRenderObject.html) and [Graphics.DrawMeshNow](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Graphics.DrawMeshNow.html). Wireframe support and callback availability differ on other graphics APIs/render pipelines; this is not a cross-platform certification.

Existing colliders, collision layers, native renderers/materials, cameras, track transforms and game debug flags stay untouched. No collider or physics mesh is created or rebaked. Only generated visual meshes are destroyed; native shared meshes are retained. Hiding, disabling, leaving the race, entering online play, changing maps and unloading remove the module's visual objects and materials. A camera that excludes Default will not show them; camera masks are never expanded.

The active collider inventory refreshes every five seconds. Selection/range checks run once a second; selected volumes follow their transforms each frame. No scene scanning occurs in menus. The legend reports walls/respawn shapes and unsupported counts. A zero result includes mask matches, out-of-range matches and already-visible walls; the log also records inventory size and the kart's exact three masks.

**Disable this module for performance captures.** Rendering boundary geometry adds work. Performance diagnostics records whether the view was visible.

## Current-build evidence

Current `GameAssembly.dll` SHA-256: `e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130`.

| Native routine | Address | Observation |
|---|---|---|
| `PTK_HideMeshRendererInPlayMode.Start` | `0x180a49ca0` | The enabled helper hides its own renderer and optionally children. It does not establish that all collision boundaries have this component. |
| `PixelEasyCharMoveKartController.ICharacterController.OnMovementHit` | `0x1805da290` | Wall mask triggers wall-hit effects. Always-respawn mask calls `VehicleCollidedWithOutsideMapResetCollider`; flat-respawn mask does so conditionally on the angle threshold. Respawn checks are independent of wall effects. |
| `PixelEasyCharMoveKartController.ICharacterController.IsColliderValidForCollisions` | `0x1805d9150` | Other collider categories dispatch separate gameplay effects; the module does not enable those categories. |

Read-only native exports are retained in ignored `local/track-boundaries-2026-10-09/native`. The matching existing Ghidra database was opened read-only without analysis or edits. Installed interop SHA-256: `7287268ca8a540c42f72525e0781e93643617427b109fe40446f347263529337`. The three kart mask getters are unique parameterless instance LayerMask getters (`20 00 11 81 11`); `Ant_MapData.get_instance` is a parameterless static Ant_MapData getter (`00 00 12 89 70`). Narrow metadata checker: ignored `local/performance-mod-interop/BoundaryCheck.cs`.

Read-only Unity scene metadata provides a concrete shipped example: `level11` (`biome1_scene1new_Race_ReleaseReady`) has 165 enabled, self-active wall/respawn colliders on layers 10/11/30. All 165 contain only a Transform and collider, with no renderer or MonoBehaviour on the same object. Types include mesh, box and capsule colliders. Global TagManager names these layers WallCollider, WallCollider_FlatRespawn and WallCollider_AlwaysRespawn; GroundCollider is layer 8. These names corroborate the native semantics but are not hardcoded selection rules. Counts do not establish activeInHierarchy at runtime, and this sampled scene has not been identified as the player's screenshot track. Source hashes, mesh references and scope limits are retained in ignored `local/boundary-regression/scene-evidence.json`.

## Validation boundary

Production-linked tests exercise collider selection, native wrapper casts, geometry, range changes, cleanup and scoped wireframe drawing. They check disabled filled renderers, depth settings, all submesh draws, callback guards, restoration of prior raster state and injected drawing failure cleanup. Compilation uses the installed game's references. The player's 0.6.12 screenshots verify live detection; they do not verify the new 0.6.13 line appearance. A manual player run is still required to check readability and callback scheduling.
