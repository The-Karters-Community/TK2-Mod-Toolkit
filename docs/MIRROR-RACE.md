# Mirror Race

Enable **Mirror Race** in Community Mods for a local offline race or time trial. The plugin finds the local player's gameplay camera before the countdown, attaches a small image-effect component, and flips the completed camera image left-to-right. It also reverses steering for the local human player. It does not change track geometry, camera transforms, projection matrices, culling, lighting, or AI controls. When disabled, the injected image-effect component is removed and steering returns to normal.

This replaces two earlier approaches in the Toolkit history. The first physically reflected scene transforms and cloned meshes before map setup; that was complex and fragile around baked lighting, animated assets, and generated tracks. The later camera approach changed the projection matrix and global face-culling state and also waited for the race-running state, which begins after the countdown. The current image flip avoids both scene mutation and the countdown delay.

## Menu correction in 0.6.12

The previous enabled module searched all kart controllers every 50 milliseconds while waiting for a race camera, including in the menu. It retained disabled image-effect components after a race, and render/steering callbacks did not check the current menu/map/config context. These were unnecessary menu work and incomplete effect ownership; their individual contribution to the player's menu frame time was not measured.

The module now checks enabled/offline state, absence of MenuManager and a live Ant_MapData before any scene search. In menus or while no map exists it removes its injected effects and performs no scene scans. During track setup/racing it searches at most four times a second and reuses its working collection. Stale, inactive or spectator cameras lose the injected callback. Both rendering and steering check the current context directly, covering transitions before the next update. A race-running-only gate is deliberately avoided so mirroring still starts before the countdown. The native camera and GameObject are never destroyed.

## Current-build evidence

Read-only Ghidra analysis matches the installed `GameAssembly.dll` SHA-256 `e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130`.

| Native method | Address | Evidence |
| --- | --- | --- |
| `PixelEasyCharMoveKartController.SteerInput(float)` | `0x1805DC260` | Confirms the current steering hook signature. |
| `Ant_MapData.Awake` | `0x1805CF0A0` | Assigns the current map instance during track setup, before road/progress/minimap initialization. |

The image flip is implemented by the Toolkit's injected `MirrorImageEffect.OnRenderImage` callback. The plugin logs when it attaches before the countdown and when the callback first runs.

## Limits and validation

This mirrors the camera output, not a second mirrored track simulation. Camera-rendered text or world-space interface elements may also appear reversed; screen-space overlay UI is outside the camera image. The module is guarded by the Toolkit's offline gameplay check and only reverses steering for local human players.

The plugin builds against current local interop references with `python -m studio.cli build` (0 warnings, 0 errors). `tests/MirrorRace` links the production recipe and image-effect code: 49 assertions cover zero scans/callbacks through menu/loading/disable/online/map-destruction transitions, countdown attachment, mirrored rendering/steering and preservation of native cameras. These stub tests establish code behavior, not engine render timing. Live menu FPS and race/time-trial rendering still need a manual check; the game is never launched automatically by the build.
