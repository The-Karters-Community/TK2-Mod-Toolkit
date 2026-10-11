import { store } from "./../../shared/store.js";
import { $, el, notice } from "./../../shared/dom.js";
import { bindAccordion, chevronIcon } from "./accordions.js";
import { settingsControl, switchControl, syncControls } from "./controls.js";
import { changeEntry } from "./settings-state.js";
import { editModule } from "./../workshop/editor.js";
import { updateModuleCount } from "./catalog.js";

export function extraGroupsForFilter(entries, search, packName = "") {
  const groups = new Map();
  entries.forEach((entry) => {
    if (!groups.has(entry.section)) groups.set(entry.section, []);
    groups.get(entry.section).push(entry);
  });
  groups.forEach((settings, name) => {
    const enabled =
      settings.find((entry) => entry.key === "Enabled")?.value === "true";
    if (
      (store.category === "Enabled" && !enabled) ||
      (store.category !== "All" &&
        store.category !== "Enabled" &&
        store.category !== "Gameplay") ||
      ![
        packName,
        name,
        ...settings.flatMap((entry) => [entry.key, entry.description]),
      ]
        .join(" ")
        .toLowerCase()
        .includes(search)
    )
      groups.delete(name);
  });
  return groups;
}

export function appendEntryGroups(container, groups, extra = false) {
  groups.forEach((settings, name) => {
    const title = name
      .replace(/^(Recipe\.|MK\.|Community\.)/, "")
      .replace(/([a-z])([A-Z])/g, "$1 $2");
    const row = el("article", "module"),
      heading = el("div", "module-heading"),
      expand = el("button", "module-expand");
    const info = el("span", "module-info");
    info.append(el("span", "module-name", title));
    const enabled = settings.find(
      (entry) => entry.key === "Enabled" && entry.type === "Boolean",
    );
    if (enabled?.description)
      info.append(el("span", "module-description", enabled.description));
    const panel = el("div", "module-body");
    panel.id = "section-" + name.replace(/[^A-Za-z0-9_-]/g, "-");
    panel.hidden = !(
      store.expandedModules.has(name) || Boolean($("mod-search").value)
    );
    expand.append(info, chevronIcon());
    bindAccordion(expand, panel, name, !panel.hidden);
    heading.append(expand);
    if (enabled) {
      const toggle = switchControl(
        "Enable " + title,
        enabled.value.toLowerCase() === "true",
        store.busy && store.actionInFlight !== "settings",
        (value) => changeEntry(enabled, String(value), extra),
      );
      toggle.children[0].dataset[extra ? "extra" : "recipe"] =
        enabled.section + "/" + enabled.key;
      heading.append(toggle);
    }
    row.append(heading);
    const controls = el("div", "settings-grid");
    settings
      .filter((entry) => entry !== enabled)
      .forEach((entry) => {
        const bounds = /^From (.+) to (.+)$/.exec(entry.range || ""),
          kind =
            entry.type === "Boolean"
              ? "select"
              : entry.type === "Int32"
                ? "int"
                : ["Single", "Double"].includes(entry.type)
                  ? "float"
                  : "text";
        controls.append(
          settingsControl(
            entry.section + "/" + entry.key,
            entry.key.replace(/([a-z])([A-Z])/g, "$1 $2"),
            kind,
            entry.value,
            bounds?.[1],
            bounds?.[2],
            entry.description,
            entry.type === "Boolean" ? ["false", "true"] : entry.choices,
            (value) => changeEntry(entry, String(value), extra),
            extra ? "extra" : "recipe",
            entry.hasDefault === false ? undefined : entry.default,
          ),
        );
      });
    if (settings.length === 1 && enabled)
      controls.append(
        el("p", "module-note", "This module has no additional settings."),
      );
    const reset = el("button", "quiet", "Reset module to defaults");
    reset.onclick = () => {
      settings.forEach((entry) => {
        if (entry.default !== undefined && entry.hasDefault !== false)
          changeEntry(entry, entry.default, extra);
      });
      syncControls();
      notice("Defaults restored. Save changes to apply.");
    };
    reset.className = "quiet module-reset";
    reset.textContent = "Reset";
    reset.setAttribute("aria-label", "Reset " + title + " to defaults");
    heading.append(reset);
    const tools = el("div", "module-tools");
    const edit = el("button", "secondary", "Edit code");
    edit.dataset.editModule = name;
    edit.disabled = store.busy || !store.state.moduleSources?.[name]?.length;
    edit.onclick = () => editModule(name);
    tools.append(edit);
    panel.append(tools, controls);
    row.append(panel);
    container.append(row);
  });
}

export function renderRecipes() {
  const list = $("recipe-controls");
  list.replaceChildren();
  const entries = store.state.recipes || [];
  store.visibleRecipeModules = 0;
  if (!entries.length) {
    updateModuleCount();
    return;
  }
  const search = $("mod-search").value.toLowerCase().trim(),
    groups = extraGroupsForFilter(entries, search, "Your recipes");
  store.visibleRecipeModules = groups.size;
  if (!groups.size) {
    updateModuleCount();
    return;
  }
  const section = el("section", "mod-pack"),
    header = el("header", "pack-heading");
  header.append(el("h2", "", "Your recipes"));
  section.append(header);
  appendEntryGroups(section, groups);
  list.append(section);
  updateModuleCount();
}
