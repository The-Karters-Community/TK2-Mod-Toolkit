import { lazy, Suspense, useEffect, useRef, useState } from "react";
import { useToolkit } from "../lib/useToolkit.jsx";
import { Button, Disclosure, Empty } from "../components/Controls.jsx";

const Reference = lazy(() => import("./Reference.jsx"));
const Sharing = lazy(() => import("./Sharing.jsx"));
const tabs = {
  editor: "Module editor",
  api: "Game API",
  sharing: "Share modules",
  tutorial: "First mod",
};

export default function Workshop() {
  const toolkit = useToolkit();
  const {
    state,
    source,
    setSource,
    sourceDirty,
    loadSource,
    moduleId,
    setModuleId,
    action,
    busy,
    notify,
  } = toolkit;
  const [tab, setTab] = useState("editor"),
    [query, setQuery] = useState(""),
    [name, setName] = useState(""),
    [find, setFind] = useState(""),
    [buildOpen, setBuildOpen] = useState(false);
  useEffect(() => setTab("editor"), [moduleId]);
  const code = useRef(null),
    numbers = useRef(null),
    tabButtons = useRef({});
  const blocked = Boolean(busy);
  const allowed = state.moduleSources?.[moduleId];
  const files = (state.files || []).filter(
    (file) =>
      (!allowed || allowed.includes(file)) &&
      (moduleId !== "reconstructed" || file.startsWith("src/Reconstructed/")) &&
      file.toLowerCase().includes(query.toLowerCase()),
  );
  const lines = source?.content.split("\n").length || 1;

  function create(recipe) {
    if (sourceDirty)
      return notify("Save your C# before creating another recipe.", true);
    action("create-recipe", { name: recipe }, async (result) => {
      setModuleId("Recipe." + recipe);
      setTab("editor");
      await loadSource(result.file);
    });
  }
  function selectModule(id) {
    if (sourceDirty) return notify("Save C# before switching modules.", true);
    setModuleId(id);
    if (state.moduleSources?.[id]?.length)
      loadSource(state.moduleSources[id][0]);
  }
  function save() {
    action(
      "save-source",
      { file: source.file, hash: source.hash, content: source.content },
      (result) =>
        setSource((latest) => ({
          ...latest,
          hash: result.hash,
          base: source.content,
        })),
    );
  }
  function search() {
    if (!source || !find) return;
    const position = code.current.selectionEnd || 0;
    let index = source.content
      .toLowerCase()
      .indexOf(find.toLowerCase(), position);
    if (index < 0)
      index = source.content.toLowerCase().indexOf(find.toLowerCase());
    if (index < 0) return notify("No match in this file.");
    code.current.focus();
    code.current.setSelectionRange(index, index + find.length);
  }
  function tabKeyboard(event, id) {
    const names = Object.keys(tabs),
      index = names.indexOf(id);
    const next =
      event.key === "ArrowRight"
        ? names[(index + 1) % names.length]
        : event.key === "ArrowLeft"
          ? names[(index + names.length - 1) % names.length]
          : event.key === "Home"
            ? names[0]
            : event.key === "End"
              ? names.at(-1)
              : null;
    if (next) {
      event.preventDefault();
      setTab(next);
      tabButtons.current[next]?.focus();
    }
  }

  return (
    <div className="space-y-5">
      <header>
        <h1>Workshop</h1>
        <p className="mt-2 text-sm text-muted">
          Edit C#, inspect game APIs, or share a module.
        </p>
      </header>
      <div
        role="tablist"
        aria-label="Workshop tools"
        className="flex flex-wrap gap-1 border-b border-line pb-3"
      >
        {Object.entries(tabs).map(([id, label]) => (
          <Button
            key={id}
            ref={(node) => (tabButtons.current[id] = node)}
            id={"tab-" + id}
            role="tab"
            aria-selected={tab === id}
            aria-controls={"panel-" + id}
            tabIndex={tab === id ? 0 : -1}
            className={"filter " + (tab === id ? "selected" : "")}
            onClick={() => setTab(id)}
            onKeyDown={(event) => tabKeyboard(event, id)}
          >
            {label}
          </Button>
        ))}
      </div>
      <div
        id="panel-editor"
        role="tabpanel"
        aria-labelledby="tab-editor"
        hidden={tab !== "editor"}
        className="space-y-4"
      >
        <div className="flex flex-wrap items-end gap-3">
          <label className="min-w-0 flex-1 space-y-2 text-sm">
            <span>Create a recipe</span>
            <input
              aria-label="Recipe name"
              className="field block w-full"
              placeholder="MyKartHop"
              value={name}
              onChange={(event) => setName(event.target.value)}
            />
          </label>
          <Button
            disabled={blocked || !name.trim()}
            onClick={() => create(name.trim())}
          >
            Create recipe
          </Button>
          <Button disabled={blocked} onClick={() => action("build")}>
            Build pack
          </Button>
          <Button
            primary
            disabled={blocked}
            onClick={() => action("build-install")}
          >
            Build & install
          </Button>
        </div>
        <p className="text-xs text-muted">
          Save C# → close the game → build and install → restart the game
          yourself. Config edits reload live.
        </p>
        <div className="grid min-w-0 gap-4 lg:grid-cols-[230px_minmax(0,1fr)]">
          <aside className="min-w-0 space-y-3">
            <label className="block space-y-2 text-sm">
              <span>Module source</span>
              <select
                className="field w-full"
                value={moduleId}
                disabled={blocked}
                onChange={(event) => selectModule(event.target.value)}
              >
                <option value="all">All project files</option>
                <option value="reconstructed">Reconstructed game logic</option>
                {state.features.map((module) => (
                  <option key={module.id} value={module.id}>
                    {module.name}
                  </option>
                ))}
                {Object.keys(state.moduleSources || {})
                  .filter(
                    (id) =>
                      id.startsWith("Recipe.") &&
                      !state.features.some((module) => module.id === id),
                  )
                  .map((id) => (
                    <option key={id} value={id}>
                      {id.slice(7)}
                    </option>
                  ))}
              </select>
            </label>
            <input
              type="search"
              aria-label="Filter source files"
              className="field w-full"
              placeholder="Filter files…"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
            />
            <div className="max-h-80 overflow-auto rounded-lg border border-line lg:max-h-[540px]">
              {files.map((file) => (
                <button
                  type="button"
                  key={file}
                  title={file}
                  className="result-row"
                  aria-pressed={source?.file === file}
                  disabled={blocked}
                  onClick={() => loadSource(file)}
                >
                  <span className="block break-all text-sm">
                    {file.split("/").at(-1)}
                  </span>
                  <span className="text-xs text-muted">
                    {file.startsWith("src/")
                      ? "Reconstructed behavior"
                      : file.includes("/Recipes/")
                        ? "Recipe"
                        : "Module / shared code"}
                  </span>
                </button>
              ))}
            </div>
          </aside>
          <section className="min-w-0 space-y-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <p className="min-w-0 break-all text-xs text-muted">
                {source?.file || "Choose a source file"}
              </p>
              <div className="flex flex-wrap gap-2">
                <Button
                  disabled={blocked || !source}
                  onClick={() =>
                    action("open-source-folder", { file: source.file })
                  }
                >
                  Open folder
                </Button>
                <Button
                  disabled={blocked || !source}
                  onClick={() => {
                    if (
                      !sourceDirty ||
                      confirm(
                        "Discard these unsaved C# edits and reload the file?",
                      )
                    )
                      loadSource(source.file, true);
                  }}
                >
                  Reload file
                </Button>
                <Button
                  primary
                  disabled={blocked || !sourceDirty}
                  onClick={save}
                >
                  Save C#
                </Button>
              </div>
            </div>
            <div className="flex items-center gap-2">
              <input
                aria-label="Find in file"
                className="field min-w-0 flex-1"
                value={find}
                placeholder="Find in file…"
                onChange={(event) => setFind(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === "Enter") search();
                }}
              />
              <Button disabled={!source || !find} onClick={search}>
                Next match
              </Button>
            </div>
            {source ? (
              <div className="flex h-[480px] min-w-0 overflow-hidden rounded-lg border border-line bg-panel">
                <pre
                  ref={numbers}
                  aria-hidden="true"
                  className="!m-0 shrink-0 !rounded-none !border-0 !bg-transparent !py-3 !pr-2 text-right text-muted"
                  style={{ whiteSpace: "pre", overflow: "hidden" }}
                >
                  {Array.from({ length: lines }, (_, i) => i + 1).join("\n")}
                </pre>
                <textarea
                  ref={code}
                  aria-label="Editable C# source"
                  spellCheck={false}
                  value={source.content}
                  readOnly={blocked}
                  className="min-w-0 flex-1 resize-none bg-transparent p-3 font-mono text-xs leading-6 outline-none"
                  style={{ whiteSpace: "pre", tabSize: 4 }}
                  onScroll={(event) =>
                    (numbers.current.scrollTop = event.target.scrollTop)
                  }
                  onChange={(event) =>
                    setSource({ ...source, content: event.target.value })
                  }
                  onKeyDown={(event) => {
                    if (event.key !== "Tab") return;
                    event.preventDefault();
                    const start = event.target.selectionStart,
                      end = event.target.selectionEnd;
                    setSource({
                      ...source,
                      content:
                        source.content.slice(0, start) +
                        "    " +
                        source.content.slice(end),
                    });
                    requestAnimationFrame(() =>
                      code.current.setSelectionRange(start + 4, start + 4),
                    );
                  }}
                />
              </div>
            ) : (
              <Empty title="Choose a C# file">
                Select a module or recipe on the left to edit its source.
              </Empty>
            )}
            <p className="text-xs text-muted">
              {source
                ? `${sourceDirty ? "Unsaved edits" : "File saved"} · ${lines} lines · Ctrl+S saves`
                : "Editable source stays beside the application."}
            </p>
          </section>
        </div>
        <Disclosure
          title="Compiler output"
          open={buildOpen}
          onChange={setBuildOpen}
        >
          <pre>{state.buildLog || "No compiler run yet."}</pre>
        </Disclosure>
      </div>
      <Suspense fallback={<div className="skeleton h-40" aria-busy="true" />}>
        {tab === "api" && (
          <div id="panel-api" role="tabpanel" aria-labelledby="tab-api">
            <Reference
              onSource={(file) => {
                setModuleId("reconstructed");
                setTab("editor");
                loadSource(file);
              }}
            />
          </div>
        )}
        {tab === "sharing" && (
          <div id="panel-sharing" role="tabpanel" aria-labelledby="tab-sharing">
            <Sharing />
          </div>
        )}
      </Suspense>
      {tab === "tutorial" && (
        <div
          id="panel-tutorial"
          role="tabpanel"
          aria-labelledby="tab-tutorial"
          className="max-w-3xl space-y-5"
        >
          <h2>Your first mod: a kart hop</h2>
          <p className="text-sm text-muted">
            The template gives your local kart an upward impulse on F9, with
            configurable strength and offline checks.
          </p>
          <Button
            primary
            disabled={blocked || sourceDirty}
            onClick={() => create("MyKartHop")}
          >
            Create MyKartHop recipe
          </Button>
          <ol className="list-decimal space-y-4 pl-5 text-sm">
            <li>Create the recipe and open it in Module editor.</li>
            <li>
              In Configure, change the strength from 6f to 8f. Configure
              declares settings; Tick runs while enabled; Restore undoes
              persistent changes.
            </li>
            <li>
              Inspect AddVelocity under Game API → Kart & driving. Declarations
              alone do not prove behavior.
            </li>
            <li>
              Save C#, close the game, then Build & install. Start the game
              yourself, enable My Kart Hop in the library and save.
            </li>
          </ol>
          <pre>ReadableGame.AddVelocity(kart, Vector3.up * _hop.Value);</pre>
          <p className="text-sm text-muted">
            Author builds need the .NET SDK and initialized BepInEx interop.
            Failed builds preserve the installed DLL. Physics, item grants and
            arbitrary recipes remain protected from online use.
          </p>
          <Button
            onClick={() => {
              setTab("editor");
              toolkit.editModule("Recipe.FrameLimiter");
            }}
          >
            Open frame limiter example
          </Button>
        </div>
      )}
    </div>
  );
}
