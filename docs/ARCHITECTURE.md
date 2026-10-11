# Where to change things

The toolkit has three parts: a React interface, a local Python service, and the BepInEx C# plugin. Vite compiles the interface into static files; the Windows host displays them through WebView2. There is no Node server in the released application.

## Repository layout

| Location | Responsibility |
| --- | --- |
| `studio/ui/` | React source and Vite configuration. |
| `studio/*.py` | Local API, desktop lifecycle, installation, catalog, updates and settings. |
| `plugins/TK2.Customization/` | Game behavior and the locked online-safety policy. |
| `templates/`, `src/Reconstructed/` | C# starters and selected readable game-behavior evidence. |
| `tools/`, `tests/`, `docs/` | Build/release/analysis commands, checks and guides. |
| `.agents/context/` | Product and design guidance. |
| `local/`, `exports/`, `artifacts/` | Ignored machine data, analysis exports and generated packages. |

Existing Python/plugin paths stay stable for CLI users, recipes and releases. The frontend's former event/view/CSS fragments have been replaced by the structure below. The duplicate legacy Tk interface is retired. Both `python -m studio` and `python launch.py` use `studio/webapp.py`.

## Interface ownership

```text
studio/ui/
  index.html
  vite.config.js
  package.json
  src/
    main.jsx                  Entry and error boundary
    App.jsx                   Navigation, updates and save/launch bar
    styles.css                Tailwind, theme and shared control styles
    components/
      Controls.jsx            Buttons, switches, disclosures and fields
      ModuleSettings.jsx      Parameters and override groups
    pages/
      Mods.jsx                Search, categories, enabled filter and modules
      Workshop.jsx            Source editor and tabs
      Reference.jsx           Managed API and native evidence explorer
      Sharing.jsx             Import/export
      Installation.jsx        Game, loader, plugin and backups
    lib/
      api.js                  Authenticated requests and presence
      settings.js             Pure draft, conflict and position calculations
      useToolkit.jsx          Shared state, polling, saving and preview
```

Tests sit beside the settings model and Mods page. Pages load on demand. Adding a catalog-backed module normally needs backend metadata and C#, not another page or stylesheet. Add components for reusable interactions or independently understandable features; keep related handlers with their page.

`useToolkit` owns the server snapshot and pending edits. Polling preserves edits; saving detects conflicts and keeps changes made while a save is in flight. Only eligible settings on enabled modules use optional debounced live preview. Enable/disable changes still require Save. Rebuilding React does not reload C# into a running game.

## Development and packaging

Run `npm ci` once **from the repository root**. `npm run build` writes ignored `studio/web-dist/`; `Launch Studio.cmd` rebuilds it before opening. For Fast Refresh, start `python launch.py --no-browser`, then `npm run dev` in another root terminal. Vite proxies the authenticated loopback API using the ignored session file. Restart Vite when the backend session changes.

Run `npm test`, `npm run format:check`, `npm run build`, and `python -m unittest discover -s tests`. Inspect wide and narrow windows in both themes. `python tools/smoke_garage.py` checks compiled references and a 500 KB raw UI budget; it does not prove visual layout or game behavior.

Packaging stages compiled UI, not React source or `node_modules`. Players need no npm, including after an update. Python, desktop hosting and Workshop material dominate the portable package size. Validate a relocated portable copy before release; a source build does not prove packaged startup.

Versions change only during an explicitly prepared release. Normal UI builds overwrite the stable generated directory. See [Contributing](../CONTRIBUTING.md) and [Releasing](RELEASING.md).
