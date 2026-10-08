# Mod possibilities

This is a feasibility catalog, not a promise that every feature is implemented or verified. **Starter** means code provided but manual runtime testing still required. **Evidence** means current declarations/old code give a target to investigate. **Research** means new hooks/assets/behavior must be established. Gameplay modifications need offline session and leaderboard protection.

| Area | Options | Evidence / effort |
|---|---|---|
| HUD and UI | HUD scale, font size, colors, contrast, boost/reserve display, health bars, timer precision, lap labels, speed telemetry, input overlay, hide elements, custom panels, menu shortcuts, localization | HUD scale starter; Canvas/UI/TMP targets in dump; others evidence/research |
| Audio | Master volume, music/effect/voice mix, mute individual events, pitch, replacement engine sounds, menu sounds, dynamic soundtrack, subtitles for cues | Master volume starter; separate mixers and Wwise/event routing require research |
| Camera | FOV, follow distance, height, smoothing, shake, speed zoom, spectator camera, photo mode, split-screen layout, VR comfort settings | FOV starter; PixelGameKartCamera and related classes are candidate targets |
| Kart physics | Fast fall, gravity, acceleration, top speed, steering, traction, drift angle, jump strength, air control, collision bounce, boost strength/duration, reserves | Fast-fall starter; PixelKartPhysics/Ant_BoostManager/controller wrappers in old mods; detailed balancing research |
| Input/accessibility | Deadzones, remaps, alternate fast-fall button, automatic boost, hold/toggle behavior, reduced flashes, larger HUD, color-blind palettes | Rewired/Ant_KartInput evidence; automatic boosting exists in MK binary |
| Rules | Lap count, timers, respawn delays, team rules, mode presets, elimination rules, weapon selection/weights, pickups, damage, immunity | MainGame, configuration, WeaponsController, HpBarController evidence |
| AI | Difficulty presets, aggression, pace, reaction time, catch-up, character composition | Existing NightmareAIs currently has failed Hit patch; investigate current AI APIs |
| Cosmetics | Characters/kart/material overrides, particles, trails, textures, animations, skins, custom icons | AssetBundle and object discovery work required; official Workshop capabilities should be reused |
| Tracks | Track rotation, practice checkpoints, custom objects, track editor enhancements | Game already exposes Workshop systems; inspect official formats before custom importers |
| Training | Drift trainer, ghost comparisons, splits, replay tools, telemetry CSV, collision visualization, slow-motion offline practice | Current lap/time/player data evidence; replay serialization research |
| Streaming | Twitch commands/events, challenge voting, local telemetry overlay | Existing Twitch SDK and managed overlay code evidence; secure handling of account credentials needed |
| Developer tools | Scene browser, component inspector, method finder, hook diagnostics, signature diff, C# templates, compiler diagnostics, packaging | Method index, exact-address pseudocode export, templates and build diagnostics delivered; live bridge and recipes planned |
| Mod management | Toggle DLLs, typed configs, build/deploy, backup/restore, fingerprints, logs, profiles, dependency/conflict reports | Initial GUI delivered; existing MK config exposes 90 settings/15 toggles; package/profile milestones planned |

Do not promise remote-server behavior changes, official online compatibility, arbitrary native hot reload, or reconstruction of removed source. Native IL2CPP methods do not have normal IL bodies for Harmony transpilers; use supported prefix/postfix hooks, field/property adapters, or carefully scoped native hooks when justified.
