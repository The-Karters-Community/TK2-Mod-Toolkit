import { useMemo, useState } from "react";
import { CodeIcon, LockClosedIcon, ResetIcon } from "@radix-ui/react-icons";
import { useToolkit } from "../lib/useToolkit.jsx";
import { isEnabled, modulesFor } from "../lib/settings.js";
import { Button, Disclosure, Empty, Switch } from "../components/Controls.jsx";
import ModuleSettings from "../components/ModuleSettings.jsx";

export default function Mods() {
  const { state, busy, change, live, setLive, editModule, notify } =
    useToolkit();
  const [category, setCategory] = useState("All"),
    [query, setQuery] = useState(""),
    [enabledOnly, setEnabledOnly] = useState(false);
  const [open, setOpen] = useState({}),
    [allOpen, setAllOpen] = useState(false),
    [expansion, setExpansion] = useState(0);
  const modules = useMemo(() => modulesFor(state), [state]);
  const categories = [
    "All",
    ...new Set(modules.map((module) => module.category).sort()),
  ];
  const enabled = modules.filter((module) => isEnabled(state, module));
  const filtered = modules.filter(
    (module) =>
      (category === "All" || category === module.category) &&
      (!enabledOnly || isEnabled(state, module)) &&
      [
        module.name,
        module.description,
        ...(module.settings || []).map((setting) => setting[1]),
      ]
        .join(" ")
        .toLowerCase()
        .includes(query.toLowerCase().trim()),
  );
  const groups = new Map();
  filtered.forEach((module) => {
    const name = module.category;
    if (!groups.has(name)) groups.set(name, []);
    groups.get(name).push(module);
  });
  const disabled = !state.game || (busy && busy !== "settings");

  function toggle(module, value) {
    if (module.locked) return;
    const entry =
      module.field !== "values" &&
      module.entries.find((entry) => entry.key === "Enabled");
    if (module.field === "values" || entry)
      change(
        module.field,
        `${module.id}/Enabled`,
        module.field === "values" ? value : String(value),
      );
  }
  function reset(module) {
    if (module.locked) return;
    if (module.field === "values") {
      module.settings.forEach((setting) =>
        change("values", `${module.id}/${setting[0]}`, setting[3]),
      );
    } else
      module.entries.forEach((entry) => {
        if (entry.hasDefault !== false && entry.default !== undefined)
          change(module.field, `${module.id}/${entry.key}`, entry.default);
      });
    toggle(module, false);
    notify("Defaults staged. Save changes to apply.");
  }
  function expandAll(value) {
    setAllOpen(value);
    setExpansion((previous) => previous + 1);
    setOpen(Object.fromEntries(modules.map((module) => [module.id, value])));
  }

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1>Mod library</h1>
          <p className="mt-2 text-sm text-muted">
            Choose a module. Tune its settings. Keep playing.
          </p>
        </div>
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            className="checkbox"
            checked={live}
            onChange={(event) => setLive(event.target.checked)}
          />
          Apply as you adjust
        </label>
      </header>
      <div className="library-toolbar space-y-3">
        <div className="flex flex-wrap items-center gap-3">
          <input
            type="search"
            aria-label="Search modules and settings"
            placeholder="Search modules and settings…"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            className="field min-w-0 flex-1"
          />
          <Button
            aria-pressed={enabledOnly}
            className={enabledOnly ? "selected" : ""}
            onClick={() => setEnabledOnly(!enabledOnly)}
          >
            Enabled <span className="badge">{enabled.length}</span>
          </Button>
        </div>
        <div className="flex flex-wrap gap-1" aria-label="Module categories">
          {categories.map((name) => (
            <Button
              key={name}
              aria-pressed={name === category}
              className={"filter " + (category === name ? "selected" : "")}
              onClick={() => setCategory(name)}
            >
              {name}
            </Button>
          ))}
        </div>
        <div className="flex flex-wrap items-center justify-between gap-2 border-t border-line pt-3">
          <span className="text-xs text-muted">
            {filtered.length} of {modules.length} modules
          </span>
          <div className="flex flex-wrap gap-2">
            {enabledOnly && (
              <Button
                disabled={disabled || !enabled.some((module) => !module.locked)}
                onClick={() =>
                  enabled.forEach((module) => toggle(module, false))
                }
              >
                Disable enabled
              </Button>
            )}
            <Button onClick={() => expandAll(true)}>Expand all</Button>
            <Button onClick={() => expandAll(false)}>Collapse all</Button>
          </div>
        </div>
      </div>
      {!filtered.length && (
        <Empty title="No matching modules">
          Try a different search or switch off the Enabled filter.
        </Empty>
      )}
      {[...groups].map(([name, items]) => (
        <section key={name} className="space-y-3">
          <h2 className="flex items-center gap-3 text-base">
            {name}
            <span className="text-xs font-normal text-muted">
              {items.length}
            </span>
          </h2>
          <div className="space-y-2">
            {items.map((module) => {
              const active = isEnabled(state, module),
                expanded = open[module.id] ?? Boolean(query);
              const canToggle =
                module.field === "values" ||
                module.entries?.some((entry) => entry.key === "Enabled");
              const pack =
                state.packs?.find((pack) => pack.id === module.pack)?.name ||
                (module.pack === "recipes"
                  ? "Your recipes"
                  : "Additional settings");
              return (
                <Disclosure
                  key={module.id}
                  title={module.name}
                  description={module.description}
                  status={
                    module.locked
                      ? "Always on"
                      : !canToggle
                        ? "Settings"
                        : active
                          ? "On"
                          : "Off"
                  }
                  open={expanded}
                  onChange={(value) =>
                    setOpen((previous) => ({ ...previous, [module.id]: value }))
                  }
                  tools={
                    module.locked ? (
                      <span className="badge gap-1">
                        <LockClosedIcon />
                        Locked
                      </span>
                    ) : (
                      <>
                        {active && (
                          <Button
                            title="Reset module"
                            aria-label={"Reset " + module.name}
                            className="hidden px-2 sm:inline-flex"
                            disabled={disabled}
                            onClick={() => reset(module)}
                          >
                            <ResetIcon />
                          </Button>
                        )}
                        {canToggle && (
                          <Switch
                            label={"Enable " + module.name}
                            checked={active}
                            disabled={disabled || module.available === false}
                            onChange={(value) => toggle(module, value)}
                          />
                        )}
                      </>
                    )
                  }
                >
                  <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
                    <span className="text-xs text-muted">{pack}</span>
                    <div className="flex gap-2">
                      {!module.locked && (
                        <Button
                          disabled={disabled}
                          onClick={() => reset(module)}
                        >
                          Reset module
                        </Button>
                      )}
                      <Button
                        disabled={
                          Boolean(busy) ||
                          !state.moduleSources?.[module.id]?.length
                        }
                        onClick={() => editModule(module.id)}
                      >
                        <CodeIcon />
                        Edit code
                      </Button>
                    </div>
                  </div>
                  {module.available === false && (
                    <p className="mb-3 text-sm text-warning">
                      {module.reason ||
                        "This module needs a compatibility update."}
                    </p>
                  )}
                  <ModuleSettings
                    key={expansion}
                    module={module}
                    allOpen={allOpen}
                  />
                </Disclosure>
              );
            })}
          </div>
        </section>
      ))}
    </div>
  );
}
