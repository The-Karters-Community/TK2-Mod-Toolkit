# Implementation roadmap

## Outcome and source recovery limits

Build a Windows GUI that lets a mod author inspect readable game APIs, understand selected native implementations, edit C# mods, compile against the installed game, deploy with rollback, and manage settings. A player should be able to choose a profile and toggle a mod without handling DLLs manually.

IL2CPP converts managed IL to C++ and a native binary. Metadata preserves many type names and signatures; it does not preserve original C# bodies, comments, local variable names, project layout, or code removed by stripping/inlining. Il2CppDumper dummy DLLs are declarations. BepInEx interop DLLs are managed bridges to native code. Ghidra recovers approximate C-like behavior. A complete, buildable copy of the original Unity project cannot be promised. Reconstruct important methods in readable C# with address, signature, hash, evidence, and confidence; use tests/runtime observations to validate them.

## Architecture decision

Keep the installed BepInEx IL2CPP loader initially: its existing log proves useful plugins still load. Separate the GUI/authoring layer from runtime transport. Avoid replacing a working injector merely because old patch targets changed. Native patches, a MelonLoader adapter, or a different injector are later options only if a reproducible BepInEx limitation requires them. Never run two loaders simultaneously.

1. **Analysis workspace:** fingerprint game, metadata and loader; index dump.cs; export Ghidra functions/pseudocode read-only; decompile managed community mods locally; compare signatures between builds.
2. **Authoring GUI:** search APIs, view declarations and pseudocode, edit C# project sources, generate a starter, compile, show diagnostics, stage a package, deploy/disable/restore.
3. **Runtime C# plugin:** isolated feature modules, complete signature resolution, capability probes, per-feature failure reporting, main-thread configuration reload, restoration of touched state.
4. **Compatibility layer:** game-hash-bound build receipt; reject deployment against changed references; version adapters rather than hardcoded native addresses.
5. **Configuration layer:** standard BepInEx .cfg for interoperability; structured controls for supported starter settings; generic controls generated from existing mods' setting comments, plus raw editing. DLL lifecycle changes require restart. Reload only settings supported by the plugin.

## Milestones and acceptance gates

### M0 — Evidence and project bootstrap

Inspect all supplied source projects and binary-only mod, installed logs, current metadata and Ghidra database. Record what is observed versus inferred. Initialize independent Git repository; exclude proprietary binaries and local exports. Gate: reproducible inventory with hashes, diagnosed failures, and explicit limitations.

### M1 — Readable analysis pipeline

Create dump.cs catalog (assembly, namespace, class, members, RVA), selected Ghidra export and full-function-index export; browse declarations and native pseudocode in the GUI. Decompile MKsKartersMods using installed dnSpy. Gate: verify Ghidra executable SHA-256 matches installed GameAssembly; preserve database; report export errors/timeouts instead of fabricated bodies.

### M2 — Usable authoring and management GUI

Provide game path selection, installation diagnostics, plugin inventory, enable/disable with dependency caveat, backup/restore of overwritten files, C# editing, build and managed deployment, existing config editing, structured starter controls, and log viewer. Gate: test path containment, config round trips, deployment receipt/hash checks, rollback and no accidental game execution. Native tkinter keeps this first release dependency-free on this machine; polished packaged Windows UI and richer code completion are later milestones.

### M3 — Starter customization plugin

Independent toggles for HUD scale, master volume, camera field of view, and an offline fast-fall experiment. Defaults preserve game behavior. Probe game signatures; use leaderboard and online-mode guards for physics. Reload config on Unity main thread; restore captured presentation values when disabled. Gate: compile against current local .NET 6 runtime and interop, then manual game validation for every feature and transition. Compilation alone is not runtime verification.

### M4 — Stable SDK facade and update migration

Introduce tested `GameSession`, `LocalPlayer`, `Kart`, `HUD`, `Audio`, `Camera` facades around verified adapters. Publish event subscriptions with disposal and subscriber isolation. Generate API migration reports (removed types, changed overloads, members); plugin declares required capabilities and supported fingerprints. Gate: deliberately missing/ambiguous methods disable one feature without aborting all other modules; baseline unchanged when disabled.

### M5 — Visual recipes and integrated editor

Offer recipes such as change race lap count, adjust drift boost, replace HUD text, set audio mix, and apply a camera preset. Generate normal documented C# from typed selections and ranges; allow external editors to modify the same files. Add syntax highlighting, search/replace, completion from current interop, compiler error navigation, package manifest and conflict diagnostics. Gate: GUI-generated and externally edited mods build/deploy identically; no assembly knowledge required.

### M6 — Live inspection and richer assets

Add opt-in in-game bridge with an authenticated local named pipe. Route commands through a bounded Unity main-thread queue. Expose scene hierarchy/components, current values, live preview, patch ownership, logs and per-feature health. Implement texture/audio/AssetBundle overrides as separate modules with lifetime and restore handling. Gate: stale handles rejected; no Unity access on background thread; disconnect restores preview state; large scans do not run every frame.

### M7 — Profiles, packages, distribution

Named profiles, mod dependency DAG, loader compatibility, package import/export, receipts, rollback, health summary, signed optional distribution, Windows packaging without requiring Python installation. Backup only owned files, never erase game folders. Gate: install/uninstall/rollback across a clean and existing modded installation; unknown community DLLs are never treated as safe to unload live.

## Manual test sequence

First boot with presentation features disabled; check startup log. Toggle UI/audio/FOV individually in menu and race; verify disable and scene transition restoration. For physics use an offline test race with leaderboard upload protection established; verify local-player-only behavior, controller input and pause state, then online transitions (feature must stop). Test invalid configs, updated hashes, failed target probes and competing mods. Do not label a mod working until these observations are recorded.

## Next work priorities

Finish local analysis and executable first slice before expanding. Then manual runtime verification, overload fix for NightmareAIs when its source is available/recovered, stable adapters, live bridge, and visual recipes. Full-game reconstruction is a separate long reverse-engineering effort; prioritize gameplay systems that unblock actual mods.

## Current delivery status

- M0 evidence and Git bootstrap: delivered.
- M1 metadata/native index, targeted pseudocode, address-specific export and managed recovery: delivered; full-game semantic reconstruction remains ongoing research.
- M2 initial GUI authoring, typed existing settings, build/deploy/toggle/backup workflows: delivered with core tests and off-screen smoke checks; visible interaction and distribution packaging remain.
- M3 starter source and build: delivered; in-game acceptance remains unverified.
- M4–M7 facades, migration UI, visual recipes, live bridge, richer assets, profiles and distribution: planned. They are not represented as working features in this release.

## Loader options

| Approach | Use in this project | Reason / tradeoff |
|---|---|---|
| Existing BepInEx IL2CPP | Initial runtime backend | Already loads useful mods; generated interop enables readable C#. Signature changes still need migration. |
| Native game content / Workshop pipeline | Investigate as companion backend | Current metadata exposes mod loaders/config/trigger systems. Formats and supported customization boundaries still need validation. |
| MelonLoader | Alternative only after a measured blocker | Similar managed/native bridge needs game compatibility work; switching does not fix obsolete class/method assumptions. Validate a separate installation and never combine loaders. |
| Custom native injector or binary patching | Last-resort scoped adapter | More maintenance, ABI/lifetime hazards and rollback burden. Keep native implementation behind typed C# facades; ordinary authors should not need assembly. |
| Complete reconstructed Unity project | Separate research effort | Original bodies, project structure and asset authoring metadata are not fully preserved. Not a prerequisite for the requested tool. |
