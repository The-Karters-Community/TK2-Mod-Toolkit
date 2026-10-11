import { useState } from "react";
import { useToolkit } from "../lib/useToolkit.jsx";
import { downloadPackage } from "../lib/api.js";
import { Button } from "../components/Controls.jsx";

export default function Sharing() {
  const { state, busy, action, change, sourceDirty, notify } = useToolkit();
  const [selected, setSelected] = useState(new Set()),
    [name, setName] = useState("My TK2 modules"),
    [path, setPath] = useState(""),
    [preview, setPreview] = useState(null),
    [replace, setReplace] = useState(false);
  const blocked = Boolean(busy);
  function toggle(id) {
    setSelected((previous) => {
      const next = new Set(previous);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }
  function importSettings(values, latest = state) {
    Object.entries(values || {}).forEach(([key, value]) => {
      if (Object.hasOwn(latest.settings, key)) change("values", key, value);
      else
        for (const field of ["recipes", "extraSettings"])
          if (
            latest[field]?.some(
              (entry) => `${entry.section}/${entry.key}` === key,
            )
          )
            change(field, key, String(value));
    });
    setPreview(null);
  }
  function author(command, body, after) {
    if (sourceDirty)
      return notify("Save C# before importing or exporting source.", true);
    action(command, body, after);
  }
  return (
    <div className="grid gap-8 xl:grid-cols-2">
      <section className="space-y-4">
        <h2>Export modules</h2>
        <p className="text-sm text-muted">
          Share selected Toolkit source and settings, without game binaries.
        </p>
        <label className="block space-y-2 text-sm">
          <span>Package name</span>
          <input
            className="field w-full"
            value={name}
            onChange={(event) => setName(event.target.value)}
          />
        </label>
        <div className="flex gap-2">
          <Button
            onClick={() =>
              setSelected(new Set(state.features.map((module) => module.id)))
            }
          >
            Select all
          </Button>
          <Button onClick={() => setSelected(new Set())}>
            Clear selection
          </Button>
          <span className="self-center text-xs text-muted">
            {selected.size} selected
          </span>
        </div>
        <div className="max-h-80 space-y-3 overflow-auto rounded-lg border border-line p-4">
          {state.features.map((module) => (
            <label key={module.id} className="flex items-center gap-3 text-sm">
              <input
                type="checkbox"
                className="checkbox"
                checked={selected.has(module.id)}
                onChange={() => toggle(module.id)}
              />
              {module.name}
            </label>
          ))}
        </div>
        <Button
          primary
          disabled={blocked || !selected.size}
          onClick={() =>
            author(
              "export-package",
              { ids: [...selected], name: name.trim(), values: state.settings },
              downloadPackage,
            )
          }
        >
          Export package
        </Button>
      </section>
      <section className="space-y-4">
        <h2>Import modules</h2>
        <p className="text-sm text-muted">
          Preview a package before applying source changes. Imported modules
          start off.
        </p>
        <label className="block space-y-2 text-sm">
          <span>Package file</span>
          <input
            className="field w-full"
            value={path}
            onChange={(event) => {
              setPath(event.target.value);
              setPreview(null);
            }}
            placeholder="Path to a Toolkit module ZIP"
          />
        </label>
        <div className="flex flex-wrap gap-2">
          <Button
            disabled={blocked}
            onClick={() =>
              action("browse-file", { kind: "package" }, (result) => {
                if (result.path) {
                  setPath(result.path);
                  setPreview(null);
                }
              })
            }
          >
            Browse…
          </Button>
          <Button
            disabled={blocked || !path.trim()}
            onClick={() =>
              action("preview-package", { path: path.trim() }, (result) => {
                setPreview(result);
                setReplace(false);
              })
            }
          >
            Preview package
          </Button>
        </div>
        {preview && (
          <div className="space-y-3 rounded-lg border border-line p-4">
            <h3>{preview.modules.length} modules</h3>
            <ul className="list-disc space-y-1 pl-5 text-sm">
              {preview.modules.map((module, i) => (
                <li key={i}>
                  {typeof module === "string"
                    ? module
                    : module.name || module.id}
                </li>
              ))}
            </ul>
            <details>
              <summary className="text-sm">
                Source files ({preview.files?.length || 0})
              </summary>
              <ul className="space-y-1 pt-2 text-xs text-muted">
                {(preview.files || []).map((file, i) => (
                  <li key={i} className="break-all">
                    {typeof file === "string" ? file : file.path || file.name}
                  </li>
                ))}
              </ul>
            </details>
            {preview.conflicts?.length > 0 && (
              <p className="text-sm text-warning">
                {preview.conflicts.length} existing files differ. Shared source
                can affect other modules.
              </p>
            )}
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                className="checkbox"
                checked={replace}
                onChange={(event) => setReplace(event.target.checked)}
              />
              Replace conflicting source files
            </label>
            <Button
              primary
              disabled={
                blocked || Boolean(preview.conflicts?.length && !replace)
              }
              onClick={() =>
                author(
                  "import-package",
                  { path: preview.path, hash: preview.hash, replace },
                  (result, latest) => importSettings(result.settings, latest),
                )
              }
            >
              Import reviewed package
            </Button>
          </div>
        )}
      </section>
    </div>
  );
}
