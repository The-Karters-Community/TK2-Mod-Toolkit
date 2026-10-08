# TK2 Mod SDK

Keep original game binaries, Ghidra projects, and third-party mods intact. Generated game data and recovered third-party sources belong in ignored `local/` or `exports/`, never in Git. Record evidence and limitations in docs. Never describe dummy DLL declarations or interop wrapper bodies as recovered game implementation. Resolve Harmony targets by complete signature. Default gameplay-changing features off; keep them disabled in online modes. Use current local BepInEx references, not old Prologue assemblies. Commit focused milestones. Run Python tests, plugin build, and GUI smoke checks after relevant changes. Do not launch a game automatically as a test.

Current user policy for the 2026-10-08 test build: gameplay features may be enabled for local offline testing without Disable Leaderboards. Do not install upload-blocking hooks or require leaderboard protection to enable these test features. Defaults remain off; online gameplay guards remain.
