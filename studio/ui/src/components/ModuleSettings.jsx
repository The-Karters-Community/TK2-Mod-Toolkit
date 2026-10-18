import { useState } from "react";
import { useToolkit } from "../lib/useToolkit.jsx";
import { displayValue, parameterGroup } from "../lib/settings.js";
import { Disclosure, Setting } from "./Controls.jsx";

export default function ModuleSettings({ module, allOpen }) {
  const { state, change, busy } = useToolkit();
  const [open, setOpen] = useState({});
  const disabled =
    !state.game ||
    (busy && busy !== "settings") ||
    module.locked ||
    module.available === false;
  const groups = new Map();
  const presentation =
    module.field === "values" &&
    ["Camera", "GamepadOverlay"].includes(module.id);
  const required = new Set(
    (module.settings || []).map((setting) => setting[8]).filter(Boolean),
  );
  const settings =
    module.field === "values"
      ? module.settings.filter((setting) => !required.has(setting[0]))
      : module.entries.filter((entry) => entry.key !== "Enabled");
  settings.forEach((setting) => {
    let group =
      module.field === "values" ? parameterGroup(module, setting[0]) : "";
    if (presentation) {
      group =
        module.id === "Camera"
          ? group || "Framing"
          : ["Corner", "PositionX", "PositionY"].includes(setting[0])
            ? "Placement"
            : ["Scale", "Opacity"].includes(setting[0])
              ? "Size & visibility"
              : "Playback & shortcut";
    }
    if (!groups.has(group)) groups.set(group, []);
    groups.get(group).push(setting);
  });
  function control(setting) {
    if (module.field !== "values") {
      const bounds = /^From (.+) to (.+)$/.exec(setting.range || "");
      return (
        <Setting
          key={setting.key}
          label={setting.key.replace(/([a-z])([A-Z])/g, "$1 $2")}
          kind={
            setting.type === "Int32"
              ? "int"
              : ["Single", "Double"].includes(setting.type)
                ? "float"
                : "text"
          }
          value={setting.value}
          description={setting.description}
          choices={
            setting.type === "Boolean" ? ["false", "true"] : setting.choices
          }
          low={bounds ? Number(bounds[1]) : null}
          high={bounds ? Number(bounds[2]) : null}
          fallback={setting.hasDefault === false ? undefined : setting.default}
          disabled={disabled}
          onChange={(value) =>
            change(module.field, `${module.id}/${setting.key}`, String(value))
          }
        />
      );
    }
    const [
      key,
      label,
      kind,
      fallback,
      low,
      high,
      description,
      choices,
      requires,
    ] = setting;
    const requireKey =
      requires &&
      (requires.includes("/") ? requires : `${module.id}/${requires}`);
    return (
      <Setting
        key={key}
        compact={presentation}
        label={label}
        kind={kind}
        value={displayValue(state, `${module.id}/${key}`) ?? fallback}
        fallback={fallback}
        low={low}
        high={high}
        description={description}
        choices={choices}
        disabled={
          disabled ||
          Boolean(requireKey && !state.settings[requireKey]) ||
          (module.id === "Camera" &&
            key === "TargetHeight" &&
            !state.settings["Camera/AimAtKart"])
        }
        override={
          requireKey && {
            checked: state.settings[requireKey],
            disabled,
            onChange: (value) => change("values", requireKey, value),
          }
        }
        onChange={(value) => change("values", `${module.id}/${key}`, value)}
      />
    );
  }
  if (!settings.length)
    return <p className="text-sm text-muted">No additional settings.</p>;
  if (presentation) {
    // These small presentation modules should not be a stack of nested accordions.
    const columns =
      module.id === "GamepadOverlay"
        ? [["Placement"], ["Size & visibility", "Playback & shortcut"]]
        : [
            ["Framing", "In-race panel"],
            ["Aim & rotation", "Transitions"],
          ];
    const assigned = new Set(columns.flat());
    columns[1].push(
      ...[...groups.keys()].filter((group) => !assigned.has(group)),
    );
    return (
      <div className="presentation-settings">
        {columns.map((column, index) => (
          <div className="presentation-column" key={index}>
            {column
              .filter((group) => groups.has(group))
              .map((group) => (
                <fieldset className="presentation-section" key={group}>
                  <legend>{group}</legend>
                  {group === "Placement" && (
                    <p className="presentation-note">
                      Choose a screen position, or adjust either axis to place
                      it yourself.
                    </p>
                  )}
                  {group === "Framing" && (
                    <p className="presentation-note">
                      Keep kart size compensates distance when you change the
                      field of view.
                    </p>
                  )}
                  <div className="presentation-fields">
                    {groups.get(group).map(control)}
                  </div>
                </fieldset>
              ))}
          </div>
        ))}
      </div>
    );
  }
  return (
    <div className="space-y-3">
      {[...groups].map(([group, items]) => {
        const fields = (
          <div className="grid grid-cols-1 gap-x-8 gap-y-6 py-2 lg:grid-cols-2">
            {items.map(control)}
          </div>
        );
        return group ? (
          <Disclosure
            key={group}
            title={group}
            open={open[group] ?? allOpen}
            onChange={(value) =>
              setOpen((previous) => ({ ...previous, [group]: value }))
            }
          >
            {fields}
          </Disclosure>
        ) : (
          <div key="general">{fields}</div>
        );
      })}
    </div>
  );
}
