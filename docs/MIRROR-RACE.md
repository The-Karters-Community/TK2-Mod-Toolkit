# Mirror Race

Enable **Mirror Race** in Community Mods for a local offline race or time trial. The plugin finds the local player's gameplay camera before the countdown, attaches a small image-effect component, and flips the completed camera image left-to-right. It also reverses steering for the local human player. It does not change track geometry, camera transforms, projection matrices, culling, lighting, or AI controls. When disabled, the image effect is turned off and steering returns to normal.

This replaces two earlier approaches in the Toolkit history. The first physically reflected scene transforms and cloned meshes before map setup; that was complex and fragile around baked lighting, animated assets, and generated tracks. The later camera approach changed the projection matrix and global face-culling state and also waited for the race-running state, which begins after the countdown. The current image flip avoids both scene mutation and the countdown delay.

## Current-build evidence

Read-only Ghidra analysis matches the installed `GameAssembly.dll` SHA-256 `e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130`.

| Native method | Address | Evidence |
| --- | --- | --- |
| `PixelEasyCharMoveKartController.SteerInput(float)` | `0x1805DC260` | Confirms the current steering hook signature. |

The image flip is implemented by the Toolkit's injected `MirrorImageEffect.OnRenderImage` callback. The plugin logs when it attaches before the countdown and when the callback first runs.

## Limits and validation

This mirrors the camera output, not a second mirrored track simulation. Camera-rendered text or world-space interface elements may also appear reversed; screen-space overlay UI is outside the camera image. The module is guarded by the Toolkit's offline gameplay check and only reverses steering for local human players.

The plugin builds against current local interop references with `python -m studio.cli build` (0 warnings, 0 errors). In-game race and time-trial rendering still needs a manual check; the game is never launched automatically by the build.
