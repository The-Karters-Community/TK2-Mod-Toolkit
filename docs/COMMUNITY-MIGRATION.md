# Cathret community pack migration

Audit date: 2026-10-08. Source: the user's unchanged `TheKarters2Mods` checkout,
whose Git origin is [Cathret/TheKarters2Mods](https://github.com/Cathret/TheKarters2Mods).
The original folder contains six projects, not a Nightmare AI source project.
New code is in `plugins/TK2.Customization/CommunityMods.cs` and
`CommunityCommandParser.cs`; no original folders were changed.

## Project mapping

| Original project | Unified replacement | Status and material differences |
|---|---|---|
| AerialFastFallMod | Physics/fast-fall module | Existing current-build module owned by the main pack. Legacy input modes and dodge are tracked with that module, not duplicated in CommunityMods. |
| AutoReloadConfigModSDK | StudioBehaviour config reload | Shared main-thread reload replaces the dependency DLL and mod-specific registration. |
| DisableLeaderboards | Excluded for this test build | User explicitly requested testing without blanket leaderboard disabling. No upload hook is installed by the community module. |
| TwitchIntegrationSDK | CommunityMods TLS receive worker | Explicit `ConnectToTwitch`, off by default; channel/login and token environment variable required. Reads chat, handles PING/PONG, never sends chat replies. |
| TwitchBasicCommandsSDK | CommunityCommandParser + main-thread dispatch | Prefix, successful-command cooldown, allowlist, bounded queue. Fixes the old `.Seconds` cooldown rollover and malformed arguments. |
| TwitchCathretCommands | Six command families below | Current game calls compiled against local 0.1.4.18 interop. No separate SDK dependency DLLs. |

## Command mapping

Every command requires `CommunityCommands.Enabled`, its individual enable switch,
an offline running race, and permission from `AllowedUsers`. Empty allowlist permits
only the configured channel owner; `*` explicitly permits all viewers. All switches
start off.

| Legacy command | Current behavior | Current API evidence |
|---|---|---|
| `#tk2 hp gain [amount]` | Add HP to local player 0; default 20; bounded command amount | `HpBarController.GetCurrentHP()`, generated `currentHp` property, `CheckHp()` |
| `#tk2 hp lose [amount]` | Subtract HP from local player 0, stop at zero, apply normal death check | Same HP signatures; both overloads of `Hit` are deliberately avoided |
| `#tk2 reserve gain [seconds]` | Add reserve duration; default 20 seconds | `Ant_BoostManager.GetCurrentBoostReservesTime()` and `fBoostingReserves` |
| `#tk2 reserve lose [seconds]` | Subtract reserve duration, stop at zero | Same current reserve APIs |
| `#tk2 reserve set seconds` | Set reserve duration, clamped to configured maximum | Same current reserve APIs |
| `#tk2 kill humans / ais / pos 1 / karter NAME` | Eliminate matching local racers through the normal death routine | `Ant_Player` types, `HpBarController.Death()`, `PlayerRaceLogic.GetCurrentPlayerRacePositionIndex()`, `Ant_Player.GetPlayerName()` |

The kill position is one-based. Ghosts, network players, and disabled player objects
are excluded. A name is one token, matching the original command parser. HP values
must be nonnegative integers; reserve seconds may be nonnegative decimals. Unknown,
missing, negative, non-finite, and overflowing arguments are rejected.

**Reserve compatibility limitation:** the old SDK translated engine speed percentages
into reserves using three empirical Prologue slopes and offsets. Those constants have
not been established for the current game. This port explicitly uses reserve seconds
and labels that change in configuration; it does not silently apply the old conversion.
Reconstructing the current percentage formula is tracked in the REA investigation.

## Twitch setup and authoring

Set `Channel` and `UserName` to Twitch login names. Put a current chat-read OAuth
token in the environment variable named by `TokenEnvironmentVariable` before
launching the game. The default name is `TK2_TWITCH_TOKEN`. Enable the desired
commands, then `Enabled` and `ConnectToTwitch`. Changing connection fields reconnects;
after a failed connection, toggle `ConnectToTwitch` to retry. Disconnect closes the
socket and clears pending messages. No credentials are copied into config or logs.

Transport follows [Twitch's official IRC documentation](https://dev.twitch.tv/docs/chat/irc/):
TLS on port 6697, PASS/NICK/JOIN, and PING/PONG. Twitch recommends EventSub for
new full chatbot integrations; the port retains IRC to preserve this legacy pack's
receive-only command model. It deliberately omits automatic chat feedback messages.

A C# recipe can queue a local test command without a network connection:

```csharp
CommunityMods.Receive("yourchannel", "#tk2 hp gain 20");
```

The same enable switches, allowlist, race guard, and cooldown apply. Calling from a
recipe is safe to queue off-thread; only the main-thread `Tick()` executes game calls.

## Validation and remaining work

- The entire plugin builds against the current installed BepInEx/interop references
  with zero warnings/errors after adding these files.
- `dotnet run --project tests/CommunityCommands/CommunityCommands.csproj -c Release`
  passes 62 assertions, including all command forms, malformed input regressions,
  broadcaster/allowlist authorization, and all six local command families mapping
  to their displayed default-off GUI setting.
- `python -m unittest discover -s tests -p test_community_catalog.py -v` verifies
  all 16 current C# bindings against `studio/community_catalog.json`, including
  defaults, types, and numeric ranges. `Enabled` is the feature's implicit toggle;
  the remaining 15 fields are explicitly listed in the catalogue.
- No game launch, Twitch connection, chat message, or game-effect test was performed.
  Compiling verifies current wrapper access; it does not certify the runtime behavior.
- Runtime verification still requires HP/HUD/death, reserve speed/expiry, elimination
  callbacks, Twitch login/reconnect, online transition, and disable/unload tests.
- Full outbound Twitch bot feedback is intentionally absent. An EventSub transport
  and the old speed-percentage reserve syntax remain separate future work.

## Additional companion migration

NightmareAIs.dll was found in the separate MK folder and decompiled into ignored local/managed/NightmareAIs-bbe51bc06d92. Its current port is NightmareAI.cs, exposed under MK's pack with all 20 typed settings. AI and local-human speed, reserves, damage, maximum HP, wall loss, rubberbanding, champion/chase behavior and optional names are implemented. Damage uses exact overloads with nested-call protection; wall handling preserves native callbacks. Runtime parity remains unverified.

Fast fall now includes both original input modes, configurable controller action/deadzone and optional dodge duration. The dodge flag uses the current per-source API and cannot clear native shield/respawn/death flags. Automatic config reload and Twitch framework plumbing are absorbed into the single DLL. Disable Leaderboards is omitted under the user's test policy.
