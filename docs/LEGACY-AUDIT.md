# Legacy Harmony target audit

Generated with `python tools/audit_legacy.py` against the indexed current dump.

This is a static name/signature audit. A present target does not establish compatible fields, hook semantics, native hookability, or working runtime behavior. Duplicated targets are counted separately because they occur in different modules. Current declarations cannot reveal a legacy binary's compile-time overload requirements.

| Source | C# files scanned |
|---|---:|
| TKMA-New-Mod-Template | 3 |
| The-Karters-Modding-Assistant-SDK | 22 |
| TheKarters2Mods | 31 |
| managed | 37 |

| Result | Patch declarations |
|---|---:|
| method absent | 1 |
| named target present | 78 |
| overload review | 1 |
| type absent | 1 |

| Source file and line | Target | Static result |
|---|---|---|
| TKMA-New-Mod-Template/src\Core\Ant_CurrentGameConfiguration.cs:8 | `Ant_CurrentGameConfiguration.Start` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Ant_CurrentGameConfiguration.cs:6 | `Ant_CurrentGameConfiguration.Start` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Ant_MainGame.cs:7 | `Ant_MainGame.Start` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Ant_MainGame.cs:22 | `Ant_MainGame.FixedUpdate` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Ant_MainGame.cs:36 | `Ant_MainGame.GetGameModeRequiredLapCount` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Ant_MainGame_Players.cs:7 | `Ant_MainGame_Players.Start` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Ant_MainGame_Players.cs:21 | `Ant_MainGame_Players.ResetAndPreparePlayersToRace` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Health\HpBarController.cs:6 | `HpBarController.RefillHp` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Health\HpBarController.cs:25 | `HpBarController.Hit` | overload review |
| The-Karters-Modding-Assistant-SDK/src\Core\Health\HpBarController.cs:46 | `HpBarController.Death` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Player\Ant_KartInput.cs:7 | `Ant_KartInput.ProcessRacingInput` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Player\Ant_Player.cs:6 | `Ant_Player.Update` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Player\PixelKartPhysics.cs:6 | `PixelKartPhysics.FixedUpdate` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Player\PixelKartPhysics.cs:43 | `PixelKartPhysics.JumpInputTheKarters` | method absent |
| The-Karters-Modding-Assistant-SDK/src\Core\Reserve\Ant_BoostManager.cs:6 | `Ant_BoostManager.FixedUpdate` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Reserve\Ant_BoostManager.cs:17 | `Ant_BoostManager.FireSliderBoost` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Reserve\Ant_BoostManager.cs:32 | `Ant_BoostManager.WallCollisionOccuredInTimeFromLastOne` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Reserve\Ant_BoostManager.cs:43 | `Ant_BoostManager.BoostPadTriggerEnter` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Reserve\Ant_BoostManager.cs:54 | `Ant_BoostManager.OnKartLandedAfterPlayerTriggeredJump` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Weapon\WeaponsController.cs:6 | `WeaponsController.WeaponBoxReward_AddWeapon` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Weapon\WeaponsController.cs:17 | `WeaponsController.PickupCurrentlySelectedWeapon` | named target present |
| The-Karters-Modding-Assistant-SDK/src\Core\Weapon\WeaponsController.cs:28 | `WeaponsController.Shoot` | named target present |
| TheKarters2Mods/AerialFastFallMod\Patches\FastFallOnPress.cs:63 | `PixelKartPhysics.FixedUpdate` | named target present |
| TheKarters2Mods/AerialFastFallMod\Patches\FastFallOnPress.cs:122 | `Ant_KartInput.ProcessRacingInput` | named target present |
| TheKarters2Mods/AerialFastFallMod\Patches\FastFallWhileJoystickInput.cs:15 | `PixelKartPhysics.FixedUpdate` | named target present |
| TheKarters2Mods/DisableLeaderboards\Patches\DisableLeaderboards.cs:7 | `KartersLeaderboardsManager.RaceFinished_UploadLeaderboard` | named target present |
| managed/MKsKartersMods\DisableLeaderboardUploads.cs:16 | `KartersLeaderboardsManager.RaceFinished_UploadLeaderboard` | named target present |
| managed/MKsKartersMods\Mods\AlternateBoostMeter\CNKStyleBoostMeter.cs:155 | `PixelEasyCharMoveKartController.FixedUpdate` | named target present |
| managed/MKsKartersMods\Mods\AlternateReservesMode.cs:46 | `PixelEasyCharMoveKartController.FixedUpdate` | named target present |
| managed/MKsKartersMods\Mods\AutoBoosting.cs:45 | `Ant_BoostManager.FixedUpdate` | named target present |
| managed/MKsKartersMods\Mods\AutoBoosting.cs:62 | `PixelKartPhysics.StopDrifting` | named target present |
| managed/MKsKartersMods\Mods\AutoBoosting.cs:80 | `Ant_BoostManager.BoostInput` | named target present |
| managed/MKsKartersMods\Mods\BobbyGang.cs:46 | `Ant_CurrentGameConfiguration.Update` | named target present |
| managed/MKsKartersMods\Mods\BoostTrainer.cs:46 | `Ant_KartInput.ProcessRacingInput` | named target present |
| managed/MKsKartersMods\Mods\BoringMode.cs:43 | `PixelEasyCharMoveKartController.DriftInput` | named target present |
| managed/MKsKartersMods\Mods\BoringMode.cs:51 | `PixelEasyCharMoveKartController.JumpInput` | named target present |
| managed/MKsKartersMods\Mods\CustomLapCounts.cs:45 | `Ant_MainGame.GetGameModeRequiredLapCount` | named target present |
| managed/MKsKartersMods\Mods\CustomPhysicsMod\CustomPhysicsAndBoostParameters.cs:119 | `PixelKartPhysics.FixedUpdate` | named target present |
| managed/MKsKartersMods\Mods\CustomPhysicsMod\CustomPhysicsAndBoostParameters.cs:166 | `Ant_BoostManager.FixedUpdate` | named target present |
| managed/MKsKartersMods\Mods\DashAndStash.cs:72 | `Ant_MainGame.FixedUpdate` | named target present |
| managed/MKsKartersMods\Mods\DashAndStash.cs:126 | `Ant_KartInput.ProcessRacingInput` | named target present |
| managed/MKsKartersMods\Mods\FastRespawn.cs:47 | `PTK_VehicleOutsideMapValidator.FixedUpdate` | named target present |
| managed/MKsKartersMods\Mods\FastRespawn.cs:67 | `PTK_VehicleOutsideMapValidator.FixedUpdate` | named target present |
| managed/MKsKartersMods\Mods\FastRespawn.cs:95 | `PTK_VehicleOutsideMapValidator.PlayerOutsideTrack_RespawnPlayer` | named target present |
| managed/MKsKartersMods\Mods\MirrorMode.cs:46 | `Ant_MainGame.Update` | named target present |
| managed/MKsKartersMods\Mods\MirrorMode.cs:61 | `PixelSDK_CameraEvents.OnPreCull` | named target present |
| managed/MKsKartersMods\Mods\MirrorMode.cs:72 | `PixelSDK_CameraEvents.OnPreRender` | named target present |
| managed/MKsKartersMods\Mods\MirrorMode.cs:80 | `PixelSDK_CameraEvents.OnPostRender` | named target present |
| managed/MKsKartersMods\Mods\MirrorMode.cs:88 | `PixelEasyCharMoveKartController.SteerInput` | named target present |
| managed/MKsKartersMods\Mods\MirrorMode.cs:101 | `Ant_KartParticlesWorker.WorkerUpdate` | named target present |
| managed/MKsKartersMods\Mods\MirrorMode.cs:112 | `PTK_CounterBoardController.Update` | named target present |
| managed/MKsKartersMods\Mods\MirrorMode.cs:121 | `UL_FastGI.OnEnable` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:50 | `PlayerRaceLogic.OnRaceResetted` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:60 | `PTK_PlayerVoiceOverManager.PlayVoiceOver` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:207 | `PTK_PlayerVoiceOverManager.OnBoostFired` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:215 | `PTK_PlayerVoiceOverManager.OnBoostFired` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:223 | `PTK_PlayerVoiceOverManager.OnDeath` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:231 | `PTK_PlayerVoiceOverManager.OnDeath` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:239 | `PTK_PlayerVoiceOverManager.OnHitByPlayer` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:247 | `PTK_PlayerVoiceOverManager.OnHitByPlayer` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:255 | `PTK_PlayerVoiceOverManager.OnItemPickUpped` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:263 | `PTK_PlayerVoiceOverManager.OnItemPickUpped` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:271 | `PTK_PlayerVoiceOverManager.OnItemUsed` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:279 | `PTK_PlayerVoiceOverManager.OnItemUsed` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:287 | `PTK_PlayerVoiceOverManager.OnJustMadeTrick` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:295 | `PTK_PlayerVoiceOverManager.OnJustMadeTrick` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:303 | `PTK_PlayerVoiceOverManager.OnOttoRespawningUsStartedHoldingUs` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:311 | `PTK_PlayerVoiceOverManager.OnOttoRespawningUsStartedHoldingUs` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:319 | `PTK_PlayerVoiceOverManager.OnPlayerIntroductionBeforeRace` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:327 | `PTK_PlayerVoiceOverManager.OnPlayerIntroductionBeforeRace` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:335 | `PTK_PlayerVoiceOverManager.PassedThroughDeadPlayer` | named target present |
| managed/MKsKartersMods\Mods\ProximityVoiceLines.cs:343 | `PTK_PlayerVoiceOverManager.PassedThroughDeadPlayer` | named target present |
| managed/MKsKartersMods\Mods\ReverseRaceMode.cs:46 | `PlayerRaceLogic.PrepareToStartRace` | named target present |
| managed/MKsKartersMods\Mods\ReverseRaceMode.cs:61 | `PixelEasyCharMoveKartController.FixedUpdate` | named target present |
| managed/MKsKartersMods\Mods\ReverseRaceMode.cs:89 | `PlayerRaceLogic.UpdateAndCalculateLapCount` | named target present |
| managed/MKsKartersMods\Mods\ReverseRaceMode.cs:102 | `PlayerRaceLogic.UpdateAndCalculateLapCount` | named target present |
| managed/MKsKartersMods\Mods\SaveStatesMod\SaveStates.cs:73 | `Ant_KartInput.ProcessRacingInput` | named target present |
| managed/MKsKartersMods\Mods\SaveStatesMod\SaveStates.cs:216 | `Ant_MainGame.GetGameModeRequiredLapCount` | named target present |
| managed/MKsKartersMods\Mods\SupraMayroKratt.cs:42 | `PixelEasyCharMoveKartController.FixedUpdate` | named target present |
| managed/MKsKartersMods\Mods\TeleportersForTricks.cs:46 | `Ant_BoostManager.OnKartLandedAfterPlayerTriggeredJump` | named target present |
| managed/MKsKartersMods\Mods\TeleportersForTricks.cs:61 | `PortalDash.StartEffect` | type absent |

## Migration notes

- `HpBarController.Hit` has both three- and four-argument overloads in current metadata. Specify the intended types; do not pick the first reflection result.
- `PixelKartPhysics.JumpInputTheKarters` is absent; `JumpInput(bool)` exists but is not assumed to be a drop-in replacement. Its native body includes input buffering, replay grounding and coyote-time logic.
- The Twitch SDK carries live network credentials in config in old examples; keep generated config/backups local. The new studio does not initialize external accounts.
- Old TKMA hooks dereference kartController before testing the instance; correct null/lifecycle ordering when porting. Event subscriptions also need explicit disposal when unloading.
- The old config reloader and wrappers remain independent dependencies. The starter plugin implements its own main-thread reload and has no dependency on those SDK DLLs.
