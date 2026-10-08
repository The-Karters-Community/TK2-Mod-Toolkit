# Packs and migration

One physical plugin, TK2.Customization.dll, contains these logical packs. New modules start disabled; defaults do not modify a race until enabled. All bindings are exposed in the static GUI catalogs before the first plugin launch, including booleans, enums, numeric limits and editable text. Individual advanced values require their own override switch.

| Pack | Modules |
|---|---|
| Garage Essentials | HUD size, HUD transparency, audio mixer, camera setup, graphics |
| MK's Karters Mods | Custom laps, driving challenge, automatic drift boost, basic kart tuning, fast respawn, boost trainer, alternate reserves, Dash and Stash, portal tricks, mirror, Bobby Gang, nearby voice lines, CNK-style boost meter, air brake, kart/boost parameters, practice save states, reverse race, Nightmare AI |
| The Karters Community | Both fast-fall input modes and optional dodge; health/reserve/elimination chat commands |
| Your recipes | FrameLimiter and author-created modules in the same DLL |

See MK-MIGRATION.md and COMMUNITY-MIGRATION.md for per-module signatures, settings and differences. NightmareAIs.dll from the MK folder is also recovered locally and ported in NightmareAI.cs; it is not omitted simply because its source was absent from TheKarters2Mods. Its ambiguous damage patches now resolve both current overloads by full signature, and its obsolete Harmony call is corrected for the installed version.

The full 57 legacy numeric tuning ideas have typed current-build bindings. They are legacy presets, not assumed native defaults. Disabled overrides restore captured values. Basic speed/jump tuning and advanced matching overrides coordinate ownership; advanced wins. Basic/advanced tuning wins over Nightmare AI human speed. Other combinations still need explicit in-game testing.

Automatic boost includes the legacy early-press suppression and boost release on drift stop, with a configurable threshold. Driving challenge exposes separate jump/drift restrictions. Fast fall exposes press/hold, controller action, air time, acceleration, deadzone and optional dodge duration. Dodge uses an independent immunity flag, preserving native shield/respawn/death flags.

All legacy **ideas** have a current code path or documented adaptation. This is not a claim of complete native/runtime parity: CNK bars replace the external arc overlay, mirror particle/GI fixes remain limited, reverse track checkpoint/respawn semantics remain experimental, snapshot route histories/other racers are not rewound, and old empirical reserve-speed percentages were replaced by explicit seconds. Framework SDK projects are absorbed into the single plugin; they are not separately toggled empty mods. Disable Leaderboards is deliberately excluded for this requested test build.
