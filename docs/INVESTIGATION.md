# Investigation — 2026-10-08

Installed `build-info.txt`: 0.1.4.18, beta_dev, Unity loader log: 6000.0.75f1, embedded runtime 6.0.7, BepInEx 6.0.0-be.788 / 5b766a3. This is local evidence, not a claim about every public Steam branch.

`TKMA-New-Mod-Template` and `The-Karters-Modding-Assistant-SDK` explicitly target the Prologue. They provide readable C# wrappers/events and Harmony patches, but use old `_references`, wildcard NuGet versions and additional SDK dependencies. `TheKarters2Mods` provides fast-fall, leaderboard suppression, config reload, Twitch integration and health/reserve commands. `MKsKartersMods` is binary-only locally and needs managed decompilation.

Current `BepInEx/LogOutput.log` shows AutoReloadConfigModSDK, DisableLeaderboards and MKsKartersMods loaded. NightmareAIs fails with `AmbiguousMatchException` for `HpBarController.Hit`. This is specific overload-selection breakage; it does not establish that injection failed. The log also warns that Class::Init signatures were exhausted and a substitute used. Retain as a compatibility observation to test, not proof of a fatal error. A loader success message does not prove every feature still behaves correctly.

Ghidra project `TK2_1_4_18.gpr` has a database directory `TK2_1_4_18.rep`; property file identifies `/GameAssembly.dll`. Database contains ~1 GB current program store and a previous ~565 MB store. The database is not plain source and must be queried with Ghidra APIs. Export should use `-process GameAssembly.dll -readOnly -noanalysis`.

## Completed analysis

Read-only Ghidra export confirms its executable hash matches the installed GameAssembly. The function index contains 184,138 functions (155,180 non-default names); 56 pseudocode bodies were exported across two successful selections. The metadata catalog contains 23,785 types. See [reconstruction notes](RECONSTRUCTION.md) for evidence quality and examples.

dnSpy recovered the MKsKartersMods managed DLL to 37 local C# files. Its modules include alternate reserves, AutoBoosting, BobbyGang, BoringMode, BoostTrainer, custom lap counts, CNK-style boost meter, custom physics and boost parameters, DashAndStash, fast respawn, mirror/reverse modes, proximity voice lines, save states, SupraMayroKratt and teleport rewards. These are useful patterns; not all enabled features are currently runtime-verified.

The static audit scanned 93 C# files across supplied source projects and recovered managed output. Of 81 named Harmony patch declarations, 79 have a current target by name; `HpBarController.Hit` needs overload selection and `JumpInputTheKarters` is absent. Namespaced types are resolved through the source's using statements. This explains why broad rewrites of every old mod are premature: migrate specific failed signatures/semantics first. See [the complete audit](LEGACY-AUDIT.md).

The current metadata exposes a substantial content-mod system: loaders, runtime gameplay config generation, mod data synchronization, Wwise event commands, conditions and triggers, plus character/vehicle/track info classes. This suggests a hybrid tool: BepInEx for code hooks and the existing content pipeline for compatible assets/rules. That recommendation is an inference from declarations; exact file formats and runtime restrictions remain to be verified.

## References

- [User's reverse-engineering guide](https://gist.github.com/BadMagic100/47096cbcf64ec0509cf75d48cfbdaea5)
- [BepInEx IL2CPP installation](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html)
- [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop)
- [Il2CppDumper](https://github.com/Perfare/Il2CppDumper)
- [Unity IL2CPP pipeline](https://docs.unity3d.com/Manual/scripting-backends-il2cpp.html)
- Local project READMEs and C# source files; local BepInEx log and build-info. Third-party sources remain outside this repository.
