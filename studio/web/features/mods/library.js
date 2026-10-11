import { $, el, notice } from "./../../shared/dom.js";
import { store } from "./../../shared/store.js";
import { editModule } from "./../workshop/editor.js";
import { featureSettings } from "./definitions.js";
import { bindAccordion, chevronIcon } from "./accordions.js";
import { switchControl, syncControls } from "./controls.js";
import { changeSetting } from "./settings-state.js";
import {
  extraPackId,
  featureGroup,
  packsForState,
  updateModuleCount,
} from "./catalog.js";
import { appendEntryGroups, extraGroupsForFilter } from "./recipes.js";

export function populateFeaturePanel(panel, feature, search) {
  if (!panel.dataset.lazyModule) return;
  delete panel.dataset.lazyModule;
  if (feature.available === false)
    panel.append(
      el(
        "p",
        "module-note",
        feature.reason ||
          "This module needs a compatibility update before it can be enabled.",
      ),
    );
  if (feature.status && feature.status !== "verified")
    panel.append(el("p", "module-note", feature.status));
  const moduleTools = el("div", "module-tools");
  moduleTools.append(
    el(
      "small",
      "",
      feature.locked
        ? "Mandatory protection stays on. Online actions are blocked while unapproved modules or plugins are active."
        : "Reset restores all defaults and switches this module off.",
    ),
  );
  panel.append(moduleTools);
  const edit = el("button", "secondary", "Edit code");
  edit.dataset.editModule = feature.id;
  edit.disabled =
    store.busy || !store.state.moduleSources?.[feature.id]?.length;
  edit.onclick = () => editModule(feature.id);
  moduleTools.append(edit);
  panel.append(featureSettings(feature, search));
}

export function renderFeatureCard(feature, search) {
  const unavailable = feature.available === false;
  const row = el("article", "module");
  row.dataset.feature = feature.id;
  const heading = el("div", "module-heading"),
    expand = el("button", "module-expand"),
    info = el("span", "module-info");
  info.append(el("span", "module-name", feature.name));
  if (feature.description)
    info.append(el("span", "module-description", feature.description));
  const enabled =
    feature.locked || Boolean(store.state.settings[feature.id + "/Enabled"]);
  const status = el(
    "span",
    "module-state",
    feature.locked ? "Always on" : enabled ? "On" : "Off",
  );
  status.dataset.moduleState = feature.id;
  const panel = el("div", "module-body");
  panel.id = "module-" + feature.id;
  panel.hidden = !(store.expandedModules.has(feature.id) || Boolean(search));
  panel.dataset.lazyModule = feature.id;
  expand.append(info, status, chevronIcon());
  bindAccordion(expand, panel, feature.id, !panel.hidden, () =>
    populateFeaturePanel(panel, feature, search),
  );
  const toggle = feature.locked
    ? el("span", "module-lock-badge", "Locked")
    : switchControl(
        "Enable " + feature.name,
        enabled,
        unavailable || (store.busy && store.actionInFlight !== "settings"),
        (value) => changeSetting(feature.id + "/Enabled", value),
      );
  if (!feature.locked)
    toggle.children[0].dataset.setting = feature.id + "/Enabled";
  const headerReset = el("button", "quiet module-reset", "Reset");
  headerReset.setAttribute(
    "aria-label",
    "Reset " + feature.name + " to defaults",
  );
  headerReset.hidden = Boolean(feature.locked);
  headerReset.onclick = () => resetFeatures([feature]);
  heading.append(expand, headerReset, toggle);
  row.append(heading, panel);
  return row;
}

export function renderFeatures() {
  const list = $("feature-list");
  list.replaceChildren();
  const search = $("mod-search").value.toLowerCase().trim();
  let count = 0,
    moduleCount = 0;
  packsForState().forEach((pack) => {
    const features = pack.features.filter((feature) => {
      const matches =
        store.category === "All" ||
        (store.category === "Enabled" &&
          (feature.locked || store.state.settings[feature.id + "/Enabled"])) ||
        store.category === featureGroup(feature);
      return (
        matches &&
        [
          pack.name,
          feature.name,
          feature.description,
          ...feature.settings.map((setting) => setting[1]),
        ]
          .join(" ")
          .toLowerCase()
          .includes(search)
      );
    });
    const extras = (store.state.extraSettings || []).filter(
      (entry) => extraPackId(entry) === pack.id,
    );
    const extraGroups = extraGroupsForFilter(extras, search, pack.name);
    if (!features.length && !extraGroups.size) return;
    count += features.length + extraGroups.size;
    moduleCount += features.length + extraGroups.size;
    const section = el("section", "mod-pack");
    section.dataset.pack = pack.id;
    const header = el("header", "pack-heading"),
      copy = el("div");
    copy.append(el("h2", "", pack.name));
    if (pack.description) copy.append(el("p", "", pack.description));
    const packActions = el("div", "pack-actions"),
      reset = el("button", "quiet", "Reset pack");
    reset.onclick = () => resetFeatures(pack.features);
    packActions.append(
      el("small", "", `${features.length + extraGroups.size} modules`),
      reset,
    );
    header.append(copy, packActions);
    section.append(header);
    const grouped = new Map();
    features.forEach((feature) => {
      const group = featureGroup(feature);
      if (!grouped.has(group)) grouped.set(group, []);
      grouped.get(group).push(feature);
    });
    grouped.forEach((groupFeatures, groupName) => {
      const categorySection = el("section", "feature-category");
      categorySection.dataset.featureCategory = groupName;
      const categoryHeading = el("header", "feature-category-heading");
      categoryHeading.append(
        el("h3", "", groupName),
        el(
          "small",
          "",
          `${groupFeatures.length} module${groupFeatures.length === 1 ? "" : "s"}`,
        ),
      );
      categorySection.append(categoryHeading);
      groupFeatures.forEach((feature) =>
        categorySection.append(renderFeatureCard(feature, search)),
      );
      section.append(categorySection);
    });
    appendEntryGroups(section, extraGroups, true);
    list.append(section);
  });
  store.visibleModules = moduleCount;
  updateModuleCount();
  if (!count)
    list.append(
      el(
        "p",
        "empty-state",
        store.category === "Enabled"
          ? "No modules are enabled. Choose All to add one."
          : "No settings match. Try another search or filter.",
      ),
    );
}

export function resetFeatures(features) {
  features
    .filter((feature) => !feature.locked)
    .forEach((feature) => {
      changeSetting(feature.id + "/Enabled", false);
      feature.settings.forEach((setting) =>
        changeSetting(feature.id + "/" + setting[0], setting[3]),
      );
    });
  syncControls();
  notice("Defaults restored. Save changes to apply.");
}
