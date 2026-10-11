import { renderCategoryFilters } from "../features/mods/catalog.js";
import { renderFeatures } from "../features/mods/library.js";
import { renderRecipes } from "../features/mods/recipes.js";
import {
  loadSource,
  renderFiles,
  saveSource,
  showWorkshopTab,
} from "../features/workshop/editor.js";
import { renderInstallation } from "../features/installation/view.js";
import {
  renderSharing,
  updateSharingButtons,
} from "../features/workshop/sharing.js";
import { syncControls, syncDependencies } from "../features/mods/controls.js";
import {
  acknowledgeSettings,
  mergeState,
  settingsError,
  updateSaveState,
} from "../features/mods/settings-state.js";
import { store } from "./store.js";
import { api } from "./api.js";
import { $, notice } from "./dom.js";

export function render() {
  renderCategoryFilters();
  renderFeatures();
  renderRecipes();
  renderFiles();
  renderInstallation();
  renderSharing();
  syncDependencies();
  updateSaveState();
}

export async function refresh(preserveSettings = true, quiet = false) {
  const sequence = ++store.refreshSequence,
    next = await api("state");
  if (sequence !== store.refreshSequence) return;
  const structure = (value) => [
    value.features,
    value.packs,
    value.files,
    ...["recipes", "extraSettings"].map((field) =>
      (value[field] || []).map((entry) => entry.section + "/" + entry.key),
    ),
  ];
  const changed =
    !store.state ||
    JSON.stringify(structure(next)) !== JSON.stringify(structure(store.state));
  if (!preserveSettings) {
    store.settingEdits.clear();
    store.recipeEdits.clear();
    store.extraEdits.clear();
  }
  mergeState(next);
  if (!quiet || changed) render();
  else {
    syncControls();
    renderInstallation();
  }
}

export function setBusy(value) {
  store.busy = value;
  [
    "workshop-install",
    "build",
    "create-recipe",
    "diagnose",
    "prepare-loader",
    "installation-install",
    "plugin-update-top",
    "toolkit-update-top",
    "scan-games",
    "select-game",
    "browse-game",
    "browse-package",
    "preview-package",
  ].forEach((id) => {
    $(id).disabled = value;
  });
  $("save-source").disabled = value || !store.sourceDirty;
  $("code").readOnly = value || !store.source;
  $("reload-source").disabled = value || !store.source;
  $("open-source-folder").disabled = value || !store.source;
  updateSaveState();
  document.querySelectorAll("[data-edit-module]").forEach((button) => {
    button.disabled =
      value || !store.state?.moduleSources?.[button.dataset.editModule]?.length;
  });
  // Keep controls mounted and editable during a settings save.
  document
    .querySelectorAll("[data-setting], [data-recipe], [data-extra]")
    .forEach((input) => {
      const feature = store.state?.features.find(
        (item) => item.id === input.dataset.setting?.split("/")[0],
      );
      input.disabled =
        (value && store.actionInFlight !== "settings") ||
        feature?.available === false ||
        !store.state?.game;
    });
  syncDependencies();
  if (store.state) {
    renderInstallation();
    renderCategoryFilters();
    updateSharingButtons();
  }
}

export async function runAction(action, body = {}, snapshot) {
  if (store.busy) return;
  store.actionInFlight = action;
  setBusy(true);
  if (action !== "settings")
    notice(
      action === "build" || action === "build-install"
        ? "Compiling your plugin…"
        : action === "install"
          ? "Installing the bundled plugin…"
          : "Working…",
    );
  try {
    if (action === "build" || action === "build-install") await saveSource();
    const result = await api(action, body);
    if (action === "settings") {
      acknowledgeSettings(snapshot, result);
      syncControls();
      // The write already succeeded. A failed follow-up poll must not turn it
      // into a misleading save error; the idle poll will retry.
      await refresh(true, true).catch(() => {});
      renderCategoryFilters();
      renderFeatures();
      renderRecipes();
    } else if (action === "diagnose") {
      await refresh(true, true);
      notice(result.message);
    } else {
      await refresh(true);
      notice(result.message || "Done");
    }
    if (action === "create-recipe") {
      store.selectedModule = "Recipe." + body.name;
      showWorkshopTab("editor");
      await loadSource(result.file);
    }
  } catch (error) {
    if (action === "settings") settingsError(error);
    else notice(error.message, true);
  } finally {
    store.actionInFlight = undefined;
    setBusy(false);
    if (action !== "settings") await refresh(true, true).catch(() => {});
  }
}
