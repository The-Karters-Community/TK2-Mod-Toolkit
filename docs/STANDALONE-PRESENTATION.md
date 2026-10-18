# Standalone input viewer and camera controls

These two presentation modules can be separated from the Toolkit and BepInEx.
This document is a feasibility assessment, not a completed native port or a
claim of tested standalone behavior.

## What the existing modules actually need

| Feature | Existing implementation | Standalone requirement |
|---|---|---|
| Local driving inputs | `GamepadOverlay` reads `Ant_KartInput.inputData` plus the player's named Rewired actions | Read the game's mapped actions, including keyboard input. Reading a controller directly would lose remapping and some game-specific action behavior. |
| Ghost inputs | Reads the active `PTK_PlayerReplayInput_ReplaySerializer` | Read replay steering, acceleration, brake and recorded action pulses from the game, rather than the physical controller. |
| Camera framing | `CameraFeature` adjusts the output of `PixelGameKartCamera.GetRacingCamPositionAndRotation(float, ref Vector3, ref Quaternion, ref float)` | A callback at the native camera output, on the game thread. Occasional external FOV writes would be overwritten and would not preserve the current position/rotation behavior. |
| UI and persistence | Unity IMGUI, BepInEx ConfigEntry and the Toolkit's config merge | Small native controls and a settings file limited to these two modules. |

## Smallest suitable architecture

A native Windows executable can own settings, connection state and the input
overlay. Embed a small authored native DLL and load it into the game when the
user connects. The DLL would provide only input sampling and the local camera
callback. This replaces BepInEx's loading/hooking facilities; the camera feature
still executes code inside the game process.

Use the exported IL2CPP runtime API to resolve types, methods and fields by name
and complete signature, and obtain the native camera method at runtime. Bobby's
Garage's `native-bridge/QuickSetup.cpp` demonstrates that discovery and execution
on the game thread are available in this game. It handles bounded requests,
however, and is not already a per-frame camera/input implementation.

For a native port:

- Keep input state in a fixed-size shared snapshot. Update it on the game thread;
  draw the external overlay from the latest sample without request round trips
  or object scans on every frame.
- Preserve local-player, intro and spectator checks and the current camera math.
  Disabling overrides must immediately return to the native camera output.
- Use a transparent, non-activating overlay for borderless/windowed play.
  Exclusive fullscreen needs separate validation and may require an in-game
  drawing path to keep the same visibility.
- Separate connection discovery from per-frame work. Do not allocate, walk
  metadata or enumerate scene objects in the camera callback.
- Use native Windows UI/drawing to avoid bundling Python, Chromium or a managed
  runtime. A much smaller distribution is plausible, but no size or performance
  measurement exists until this port is built.
- Check the required signatures and field types rather than a game-version
  allowlist. Address relocation alone should not require maintenance; changed
  game contracts or camera code can still require a repair.

The current changes retain the existing BepInEx implementation and its online
presentation allowlist. They do not install a new native bridge, change the
protection policy, or produce a standalone executable.
