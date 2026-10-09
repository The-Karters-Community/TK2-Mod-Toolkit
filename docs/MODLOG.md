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

## Toolkit interface and vignette 0.6.17 (2026-10-09)

- Removed the fixed 1450px application content cap and made the main view fill the app window. Wide settings panes use three columns; smaller windows retain the existing one/two-column breakpoints.
- Replaced the font glyph accordion indicator with a centered SVG chevron that rotates in place. Shifted the light/dark app palette toward the game's lavender, charcoal and orange interface colors, and display the Toolkit app version beside the supported game version.
- Added the default-off `DisableVignette` visual module. It suppresses `Vignette.IsEnabledAndSupported` while active and leaves profile intensities untouched, so the game's aspect-adaptive vignette settings resume when disabled.
- Native evidence: read-only Ghidra 12.1.4 analysis of the existing `GameAssembly.dll` program found `Ant_MapData$$RefreshAspectAdaptiveVignette` at `0x1805d3e80`, which finds `PostProcessVolume` instances, retrieves `Vignette` settings from their profiles, and caches intensity values. `UnityEngine.Rendering.PostProcessing.Vignette$$IsEnabledAndSupported` at `0x1830aae30` takes a `PostProcessRenderContext` and checks the effect's enabled state and mode parameters. The mod prefixes that exact method signature; source and interop build validation is separate from runtime verification.
- Plugin built against the installed interop references with 0 warnings and 0 errors. No live game was launched, so vignette rendering and UI appearance still need an in-game check before installing this DLL.
