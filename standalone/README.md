# Bobby's Race Tools

A small Windows app for **camera framing** and a **driving input overlay** in
The Karters 2 Turbo Charged. No BepInEx, .NET, Python, or browser runtime required.

## Use

1. Run **BobbyRaceTools.exe** and start the game yourself.
2. Click **Connect**. Choose your local player under **Input viewer** if needed.
3. Enable **Camera overrides** to adjust framing. Each value has its own reset.

**F8** opens or minimizes Camera. **F9** toggles the input overlay. Change either
shortcut in Input viewer. Settings save automatically beside the executable;
read-only installations use `%LOCALAPPDATA%\BobbyRaceTools` instead.

Camera overrides start off in a fresh installation. Disabling them, disconnecting,
or closing the app returns control to the game. Inputs follow the game's mapped
keyboard/controller actions. Your inputs take priority when racing opponent ghosts;
recorded input viewing is optional.

Use windowed or borderless mode for the overlay. Exclusive fullscreen is not
supported by the external overlay. Do not enable the Toolkit's Camera and
Gamepad viewer at the same time as these standalone equivalents.

## Compatibility

Windows 10/11 x64. Tested in a local race on game **0.1.4.24** with BepInEx
disabled: connection, live input HUD, camera changes and immediate camera restore.
Replay source selection has regression coverage, but a standalone ghost session
has not yet been tested. The two programs should run with the same permissions.

The app discovers named game types, method signatures and field layouts when
connecting. It has no fixed game addresses or patch-version allowlist. Ordinary
address changes should not need an update; major changes to game or engine
internals can still require a fix. No promise of compatibility with every future
build is possible.

The executable contains an authored native bridge, extracted to a hashed cache
under `%TEMP%\BobbyRaceTools`. Connecting loads it into the game for the camera
and mapped inputs. No game files or saves are changed. A small inactive bridge
remains loaded until the game exits; it stops applying settings when disconnected,
and falls back after 1.5 seconds if the app stops responding.

## Build from source

Install Python and Visual Studio C++ x64 Build Tools, then run from the repository:

```powershell
python standalone/build.py
```

The script runs the native core tests and creates
`artifacts/BobbyRaceTools/BobbyRaceTools.exe`, with the C++ runtime and bridge
embedded. Only Windows system DLLs are required. Build records and intermediates
stay under ignored `artifacts/` and `local/`.

Implementation evidence is in `docs/STANDALONE-PRESENTATION.md` in the source
repository. MinHook's license and provenance are in
[THIRD-PARTY.txt](THIRD-PARTY.txt) and [MinHook-LICENSE.txt](MinHook-LICENSE.txt).
