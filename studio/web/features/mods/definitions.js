import { el } from "./../../shared/dom.js";
import { settingsControl, switchControl, syncControls } from "./controls.js";
import { changeSetting, settingDisplayValue } from "./settings-state.js";
import { store } from "./../../shared/store.js";

export function parameterGroup(feature, key) {
  key = key.replace(/^Override/, "");
  if (feature.id === "BoostParameters") {
    if (/Pad/.test(key)) return "Boost pads";
    if (/Air|Landing/.test(key)) return "Air and landing";
    if (/Reserves|Triple|Continuous/.test(key)) return "Reserves and speed";
    return "Drift boost timing and strength";
  }
  if (/InstantBoost/.test(key)) return "Boost delivery";
  if (/Brak|Break|Friction|Drag/.test(key)) return "Braking and friction";
  if (/Steer|Drift|Counter/.test(key)) return "Steering and drifting";
  if (/Jump|Vertical/.test(key)) return "Jump and vertical motion";
  if (/Mass/.test(key)) return "Kart body";
  return "Speed and acceleration";
}

export function featureSettings(feature, search) {
  const container = el("div");
  const required = new Set(
    feature.settings.map((setting) => setting[8]).filter(Boolean),
  );
  const groups = new Map();
  feature.settings
    .filter((setting) => !required.has(setting[0]))
    .forEach((setting) => {
      const group =
        feature.settingGroups?.[setting[0]] ||
        (feature.settings.length >= 16
          ? parameterGroup(feature, setting[0])
          : "");
      if (!groups.has(group)) groups.set(group, []);
      groups.get(group).push(setting);
    });
  groups.forEach((settings, name) => {
    const controls = el("div", "settings-grid");
    settings.forEach(
      ([
        key,
        label,
        kind,
        fallback,
        low,
        high,
        description,
        choices,
        requires,
      ]) => {
        const fullKey = feature.id + "/" + key;
        const control = settingsControl(
          fullKey,
          label,
          kind,
          settingDisplayValue(fullKey) ?? fallback,
          low,
          high,
          description,
          choices,
          (value) => changeSetting(fullKey, value),
          "setting",
          fallback,
        );
        if (feature.locked)
          control.querySelectorAll("input,select,button").forEach((input) => {
            input.disabled = true;
          });
        if (requires) {
          const requireKey = requires.includes("/")
            ? requires
            : feature.id + "/" + requires;
          const input = control.field;
          input.dataset.requires = requireKey;
          input.disabled =
            (store.busy && store.actionInFlight !== "settings") ||
            !store.state.settings[requireKey];
          const toggle = switchControl(
            "Override " + label,
            Boolean(store.state.settings[requireKey]),
            store.busy && store.actionInFlight !== "settings",
            (value) => changeSetting(requireKey, value),
          );
          toggle.children[0].dataset.setting = requireKey;
          const paired = el("div", "parameter-controls"),
            resetOverride = el("button", "value-reset", "Reset override");
          resetOverride.onclick = () => {
            changeSetting(requireKey, false);
            syncControls();
          };
          paired.append(toggle, resetOverride);
          control.prepend
            ? control.insertBefore(paired, control.children[1])
            : control.append(paired);
        }
        controls.append(control);
      },
    );
    if (name) {
      const details = el("details", "parameter-group"),
        identity = feature.id + ":" + name;
      details.dataset.accordionKey = identity;
      details.open =
        store.expandedModules.has(identity) ||
        Boolean(
          search &&
            settings.some((setting) =>
              [
                name,
                setting[0],
                setting[1],
                setting[6],
                setting[8],
                setting[8] ? "Override " + setting[1] : "",
              ]
                .join(" ")
                .toLowerCase()
                .includes(search),
            ),
        );
      details.ontoggle = () => {
        if (details.open) store.expandedModules.add(identity);
        else store.expandedModules.delete(identity);
      };
      details.append(el("summary", "", name), controls);
      container.append(details);
    } else container.append(controls);
  });
  if (!feature.settings.length && feature.available !== false)
    container.append(
      el("p", "module-note", "This module has no additional settings."),
    );
  return container;
}
