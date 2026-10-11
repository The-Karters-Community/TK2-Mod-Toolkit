import { el, notice } from "./../../shared/dom.js";
import { store } from "./../../shared/store.js";
import { settingDisplayValue, updateSaveState } from "./settings-state.js";
import { renderCategoryFilters } from "./catalog.js";

export function switchControl(label, value, disabled, change) {
  const wrapper = el("label", "switch");
  const input = el("input");
  input.type = "checkbox";
  input.checked = value;
  input.disabled = disabled;
  input.setAttribute("aria-label", label);
  input.onchange = () => change(input.checked);
  wrapper.append(input, el("span"));
  return wrapper;
}

export function settingsControl(
  key,
  label,
  kind,
  value,
  low,
  high,
  description,
  choices,
  onChange,
  dataset = "setting",
  defaultValue,
) {
  const control = el("div", "setting"),
    copy = el("label", "setting-copy");
  copy.append(el("span", "setting-label", label));
  if (description) copy.append(el("small", "setting-description", description));
  const defaultText =
    defaultValue === ""
      ? "empty"
      : defaultValue === undefined
        ? "not supplied"
        : String(defaultValue);
  const allowed = choices?.length
    ? choices.length > 8
      ? choices.slice(0, 8).join(", ") + " … (all choices in dropdown)"
      : choices.join(", ")
    : kind === "bool"
      ? "on / off"
      : low !== undefined && low !== null
        ? `${low} to ${high}${kind === "int" ? " (whole numbers)" : ""}`
        : "single-line text";
  copy.append(
    el(
      "small",
      "setting-hint",
      `Default: ${defaultText} · Allowed: ${allowed}`,
    ),
  );
  control.append(copy);
  const input = el(choices?.length ? "select" : "input");
  if (choices?.length) {
    choices.forEach((choice) => {
      const option = el("option", "", String(choice));
      option.value = choice;
      input.append(option);
    });
    if (
      kind === "text" &&
      value !== undefined &&
      value !== null &&
      !choices.some((choice) => String(choice) === String(value))
    ) {
      const legacy = el("option", "", `Saved custom: ${value}`);
      legacy.value = value;
      input.append(legacy);
    }
  } else {
    input.type =
      kind === "text" ? "text" : kind === "bool" ? "checkbox" : "number";
    if (!["text", "bool"].includes(kind)) {
      if (low !== null && low !== undefined) input.min = low;
      if (high !== null && high !== undefined) input.max = high;
      input.step = kind === "int" ? "1" : "any";
    }
  }
  if (kind === "bool" && !choices?.length) input.checked = Boolean(value);
  else input.value = value;
  input.id = "setting-" + encodeURIComponent(key);
  copy.setAttribute("for", input.id);
  input.dataset[dataset] = key;
  input.disabled =
    !store.state.game || (store.busy && store.actionInFlight !== "settings");
  input.setAttribute("aria-label", label);
  const changed = () =>
    onChange(
      kind === "bool" && !choices?.length
        ? input.checked
        : ["text", "select"].includes(kind) || choices?.length
          ? input.value
          : input.value === ""
            ? ""
            : Number(input.value),
    );
  input.oninput = changed;
  input.onchange = changed;
  const tools = el("div", "value-tools"),
    reset = el("button", "value-reset", "Reset");
  reset.setAttribute("aria-label", "Reset " + label + " to default");
  reset.title = `Default: ${defaultText}`;
  reset.disabled = defaultValue === undefined || input.disabled;
  reset.onclick = () => {
    onChange(defaultValue);
    if (input.type === "checkbox") input.checked = Boolean(defaultValue);
    else input.value = defaultValue;
    syncControls();
    notice(
      label +
        " restored." +
        (store.livePreviewEnabled
          ? " Applying live preview."
          : " Save changes to apply."),
    );
  };
  tools.append(input, reset);
  input.linkedControls = [reset];
  control.field = input;
  control.tools = tools;
  if (
    ["float", "int"].includes(kind) &&
    low !== null &&
    low !== undefined &&
    high !== null &&
    high !== undefined &&
    Number(high) - Number(low) <= 10000
  ) {
    const slider = el("input", "value-slider");
    slider.type = "range";
    slider.min = low;
    slider.max = high;
    slider.step =
      kind === "int" ? 1 : Math.max(0.001, (Number(high) - Number(low)) / 1000);
    slider.value = value;
    slider.setAttribute("aria-label", label + " slider");
    slider.disabled = input.disabled;
    slider.oninput = () => {
      input.value = slider.value;
      changed();
    };
    input.oninput = () => {
      slider.value = input.value;
      changed();
    };
    input.slider = slider;
    input.linkedControls.push(slider);
    const stack = el("div", "value-stack");
    stack.append(tools, slider);
    control.append(stack);
  } else control.append(tools);
  input.defaultAvailable = defaultValue !== undefined;
  return control;
}

export function syncModuleState(id) {
  const feature = store.state.features.find((item) => item.id === id);
  document.querySelectorAll("[data-module-state]").forEach((node) => {
    if (node.dataset.moduleState === id)
      node.textContent = feature?.locked
        ? "Always on"
        : store.state.settings[id + "/Enabled"]
          ? "On"
          : "Off";
  });
}

export function syncDependencies() {
  document.querySelectorAll("[data-setting]").forEach((input) => {
    const feature = store.state.features.find(
      (item) => item.id === input.dataset.setting.split("/")[0],
    );
    input.disabled =
      !store.state.game ||
      (store.busy && store.actionInFlight !== "settings") ||
      feature?.available === false ||
      feature?.locked === true ||
      Boolean(
        input.dataset.requires && !store.state.settings[input.dataset.requires],
      );
    if (input.dataset.requires)
      input.setAttribute(
        "aria-description",
        input.disabled
          ? "Turn on this parameter’s override switch to edit its custom value."
          : "Custom override value.",
      );
  });
  document
    .querySelectorAll("[data-setting], [data-recipe], [data-extra]")
    .forEach((input) => {
      (input.linkedControls || []).forEach((link) => {
        link.disabled =
          link.tagName === "BUTTON"
            ? !store.state.game ||
              (store.busy && store.actionInFlight !== "settings") ||
              !input.defaultAvailable
            : input.disabled;
      });
      if (input.slider && input.slider !== document.activeElement)
        input.slider.value = input.value;
    });
}

export function syncControls() {
  document.querySelectorAll("[data-setting]").forEach((input) => {
    const value = settingDisplayValue(input.dataset.setting);
    if (input === document.activeElement || value === undefined) return;
    if (input.type === "checkbox") input.checked = Boolean(value);
    else input.value = value;
  });
  ["recipe", "extra"].forEach((kind) => {
    document.querySelectorAll("[data-" + kind + "]").forEach((input) => {
      const entry = (
        store.state[kind === "extra" ? "extraSettings" : "recipes"] || []
      ).find(
        (entry) => entry.section + "/" + entry.key === input.dataset[kind],
      );
      if (entry && input !== document.activeElement) {
        if (input.type === "checkbox")
          input.checked = entry.value.toLowerCase() === "true";
        else input.value = entry.value;
      }
    });
  });
  store.state.features.forEach((feature) => syncModuleState(feature.id));
  syncDependencies();
  renderCategoryFilters();
  updateSaveState();
}
