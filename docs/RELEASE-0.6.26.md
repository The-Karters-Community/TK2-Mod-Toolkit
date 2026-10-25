# TK2 Mod Toolkit 0.6.26

- Fix repeated Unity interop allocations in inactive parameter overrides, including when optional modules are disabled. Original values still restore when overrides are turned off. In a local six-second comparison, identity lookups fell from 345,201 to 12,710 (96.3%); this is a toolkit overhead fix, not a measured reduction in the game's total RAM requirement.
- Add experimental Air Dash, bounded practice rewind/scrubbing and expanded snapshots, with selectable keyboard and controller shortcuts.
- Correct mine-trap placement/firing, lateral movement and exact runtime hook signatures. Remove the unreliable Ricochet Items experiment.
- Expand opt-in performance diagnostics with engine timing and a one-time memory census.

Gameplay modules default off and are intended for offline use. Mandatory online/submission protection remains enabled. Experimental aerial/practice behavior and live Mirror Race rendering still require further gameplay validation. This release does not lower texture quality or apply the discarded low-memory experiment.

Windows x64 portable package; extract the ZIP into a writable folder and launch **TK2 Mod Toolkit.exe**. Existing users can use the app's updater. The package does not bundle the game or BepInEx loader; install the supported loader separately if needed. Source builds require development tools; portable users do not need Python or Node.js.

See [the performance investigation](PERFORMANCE-FIX-2026-10-25.md) and [aerial/practice controls and limitations](AERIAL-PRACTICE.md).
