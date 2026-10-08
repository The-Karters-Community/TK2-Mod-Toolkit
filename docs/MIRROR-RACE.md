# Mirror Race

Enable **Mirror race** in the Community Mods pack. It applies immediately in offline races and time trials; no track reload or asset rewrite is needed. The local gameplay camera flips horizontally, and only local human steering is reversed to match the view. AI input, collision shapes, track geometry, checkpoints, and game files stay untouched.

The projection flip is applied after the game's camera pre-cull callbacks. The mod saves the current projection and global culling flag, left-multiplies a horizontal clip-space flip (so asymmetric projections stay mirrored), inverts culling only for that camera's render, then restores both after rendering. Disabling the module also restores any pending camera state. This removes the previous mesh-copy/readback path that failed on non-readable assets and maps containing terrain or animated props.

## Current-build evidence

Read-only Ghidra analysis matches the installed `GameAssembly.dll` SHA-256 `e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130`.

| Native method | Address | Evidence |
| --- | --- | --- |
| `PixelSDK_CameraEvents.OnPreCull()` | `0x180994A20` | Calls the camera's pre-cull delegate before culling. |
| `PixelSDK_CameraEvents.OnPreRender()` | `0x180994A60` | Calls the camera's pre-render delegate. |
| `PixelSDK_CameraEvents.OnPostRender()` | `0x1809949E0` | Calls the camera's post-render delegate. |
| `PixelEasyCharMoveKartController.SteerInput(float)` | `0x1805DC260` | Confirms the current steering hook signature. |

## Limits and validation

This is a camera mirror, not a physical mirrored track. Camera-rendered text or world-space interface elements may also appear reversed; screen-space overlay UI is outside the camera projection. The module is guarded by the toolkit's offline gameplay check and only reverses steering for local human players.

The plugin builds against current local interop references with `python -m studio.cli build` (0 warnings, 0 errors). In-game race and time-trial rendering still needs a manual check; the game is never launched automatically by the build.
