# Track boundaries

The **Track boundaries** module in Toolkit Essentials reveals only authored invisible-wall and respawn debug meshes in an offline race. Enable it, then press **F10** to hide/show the view. Cyan marks walls; orange marks both unconditional and flat/angle-dependent respawn boundaries. Separate switches hide either category. The default range is 200 metres from a local kart, adjustable from 25 to 1,000 metres. A small legend reports the number of selected meshes.

This is a new, narrow module. The retired `Recipe.TrackInspector`, general trigger view, generated collider approximations and removed Race Lab recipes stay retired.

## How selection works

Only active meshes beneath the game's `PTK_HideMeshRendererInPlayMode` component with `bEnabled=true` are candidates. Its `bIncludeChildrenMeshRenderers` flag determines whether children qualify. Each candidate needs an enabled collider on its object or an ancestor within that hide component's subtree. Player-owned objects and unrelated scenes are excluded. Selection uses the local kart's actual wall and respawn layer masks, rather than names, tags or guessed layer numbers. A respawn mask takes precedence if a layer also causes a wall-hit effect. Visible scenery and other trigger categories are not selected.

The module creates a render-only child for each selected authored mesh, sharing its original `MeshFilter.sharedMesh` with identity local position, rotation and scale. It adds only a MeshFilter and MeshRenderer, with reusable cyan/orange materials. It prefers Unity's internal unlit colored shader when present, with transparency, depth testing and double-sided drawing. If that shader was stripped, it uses the debug mesh's own shipped shader when it exposes a color property; opacity then depends on that shader and may be solid. Unsupported meshes/materials are counted instead of silently claiming a successful view. The view follows the authored debug mesh, which can differ from a cooked physics mesh.

Native renderers, their materials and visibility/shadow settings remain untouched. Camera masks also remain untouched: the visual children use Unity's Default layer, so enabling the view cannot reveal unrelated objects sharing a collider layer. The module does not change any existing collider, transform, GameObject active state, collision mask, global debug flag or respawn behavior. Switching off, hiding the view, leaving the race, entering online play, changing tracks or unloading destroys the visual children. Materials are destroyed when the module is disabled or the track changes. Cameras that exclude the Default layer will not show these children; no camera masks are expanded to work around that.

**Turn this module off for performance captures.** Revealing geometry adds rendering work. The next diagnostic schema records whether boundary meshes were visible. Disabling the module is preferable to the F10 visibility toggle because it also removes scanning and the legend.

## Current-build evidence

Current `GameAssembly.dll` SHA-256: `e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130`.

| Native routine | Address | Observation |
|---|---|---|
| `PTK_HideMeshRendererInPlayMode.Start` | `0x180a49ca0` | Unless the global force-show flag is set, `bEnabled` hides the object's MeshRenderer; `bIncludeChildrenMeshRenderers` also hides its children. The shipped routine does not destroy those meshes. |
| `PixelEasyCharMoveKartController.ICharacterController.OnMovementHit` | `0x1805da290` | Wall mask triggers wall-hit effects. Always-respawn mask calls `VehicleCollidedWithOutsideMapResetCollider`; flat-respawn mask does so conditionally on the angle threshold. |
| `PixelEasyCharMoveKartController.ICharacterController.IsColliderValidForCollisions` | `0x1805d9150` | Other collider categories dispatch separate gameplay effects; enabling a global trigger/debug view would exceed this module's scope. |

Four read-only native exports, including `PTK_RoadPointsCreatorTool.Awake`, are retained in ignored `local/track-boundaries-2026-10-09/native`; zero decompilation failures. The matching existing Ghidra database was opened read-only without analysis or edits. REA's large managed-member response failed with `Invalid string length`; a narrow PE/CLI MetadataReader check supplies the missing signature evidence without loading or executing the game assembly.

Installed interop SHA-256: `7287268ca8a540c42f72525e0781e93643617427b109fe40446f347263529337`. `get_bEnabled` and `get_bIncludeChildrenMeshRenderers` are unique parameterless instance Boolean getters (`20 00 02`). The three wall/respawn getters are parameterless instance `LayerMask` getters (`20 00 11 81 11`); `Ant_MapData.get_instance` is a parameterless static `Ant_MapData` getter (`00 00 12 89 70`). Reproducible checker and output: ignored `local/performance-mod-interop/BoundaryCheck.cs` and `local/track-boundaries-2026-10-09/interop-signatures.json`.

## Validation boundary

Compilation against the installed game proves API compatibility. Production-linked tests check selection and restoration; they cannot establish which meshes are present on every shipped track, shader appearance, visibility through the game's cameras or actual respawn geometry. Those need a manual game run. A zero-count legend and BepInEx log identify a track with no matching authored meshes; the module does not fall back to displaying unrelated triggers or inventing collider shapes.
