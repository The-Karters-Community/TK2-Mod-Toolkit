# Mod log

## 2026-10-09 — Track inspector 0.6.15

- Kept the existing offline-only native-mask collider discovery, nearest-first cap, camera culling, X-ray and pinned-object readout.
- Added default-off translucent fills with back-face culling, scene depth testing, no depth writes, and an opacity control (default 0.12; range 0.03–0.35). Wireframe outlines draw after surfaces.
- Added an F7 clickable in-race settings window for wall/respawn visibility, surfaces, opacity, X-ray, distance and outline cap. It temporarily unlocks the cursor, offers reset/save, auto-saves live edits through the existing conflict-aware config manager, and restores the prior cursor state on close/Escape.
- Kept the compact HUD and keyboard shortcuts; made the view cap and range adjustable without leaving the race.
- Changed only the mod's visual helper objects/materials and inspector UI; physics colliders, masks and native renderers remain untouched.
- Evidence: plugin compiled against current installed references with 0 warnings and 0 errors; artifact hash matches its build manifest. In-game GUI/overlay appearance and performance remain unverified.
- Deployment is pending while the game process is active; do not overwrite its loaded plugin DLL.
