# Track Inspector

Enable **Track Inspector** in Mod Packs, save, and enter a local race or time trial. The in-race panel shows the active key, nearby classified zones, and a color legend. Press the configured key (F10 by default) to hide or show it. Sampling density, line width, draw distance, opacity, labels, categories, and the optional bounds fallback are configurable. This is a read-only overlay: it never changes colliders, layers, transforms, triggers, or respawn behavior.

| Color | What it identifies |
| --- | --- |
| Cyan | Physical wall layers used by the local kart controller, including invisible barriers |
| Orange | Always-respawn wall layer |
| Yellow | Conditional respawn wall layer; native tilt conditions still apply |
| Red | Trigger linked to an enabled `KillPlayer` command; native game conditions still apply |
| Purple | Other enabled game-logic trigger; not assumed to be lethal |

Box, sphere, and capsule colliders use their real primitive shapes. Readable mesh colliders show their boundary and sharp crease edges, welding matching positions at mesh seams so flat triangulation lines do not dominate. Convex, unreadable, very large, and other supported collider types use `Collider.Raycast` samples of the cooked PhysX surface instead of an axis-aligned bounds cube. Nearby samples are joined only when they remain spatially close, to avoid drawing bridges across gaps. If sampling fails, the zone is skipped by default; **ShowBoundsFallback** can opt into an explicitly labelled world-bounds approximation.

The overlay draws at most the 32 nearest zones and 400 line segments per collider, with up to 24 screen labels. **ShowThroughTrack** is enabled by default so hidden barriers remain visible through the track; turn it off to see only unobstructed surfaces. Surface sampling is cached for each zone and rebuilt when its configured resolution changes. Increase **DrawDistance** to search farther; reduce it to keep the view focused. The geometry is rescanned every four seconds, so newly changed trigger links may take that long to update.

The active track is taken from `Ant_MapData` and its registered trigger-collider scenes. Player-attached colliders are excluded. Labels use the local gameplay camera. This reveals colliders classified by the game’s wall/respawn masks and initialized physics-trigger associations; it cannot reveal scripted hazards without those colliders, falls caused by movement or course logic, or every possible death condition.

## Current-build evidence

Ghidra's saved project and installed `GameAssembly.dll` both identify SHA-256 `e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130`. Read-only headless exports used `-readOnly -noanalysis`; pseudocode is analysis evidence, not recovered original C#.

| Native method | Address | Observed behavior |
| --- | --- | --- |
| `PixelEasyCharMoveKartController.ICharacterController.OnMovementHit` | `0x1805DA290` | Checks hit-object layers against the actual wall and respawn masks; always-respawn contacts call the validator, while flat-respawn contacts have an angle predicate |
| `PTK_VehicleOutsideMapValidator.VehicleCollidedWithOutsideMapResetCollider(GameObject)` | `0x1805CCD70` | Starts native respawn processing after player, tutorial, and transition guards |
| `PTK_TriggerArrayCommandsExecutor.Start()` | `0x180A688A0` | Expands command/trigger parents and retains initialized trigger and behavior lists |
| `PTK_PlayerLogicModCommands.ExecuteEffect_KillPlayer(...)` | `0x1805BEE80` | Applies current-HP damage after `CanExecute` |
| `PTK_PlayerLogicModCommands.CanExecute(...)` | `0x1805BE200` | Applies immunity and other game conditions; a linked kill command is therefore a potential hazard |

The module uses the controller's masks and the executor's initialized lists rather than guessing from object names or proximity. Local exports are under ignored `local/ghidra-walls/` directories.

Rendering and real-track coverage still need in-game confirmation. Compilation does not establish that outlines are visible or that every hazard is classified. The game is not launched automatically.
