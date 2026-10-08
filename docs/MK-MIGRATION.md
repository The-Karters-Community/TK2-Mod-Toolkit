# MK legacy pack migration, 0.1.4.18

The original `MKsKartersMods` binaries and locally recovered C# remain intact. The new unified plugin contains fresh readable implementations in `LegacyMK*.cs`, using the installed game's current IL2CPP interop assemblies. Recovered third-party implementation files remain in ignored `local/managed/MKsKartersMods`, outside Git.

The supplied DLL contains 15 enabled-module implementations, plus the hidden, permanently disabled `SupraMayroKratt` experiment. Their ideas are covered below. This is implementation coverage, not a claim that every feature has passed an in-game test. None of the old `GameOverlay`/`SharpDX` DLLs are needed by these new implementations. No leaderboard upload patch is installed by the MK migration.

| Legacy module | Current implementation | Material differences and remaining validation |
|---|---|---|
| AlternateReservesMode | `AlternateReserves` | Keeps the legacy nonlinear reserve-growth formula. Local offline racers only; captures and restores boost reward lengths when disabled. Growth rate is configurable. |
| AutoBoosting | Existing `AutoBoost` module | Maintained by the main pack implementation. Check its documented behavior for bad-boost suppression and drift release. |
| BobbyGang | `BobbyGang` | Optional text-name replacement, configurable original/replacement names. No longer enabled by default. Restores text before rescanning, so live setting edits work. |
| BoringMode | Existing `SimpleDriving` module | Main pack controls local jump and drift inputs. |
| BoostTrainer | `BoostTrainer` | Uses the current Rewired player and boost-fill values. A short rumble occurs on threshold crossing rather than continuously overwriting game vibration. Threshold and strength are configurable. |
| CustomLapCounts | Existing `Laps` module | Main pack implementation uses the current complete no-argument signature. |
| CNKStyleBoostMeter | `CNKBoostMeter` | Three in-game fill bars beside each local kart replace the separate Windows overlay. Size, position, four colors, and threshold fractions are editable. Legacy arc shapes are not reproduced; this is an explicitly labeled CNK-inspired meter. Split-screen projection uses each player's gameplay camera and current screen pixels. Visual testing remains required. |
| CustomPhysicsAndBoostParameters | `KartParameters`, `BoostParameters` | All 35 legacy kart fields and all 22 legacy boost fields exist in the current declarations and have typed read/write bindings. Every field has a separate `Override...` switch, off by default. The displayed values are legacy presets, not asserted defaults of the current build. Turning off an override restores the captured current-game value. |
| DashAndStash | `DashAndStash` | F7 sets configurable raw reserves; F8 awards a selected item and ammo count through the current public `WeaponBoxReward_AddWeapon` entry point. Keyboard keys are configurable. Uses actual raw reserves rather than the Prologue speed-display approximation. Legacy keypad cycling and external info overlay are replaced by GUI settings. |
| FastRespawn | `FastRespawn` | The current validator has a new respawn state machine, vector-valued destination, and `PlayerOutsideTrack_RespawnPlayer(bool)` signature. The port scales captured movement speed and reduces attachment time, preserving native respawn phases. It does not run the legacy per-frame teleport/snap code. |
| MirrorMode | Migrated | The local camera projection and steering are mirrored with render-state restoration. See [Mirror race](MIRROR-RACE.md). |
| ProximityVoiceLines | `ProximityVoiceLines` | Uses current `CanPlayVOForPlayer()` and `PlayVoiceOver(bool)` targets. Nearby local AI are allowed through the normal voice predicate, with distance-based Wwise attenuation. The port does not temporarily impersonate a human player or replace native event dispatch. Attenuation overrides reset when disabled. Native event methods may contain additional AI checks; actual audible coverage requires testing. |
| ReverseRaceMode | `ReverseRace` | Uses the renamed `iPlayerMovedThroughFinishLineCount_LocalOnly` field and current exact hooks. Starts the local kart facing backward and reverses finish-counter deltas, maintaining partial lap times. Current checkpoint acceptance and respawn-route logic have changed; this module is labeled experimental until full backward-race behavior is verified. |
| SaveStates | `SaveStates` | Captures local KCC motor state, boost state, health, item/ammo, and optionally current lap/checkpoint flags and shared clock. F5/F6 shortcuts and controller actions are editable. Scene/race changes clear stale snapshots. Race-progress and clock rewinding start off. This does not rewind AI racers, active effect coroutines, checkpoint-time event history, or the new respawn-route history; it is a kart practice snapshot, not a complete deterministic game-state rewind. |
| TeleportersForTricks | `TeleportersForTricks` | Current landing method has one `bool` argument, not the legacy two-argument assumption. Awards the teleport item through the public current weapon-reward path. `ItemEffects.PortalDash.StartEffect()` blocks the following award for affected local players. Race resets clear bookkeeping. |
| SupraMayroKratt (hidden) | `AirBrake` | Exposes the legacy experimental idea as an optional module: braking while airborne cancels local kart velocity through the readable velocity helper. Ground braking stays with the game. |

`studio/mk_catalog.json` defines the same 14 newly migrated module sections and 157 configuration entries (including module enable switches), available before the plugin's first game launch. The main pack's three existing MK modules are grouped alongside them. Configuration choices include keyboard keys and current weapon enum names. Parameter rows carry `requires` metadata so the editor can associate each value with its override toggle.

## Evidence and checks

- Installed metadata source: `Il2CppDumperOutput/dump.cs`, SHA-256 `5a099f06192d449a5f53914be85957a4956c26785590e3bc5e14e1fc9f298109`.
- All 57 physics/boost field names checked against the current declarations; none were absent.
- Hook targets use `AccessTools.DeclaredMethod(type, name, exactArgumentTypes)`. There is no broad overloaded-name patching.
- Important changed declarations: respawn target is a `Vector3`; trick landing has one `bool`; lap counter ends in `_LocalOnly`; voice checks now include `CanPlayVOForPlayer()`.
- A build against current local references succeeded with zero errors before integration. Final integrated validation belongs to the root validation report.
- No game was launched automatically, and this migration agent did not deploy files. Runtime testing and REA evidence are tracked separately by the main task.

## Testing order

Enable one module at a time and test a local offline practice race. Check disable/re-enable and live numeric edits, race restart, scene change, and split-screen where relevant. Start with the meter, character names and rumble trainer, then respawn and teleport rewards. Keep reverse race, snapshot progress/clock rewinding, and selected advanced physics overrides for deliberate experiments after basic features behave correctly. Leaderboard uploads retain the game's behavior in the requested test build.
