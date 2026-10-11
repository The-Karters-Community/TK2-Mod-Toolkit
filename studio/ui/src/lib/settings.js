// Pure draft model. UI rendering and HTTP requests do not belong here.
export const positions = {
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
export const createDraft = (server) => ({
  server,
  revision: 0,
  edits: { values: {}, recipes: {}, extraSettings: {} },
});

export function effectiveState(draft) {
  const server = draft.server;
  if (!server) return null;
  const settings = { ...server.settings };
  Object.entries(draft.edits.values).forEach(
    ([key, edit]) => (settings[key] = edit.value),
  );
  const state = { ...server, settings };
  for (const field of ["recipes", "extraSettings"]) {
    state[field] = (server[field] || []).map((entry) => ({
      ...entry,
      value:
        draft.edits[field][`${entry.section}/${entry.key}`]?.value ??
        entry.value,
    }));
  }
  return state;
}

export function displayValue(state, key) {
  const axis =
    key === "GamepadOverlay/PositionX"
      ? 0
      : key === "GamepadOverlay/PositionY"
        ? 1
        : -1;
  const preset = positions[state.settings["GamepadOverlay/Corner"]];
  return axis >= 0 && preset ? preset[axis] : state.settings[key];
}

export function editDraft(draft, field, key, value, saving = false) {
  const state = effectiveState(draft);
  let changes = [[key, value]];
  if (field === "values" && key === "GamepadOverlay/Corner") {
    const coordinates = positions[value] ||
      positions[state.settings[key]] || [
        state.settings["GamepadOverlay/PositionX"] ?? 50,
        state.settings["GamepadOverlay/PositionY"] ?? 0,
      ];
    changes = [
      [key, value],
      ["GamepadOverlay/PositionX", coordinates[0]],
      ["GamepadOverlay/PositionY", coordinates[1]],
    ];
  } else if (field === "values" && /GamepadOverlay\/Position[XY]$/.test(key)) {
    changes = [
      ["GamepadOverlay/Corner", "Custom"],
      [
        "GamepadOverlay/PositionX",
        key.endsWith("X")
          ? value
          : displayValue(state, "GamepadOverlay/PositionX"),
      ],
      [
        "GamepadOverlay/PositionY",
        key.endsWith("Y")
          ? value
          : displayValue(state, "GamepadOverlay/PositionY"),
      ],
    ];
  }
  const entries = { ...draft.edits[field] };
  let revision = draft.revision;
  for (const [changed, next] of changes) {
    const original =
      field === "values"
        ? state.settings[changed]
        : state[field].find(
            (entry) => `${entry.section}/${entry.key}` === changed,
          )?.value;
    const base = entries[changed] ? entries[changed].base : original;
    if (Object.is(base, next) && !saving) delete entries[changed];
    else entries[changed] = { base, value: next, revision: ++revision };
  }
  return { ...draft, revision, edits: { ...draft.edits, [field]: entries } };
}

export function snapshot(draft, predicate = () => true) {
  const body = {
    values: {},
    baseValues: {},
    recipes: {},
    baseRecipes: {},
    extraSettings: {},
    baseExtraSettings: {},
    hash: draft.server.configHash,
  };
  const revisions = {};
  for (const [field, base] of [
    ["values", "baseValues"],
    ["recipes", "baseRecipes"],
    ["extraSettings", "baseExtraSettings"],
  ]) {
    revisions[field] = {};
    Object.entries(draft.edits[field]).forEach(([key, edit]) => {
      if (!predicate(key)) return;
      body[field][key] = edit.value;
      body[base][key] = edit.base;
      revisions[field][key] = edit.revision;
    });
  }
  return { body, revisions };
}

export function acknowledge(draft, saved, result) {
  const edits = {};
  for (const field of ["values", "recipes", "extraSettings"]) {
    edits[field] = { ...draft.edits[field] };
    Object.entries(saved.revisions[field]).forEach(([key, revision]) => {
      const current = edits[field][key];
      if (!current) return;
      if (
        current.revision === revision ||
        Object.is(current.value, saved.body[field][key])
      )
        delete edits[field][key];
      else edits[field][key] = { ...current, base: saved.body[field][key] };
    });
  }
  return { ...draft, edits, server: { ...draft.server, ...result } };
}

export const editCount = (draft) =>
  Object.values(draft.edits).reduce(
    (count, edits) => count + Object.keys(edits).length,
    0,
  );
export const isEnabled = (state, module) =>
  module.locked ||
  (module.field === "values"
    ? state.settings[`${module.id}/Enabled`] === true
    : module.entries?.some(
        (e) => e.key === "Enabled" && e.value.toLowerCase() === "true",
      ));

export function modulesFor(state) {
  const modules = (state.features || []).map((feature) => ({
    ...feature,
    field: "values",
    category: feature.gameplay ? "Gameplay" : feature.category || "Other",
    pack:
      feature.pack ||
      state.packs?.find((p) => p.features?.includes(feature.id))?.id ||
      "garage",
  }));
  for (const field of ["recipes", "extraSettings"]) {
    const groups = new Map();
    (state[field] || []).forEach((entry) => {
      if (!groups.has(entry.section)) groups.set(entry.section, []);
      groups.get(entry.section).push(entry);
    });
    groups.forEach((entries, id) =>
      modules.push({
        id,
        entries,
        field,
        name: id
          .replace(/^(Recipe\.|MK\.|Community\.)/, "")
          .replace(/([a-z])([A-Z])/g, "$1 $2"),
        description:
          entries.find((e) => e.key === "Enabled")?.description ||
          "Additional module settings.",
        category: field === "recipes" ? "Driving" : "Gameplay",
        pack:
          field === "recipes"
            ? "recipes"
            : entries[0].pack || state.extraPacks?.[id] || "community",
      }),
    );
  }
  return modules;
}

export function liveEligible(state, key) {
  if (key.endsWith("/Enabled")) return false;
  const id = key.slice(0, key.lastIndexOf("/"));
  const module = modulesFor(state).find((m) => m.id === id);
  return Boolean(
    module &&
      !module.locked &&
      module.available !== false &&
      isEnabled(state, module),
  );
}

export function parameterGroup(module, key) {
  key = key.replace(/^Override/, "");
  if (module.settingGroups?.[key]) return module.settingGroups[key];
  if ((module.settings?.length || 0) < 16) return "";
  if (module.id === "BoostParameters") {
    if (/Pad/.test(key)) return "Boost pads";
    if (/Air|Landing/.test(key)) return "Air and landing";
    if (/Reserves|Triple|Continuous/.test(key)) return "Reserves and speed";
    return "Drift timing and strength";
  }
  if (/InstantBoost/.test(key)) return "Boost delivery";
  if (/Brak|Break|Friction|Drag/.test(key)) return "Braking and friction";
  if (/Steer|Drift|Counter/.test(key)) return "Steering and drifting";
  if (/Jump|Vertical/.test(key)) return "Jump and vertical motion";
  return /Mass/.test(key) ? "Kart body" : "Speed and acceleration";
}
