import { $, notice } from "./../../shared/dom.js";
import { renderFeatures } from "./library.js";
import { renderRecipes } from "./recipes.js";
import { store } from "./../../shared/store.js";
import { renderCategoryFilters } from "./catalog.js";
import {
  changeEntry,
  changeSetting,
  settingsError,
  settingsSnapshot,
} from "./settings-state.js";
import { setAllAccordions } from "./accordions.js";
import { refresh, runAction } from "./../../shared/lifecycle.js";

export function bindModEvents() {
  $("mod-search").oninput = () => {
    renderFeatures();
    renderRecipes();
  };
  $("apply-adjustments").onclick = () => {
    store.livePreviewEnabled = !store.livePreviewEnabled;
    localStorage.setItem("tk2-live-preview", String(store.livePreviewEnabled));
    if (!store.livePreviewEnabled) clearTimeout(store.livePreviewTimer);
    renderCategoryFilters();
    notice(
      store.livePreviewEnabled
        ? "Live preview is on for edits to enabled modules. Module on/off changes still need Save changes."
        : "Live preview is off. All edits wait for Save changes.",
    );
  };
  $("show-enabled").onclick = () => {
    store.category = store.category === "Enabled" ? "All" : "Enabled";
    renderCategoryFilters();
    renderFeatures();
    renderRecipes();
  };
  $("disable-all-enabled").onclick = () => {
    if (store.busy) return;
    let disabled = 0;
    store.state.features
      .filter(
        (feature) =>
          !feature.locked && store.state.settings[feature.id + "/Enabled"],
      )
      .forEach((feature) => {
        changeSetting(feature.id + "/Enabled", false, false);
        disabled++;
      });
    ["extraSettings", "recipes"].forEach((field) => {
      (store.state[field] || []).forEach((entry) => {
        if (
          entry.key === "Enabled" &&
          entry.type === "Boolean" &&
          entry.value.toLowerCase() === "true"
        ) {
          changeEntry(entry, "false", field === "extraSettings", false);
          disabled++;
        }
      });
    });
    renderCategoryFilters();
    renderFeatures();
    renderRecipes();
    notice(
      `${disabled} unlocked module${disabled === 1 ? "" : "s"} disabled. Online protection remains on; save changes to apply.`,
    );
  };
  $("expand-all-accordions").onclick = () => setAllAccordions(true);
  $("collapse-all-accordions").onclick = () => setAllAccordions(false);
  $("save-settings").onclick = () => {
    const invalid = [
      ...document.querySelectorAll(
        "[data-setting], [data-recipe], [data-extra]",
      ),
    ].find((input) => {
      const dirty = input.dataset.setting
        ? store.settingEdits.has(input.dataset.setting)
        : input.dataset.recipe
          ? store.recipeEdits.has(input.dataset.recipe)
          : store.extraEdits.has(input.dataset.extra);
      return dirty && !input.checkValidity();
    });
    if (invalid) {
      settingsError(
        new Error(
          `${invalid.getAttribute("aria-label") || "A setting"} is outside its allowed range${invalid.min && invalid.max ? ` (${invalid.min} to ${invalid.max})` : ""}.`,
        ),
      );
      return;
    }
    const snapshot = settingsSnapshot();
    return runAction("settings", snapshot.body, snapshot);
  };
  $("reload-settings").onclick = async () => {
    if (
      !confirm(
        "Discard your edits to the conflicting settings and reload their current values? Other edits will be kept.",
      )
    )
      return;
    if (store.conflictKeys.length)
      store.conflictKeys.forEach((key) => {
        store.settingEdits.delete(key);
        store.recipeEdits.delete(key);
        store.extraEdits.delete(key);
      });
    else {
      store.settingEdits.clear();
      store.recipeEdits.clear();
      store.extraEdits.clear();
    }
    try {
      await refresh(true);
      store.conflictKeys = [];
      $("settings-error").hidden = true;
      $("reload-settings").hidden = true;
    } catch (error) {
      settingsError(error);
    }
  };
}
