# Mod log

## 2026-10-09 — Track inspector 0.6.15

- Kept the existing offline-only native-mask collider discovery, nearest-first cap, camera culling, X-ray and pinned-object readout.
- Added default-off translucent fills with back-face culling, scene depth testing, no depth writes, and an opacity control (default 0.12; range 0.03–0.35). Wireframe outlines draw after surfaces.
- Added an F7 clickable in-race settings window for wall/respawn visibility, surfaces, opacity, X-ray, distance and outline cap. It temporarily unlocks the cursor, offers reset/save, auto-saves live edits through the existing conflict-aware config manager, and restores the prior cursor state on close/Escape.
- Kept the compact HUD and keyboard shortcuts; made the view cap and range adjustable without leaving the race.
- Changed only the mod's visual helper objects/materials and inspector UI; physics colliders, masks and native renderers remain untouched.
- Evidence: plugin compiled against current installed references with 0 warnings and 0 errors; artifact hash matches its build manifest. In-game GUI/overlay appearance and performance remain unverified.
- Deployment is pending while the game process is active; do not overwrite its loaded plugin DLL.

## Track inspector UX simplification 0.6.16 (2026-10-09)

- Removed X-ray and the F9 control. F10 now opens/closes the visual inspector and its settings together; F7 is no longer a separate options mode. The inspector starts closed and restores the previous cursor state on close.
- Clarified that orange marks colliders selected by the kart's native respawn masks. F8 only pins a displayed collider for inspection and does not affect classification or physics.
- Added a scope note for respawn areas absent from the view: separate generic trigger volumes are not automatically labeled respawn because the available native evidence does not establish which trigger types respawn the kart.
- Replaced default translucent IMGUI backgrounds and toggles in the inspector and camera panels with a solid dark panel and high-contrast custom checkbox states.
- Plugin version 0.6.16 built against current installed references with 0 warnings and 0 errors. `git diff --check` passed. The game is still running, so the new DLL was not installed; live UI behavior remains to be confirmed in an offline race.
