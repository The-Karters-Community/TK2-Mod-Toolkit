import { store } from "./../../shared/store.js";
import { $ } from "./../../shared/dom.js";
import { syncControls, syncDependencies, syncModuleState } from "./controls.js";
import { renderCategoryFilters } from "./catalog.js";
import { renderFeatures } from "./library.js";
import { renderRecipes } from "./recipes.js";
import { runAction } from "./../../shared/lifecycle.js";

export function updateSaveState() {
  const count =
    store.settingEdits.size + store.recipeEdits.size + store.extraEdits.size;
  store.settingsDirty = count > 0;
  $("save-settings").disabled = store.busy || !store.settingsDirty;
  $("launch-game").disabled = store.busy || !store.state?.game;
  $("save-settings").textContent =
    store.actionInFlight === "settings" ? "Saving…" : "Save changes";
  $("settings-state").textContent = count
    ? `${count} unsaved setting${count === 1 ? "" : "s"}${store.livePreviewEnabled ? " · eligible edits auto-save after a short pause." : "."}`
    : "All changes saved.";
}

export function recordEdit(edits, key, before, value) {
  const previous = edits.get(key),
    base = previous ? previous.base : before;
  // A reverted key stays dirty while an older value is being saved.
  if (Object.is(base, value) && store.actionInFlight !== "settings")
    edits.delete(key);
  else edits.set(key, { base, value, revision: ++store.revision });
  updateSaveState();
}

const gamepadPresetCoordinates = {
  TopLeft: [0, 0],
  TopCenter: [50, 0],
  TopRight: [100, 0],
  MiddleLeft: [0, 50],
  MiddleCenter: [50, 50],
  MiddleRight: [100, 50],
  BottomLeft: [0, 100],
  BottomCenter: [50, 100],
  BottomRight: [100, 100],
};

export function settingDisplayValue(key) {
  const axis =
    key === "GamepadOverlay/PositionX"
      ? 0
      : key === "GamepadOverlay/PositionY"
        ? 1
        : -1;
  const preset =
    axis >= 0
      ? gamepadPresetCoordinates[store.state.settings["GamepadOverlay/Corner"]]
      : undefined;
  return preset ? preset[axis] : store.state.settings[key];
}

export function changeSetting(key, value, updateLibrary = true) {
  let changes = [[key, value]];
  if (key === "GamepadOverlay/Corner") {
    const coordinates = gamepadPresetCoordinates[value] ||
      gamepadPresetCoordinates[
        store.state.settings["GamepadOverlay/Corner"]
      ] || [
        store.state.settings["GamepadOverlay/PositionX"] ?? 50,
        store.state.settings["GamepadOverlay/PositionY"] ?? 0,
      ];
    changes = [
      [key, value],
      ["GamepadOverlay/PositionX", coordinates[0]],
      ["GamepadOverlay/PositionY", coordinates[1]],
    ];
  } else if (
    key === "GamepadOverlay/PositionX" ||
    key === "GamepadOverlay/PositionY"
  ) {
    const x = key.endsWith("PositionX")
      ? value
      : (settingDisplayValue("GamepadOverlay/PositionX") ?? 50);
    const y = key.endsWith("PositionY")
      ? value
      : (settingDisplayValue("GamepadOverlay/PositionY") ?? 0);
    changes = [
      ["GamepadOverlay/Corner", "Custom"],
      ["GamepadOverlay/PositionX", x],
      ["GamepadOverlay/PositionY", y],
    ];
  }
  changes.forEach(([changedKey, changedValue]) => {
    recordEdit(
      store.settingEdits,
      changedKey,
      store.state.settings[changedKey],
      changedValue,
    );
    store.state.settings[changedKey] = changedValue;
  });
  syncModuleState(key.split("/")[0]);
  syncDependencies();
  if (
    key.startsWith("GamepadOverlay/") &&
    ["Corner", "PositionX", "PositionY"].some((name) =>
      key.endsWith("/" + name),
    )
  )
    syncControls();
  if (updateLibrary && key.endsWith("/Enabled")) {
    renderCategoryFilters();
    if (store.category === "Enabled") {
      renderFeatures();
      renderRecipes();
    }
  } else if (updateLibrary)
    changes.forEach(([changedKey]) =>
      scheduleLivePreview(changedKey, store.settingEdits),
    );
}

export function changeEntry(entry, value, extra = false, updateLibrary = true) {
  recordEdit(
    extra ? store.extraEdits : store.recipeEdits,
    entry.section + "/" + entry.key,
    entry.value,
    value,
  );
  entry.value = value;
  if (updateLibrary && entry.key === "Enabled") {
    renderCategoryFilters();
    if (store.category === "Enabled") {
      renderFeatures();
      renderRecipes();
    }
  } else if (updateLibrary)
    scheduleLivePreview(
      entry.section + "/" + entry.key,
      extra ? store.extraEdits : store.recipeEdits,
    );
}

export function scheduleLivePreview(key, edits) {
  if (!store.livePreviewEnabled || key.endsWith("/Enabled")) return;
  if (!edits.has(key) || !livePreviewEligible(key)) return;
  clearTimeout(store.livePreviewTimer);
  store.livePreviewTimer = setTimeout(() => {
    const keys = new Set(
      [
        ...store.settingEdits.keys(),
        ...store.recipeEdits.keys(),
        ...store.extraEdits.keys(),
      ].filter(livePreviewEligible),
    );
    if (!keys.size) return;
    if (store.busy) {
      scheduleLivePreview(key, edits);
      return;
    }
    const snapshot = settingsSnapshot(keys);
    return runAction("settings", snapshot.body, snapshot);
  }, 600);
}

export function livePreviewEligible(key) {
  if (key.endsWith("/Enabled")) return false;
  const section = key.slice(0, key.lastIndexOf("/"));
  const feature = store.state.features.find((item) => item.id === section);
  return feature
    ? !feature.locked &&
        feature.available !== false &&
        store.state.settings[section + "/Enabled"] === true
    : [
        ...(store.state.recipes || []),
        ...(store.state.extraSettings || []),
      ].some(
        (entry) =>
          entry.section === section &&
          entry.key === "Enabled" &&
          entry.value.toLowerCase() === "true",
      );
}

export function mergeState(next) {
  store.settingEdits.forEach((edit, key) => {
    next.settings[key] = edit.value;
  });
  [
    ["recipes", store.recipeEdits],
    ["extraSettings", store.extraEdits],
  ].forEach(([field, edits]) => {
    (next[field] || []).forEach((entry) => {
      const edit = edits.get(entry.section + "/" + entry.key);
      if (edit) entry.value = edit.value;
    });
  });
  store.state = next;
}

export function settingsSnapshot(onlyKeys) {
  const body = {
      values: {},
      baseValues: {},
      recipes: {},
      baseRecipes: {},
      extraSettings: {},
      baseExtraSettings: {},
      hash: store.state.configHash,
    },
    revisions = {};
  [
    ["values", "baseValues", store.settingEdits],
    ["recipes", "baseRecipes", store.recipeEdits],
    ["extraSettings", "baseExtraSettings", store.extraEdits],
  ].forEach(([values, bases, edits]) => {
    revisions[values] = new Map();
    edits.forEach((edit, key) => {
      if (onlyKeys && !onlyKeys.has(key)) return;
      body[values][key] = edit.value;
      body[bases][key] = edit.base;
      revisions[values].set(key, edit.revision);
    });
  });
  return { body, revisions };
}

export function acknowledgeSettings(snapshot, result) {
  // Newer keystrokes survive older responses and rebase against saved values.
  [
    ["values", store.settingEdits],
    ["recipes", store.recipeEdits],
    ["extraSettings", store.extraEdits],
  ].forEach(([field, edits]) => {
    snapshot.revisions[field].forEach((savedRevision, key) => {
      const current = edits.get(key);
      if (!current) return;
      if (
        current.revision === savedRevision ||
        Object.is(current.value, snapshot.body[field][key])
      )
        edits.delete(key);
      else current.base = snapshot.body[field][key];
    });
  });
  if (result.settings)
    mergeState({
      ...store.state,
      ...result,
      settings: { ...result.settings },
      recipes: result.recipes || store.state.recipes,
      extraSettings: result.extraSettings || store.state.extraSettings,
    });
  store.conflictKeys = [];
  $("settings-error").hidden = true;
  $("reload-settings").hidden = true;
  updateSaveState();
}

export function settingsError(error) {
  store.conflictKeys =
    error.details?.conflicts || error.details?.conflictKeys || [];
  if (!Array.isArray(store.conflictKeys))
    store.conflictKeys = Object.keys(store.conflictKeys);
  const conflict =
    store.conflictKeys.length > 0 ||
    error.details?.code === "settings_conflict";
  $("settings-error-message").textContent = conflict
    ? `${error.message} Your edits are still here. Reload the conflicting values, then adjust them again.`
    : `${error.message} Your edits are still here. Check the values and try saving again.`;
  $("settings-error").hidden = false;
  $("reload-settings").hidden = !conflict;
}
