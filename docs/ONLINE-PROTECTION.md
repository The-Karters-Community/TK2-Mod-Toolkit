# Online protection

The global **Online protection** module is mandatory and locked on. Users cannot disable the module or either protection rule in the toolkit. The plugin enforces both rules regardless of config-file values, and the toolkit repairs attempts to turn them off. An action is blocked while an unapproved Toolkit module, enabled recipe, or non-Toolkit BepInEx plugin is loaded. With no unapproved code active, normal leaderboard and online-room behavior continues.

The built-in allowlist is an explicit list of local presentation and accessibility tools. New modules remain protected until their behavior has been reviewed:

| Module | Decision | Reason |
|---|---|---|
| HUD size (`UI`) | Allow | Changes matching HUD canvas scale only. |
| HUD transparency (`HudOpacity`) | Allow | Changes alpha on existing HUD CanvasGroups only. |
| Disable vignette (`DisableVignette`) | Allow | Removes a screen-edge effect without changing race state. |
| Audio mixer (`Audio`) | Allow | Changes local audio mix levels only. |
| Camera setup (`Camera`) | Allow | Changes local camera framing and rotation. |
| Character names (`BobbyGang`) | Allow | Replaces visible text in the local UI only. |
| Performance diagnostics (`PerformanceDiagnostics`) | Allow | Measures local runtime counters; does not alter game state. |

Everything else is unapproved by default. Shadow-distance overrides can change visibility; track boundaries reveal collision and respawn information; boost training, proximity voice cues, and boost meters expose timing or positional information. Race performance includes an AI-physics cadence option, so the entire module stays protected. Physics, AI, race rules, items, respawn, teleportation, rewinds, and custom recipes can change gameplay directly. These remain blocked even if a particular setting looks cosmetic or is currently inactive inside its enabled module. Unknown modules and other BepInEx plugin IDs also require protection.

## Hook coverage

The installed `GameAssembly.dll` SHA-256 is `e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130`, matching the read-only Ghidra project. The native `KartersLeaderboardsManager$$RaceFinished_UploadLeaderboard` function is at `0x180529170`; its current interop signature is `RaceFinished_UploadLeaderboard(float, float[], List<CCheckpointTimes>, Action, Action)`. The prefix returns before that function's record-upload path when protection applies.

The native GameAssembly contains Photon PUN room APIs including `PhotonNetwork.JoinLobby` (`0x18258d580`, `0x18258d660`), `JoinRoom` (`0x18258e740`), `JoinRandomRoom` (`0x18258e180`, `0x18258e670`, `0x18258e6e0`), `JoinOrCreateRoom` (`0x18258d720`), `JoinRandomOrCreateRoom` (`0x18258dc40`), `CreateRoom` (`0x182589580`), and `ReconnectAndRejoin` (`0x182596180`). The plugin discovers and patches every exact bool-returning overload of those named operations through the current interop `MethodInfo`s; a missing operation fails plugin initialization rather than silently installing partial protection.

This is a client-side guard over those game APIs, not a server-side anti-cheat or publisher permission. The native targets and signatures are static evidence for game build 0.1.4.18. Compilation and policy tests do not prove runtime Harmony dispatch or cover a future build that routes online requests through different APIs; verify manually in-game after installing the plugin. No game was launched for this change.
