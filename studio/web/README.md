# Frontend map

The app uses native ES modules bundled by Vite. `app.js` only starts presence tracking, binds feature events, selects the initial view and polls backend state. It is not a place for new feature implementations.

| Location | Responsibility |
| --- | --- |
| `features/mods/catalog.js` | Catalog filters, counts and grouping |
| `features/mods/library.js` | Module cards and settings panels |
| `features/mods/controls.js` | Setting inputs and synchronization |
| `features/mods/settings-state.js` | Draft edits, save acknowledgements and live preview |
| `features/mods/accordions.js` | Expand/collapse state |
| `features/mods/definitions.js`, `recipes.js` | Settings selection, parameter groups and recipes |
| `features/workshop/editor.js` | File selection, editing and source saves |
| `features/workshop/game-api-browser.js` | Read-only game API and pseudocode reference |
| `features/workshop/sharing.js` | Module import/export |
| `features/installation/view.js` | Installation diagnostics and update presentation |
| Each feature's `events.js` | Event binding for that feature |
| `features/shell/` | Navigation, theme, keyboard shortcuts and save bar |
| `shared/store.js` | Explicit shared application state; no implicit global variables |
| `shared/api.js` | Authenticated API requests and window presence |
| `shared/lifecycle.js` | Refresh/render orchestration and backend actions |
| `shared/dom.js` | Small DOM and notification helpers |
| `views/` | Readable HTML fragments, organized by page |
| `styles/` | Styles organized by responsibility |
| `tooling/html-templates.js` | Vite view composition and development reloads |

`index.html` is the shell. `<!-- @include ./relative-file.html -->` assembles HTML fragments. Vite uses the template plugin; the Python source-mode server uses `studio/frontend.py` with the same include syntax. Includes must stay inside this source directory. `styles/index.css` preserves import order so the existing cascade remains predictable.

Keep feature logic with its owner. Small DOM fragments such as a setting row can be created in JavaScript; whole page layouts belong in `views/`. Use `store` for shared state, explicit imports for dependencies and event-binding functions called from the bootstrap. Avoid top-level DOM side effects in imported modules. The lifecycle module coordinates existing features, so its import graph includes cycles; do not read feature state during module initialization.

The compiled output is ignored `studio/web-dist/`. Never edit it: `npm run build` replaces it. `npm run format` formats maintained source, while `npm run format:check` verifies formatting. Development and backend setup are in [Contributing](../../CONTRIBUTING.md).
