# TK2 Mod Toolkit plan

## Target and route

- Target: The Karters 2 Turbo Charged, Unity IL2CPP, player build 0.1.4.18.
- Existing mod route: BepInEx IL2CPP plugin built against the game's installed interop assemblies.
- Current work: improve the offline-only track-boundary inspector used to investigate authored invisible-wall and respawn colliders.
- Source of truth: shipped collision masks and the current matching native `GameAssembly.dll`; do not infer game behavior from stub declarations.

## Scope and safety

- Visualize only colliders selected by the local kart's wall and respawn masks. Never modify collider state, physics masks, game camera masks, or original materials.
- Keep the feature behind its default-off module setting and existing offline/race-state guard. Do not run or install over a live game process.
- Bound overlay cost with nearest-first collider selection, a user-configurable cap, and camera-space culling. Default to scene-occluded outlines and optional low-opacity fills; provide deliberate X-ray and pin controls.
- Keep normal performance captures separate from the inspector, since diagnostic rendering adds work.

## Acceptance evidence

- Build with the current installed game's references.
- For live acceptance, open the F7 panel, adjust surface visibility/opacity, category filters, range and cap, verify Escape restores the cursor, compare depth-tested and X-ray views, inspect mesh/box/sphere/capsule boundaries, pin a collider under screen center, then hide/disable the module and confirm cleanup.
- Capture a screenshot and plugin log. Compilation alone does not certify in-game rendering or performance.
- For performance diagnosis, compare the same track, settings, camera and race conditions with the inspector off; separate AI/physics cost from GPU or memory evidence.
