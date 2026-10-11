import {
  acknowledge,
  createDraft,
  displayValue,
  editCount,
  editDraft,
  effectiveState,
  isEnabled,
  liveEligible,
  modulesFor,
  positions,
  snapshot,
} from "./settings.js";

function server() {
  return {
    configHash: "first",
    settings: {
      "OnlineProtection/Enabled": true,
      "GamepadOverlay/Enabled": true,
      "GamepadOverlay/Corner": "TopCenter",
      "GamepadOverlay/PositionX": 50,
      "GamepadOverlay/PositionY": 0,
      "GamepadOverlay/Scale": 1,
    },
    features: [
      {
        id: "OnlineProtection",
        locked: true,
        settings: [],
        category: "Safety",
      },
      { id: "GamepadOverlay", settings: [], category: "Interface" },
    ],
    recipes: [
      { section: "Recipe.Hop", key: "Enabled", value: "true", type: "Boolean" },
    ],
    extraSettings: [],
  };
}

describe("settings draft compatibility", () => {
  test.each(Object.entries(positions))(
    "%s synchronizes both coordinates",
    (name, coordinates) => {
      const draft = editDraft(
        createDraft(server()),
        "values",
        "GamepadOverlay/Corner",
        name,
      );
      const state = effectiveState(draft);
      expect([
        displayValue(state, "GamepadOverlay/PositionX"),
        displayValue(state, "GamepadOverlay/PositionY"),
      ]).toEqual(coordinates);
    },
  );
  test("changing an axis selects Custom and preserves the other preset axis", () => {
    let draft = editDraft(
      createDraft(server()),
      "values",
      "GamepadOverlay/Corner",
      "BottomRight",
    );
    draft = editDraft(draft, "values", "GamepadOverlay/PositionX", 38);
    const state = effectiveState(draft);
    expect(state.settings["GamepadOverlay/Corner"]).toBe("Custom");
    expect(state.settings["GamepadOverlay/PositionY"]).toBe(100);
    expect(state.settings["GamepadOverlay/PositionX"]).toBe(38);
  });
  test("polling does not discard pending edits or overwrite their original base", () => {
    let draft = editDraft(
      createDraft(server()),
      "values",
      "GamepadOverlay/Scale",
      0.6,
    );
    draft = {
      ...draft,
      server: {
        ...server(),
        settings: { ...server().settings, "GamepadOverlay/Scale": 1.2 },
        configHash: "external",
      },
    };
    expect(effectiveState(draft).settings["GamepadOverlay/Scale"]).toBe(0.6);
    expect(snapshot(draft).body.baseValues["GamepadOverlay/Scale"]).toBe(1);
  });
  test("newer edits survive an older save and rebase to its committed value", () => {
    let draft = editDraft(
      createDraft(server()),
      "values",
      "GamepadOverlay/Scale",
      0.6,
    );
    const saved = snapshot(draft);
    draft = editDraft(draft, "values", "GamepadOverlay/Scale", 0.9, true);
    draft = acknowledge(draft, saved, {
      settings: { ...server().settings, "GamepadOverlay/Scale": 0.6 },
      configHash: "saved",
    });
    expect(editCount(draft)).toBe(1);
    expect(effectiveState(draft).settings["GamepadOverlay/Scale"]).toBe(0.9);
    expect(snapshot(draft).body.baseValues["GamepadOverlay/Scale"]).toBe(0.6);
  });
  test("reverting during an in-flight save remains dirty until restored on disk", () => {
    let draft = editDraft(
      createDraft(server()),
      "values",
      "GamepadOverlay/Scale",
      0.6,
    );
    const saved = snapshot(draft);
    draft = editDraft(draft, "values", "GamepadOverlay/Scale", 1, true);
    draft = acknowledge(draft, saved, {
      settings: { ...server().settings, "GamepadOverlay/Scale": 0.6 },
    });
    expect(editCount(draft)).toBe(1);
    expect(snapshot(draft).body.values["GamepadOverlay/Scale"]).toBe(1);
  });
  test("manual revert outside save clears its edit", () => {
    const changed = editDraft(
      createDraft(server()),
      "values",
      "GamepadOverlay/Scale",
      0.6,
    );
    expect(
      editCount(editDraft(changed, "values", "GamepadOverlay/Scale", 1)),
    ).toBe(0);
  });
  test("live preview never includes enable switches, locked modules or disabled modules", () => {
    const state = server();
    expect(liveEligible(state, "GamepadOverlay/Scale")).toBe(true);
    expect(liveEligible(state, "GamepadOverlay/Enabled")).toBe(false);
    expect(liveEligible(state, "OnlineProtection/Option")).toBe(false);
    state.settings["GamepadOverlay/Enabled"] = false;
    expect(liveEligible(state, "GamepadOverlay/Scale")).toBe(false);
    expect(liveEligible(state, "Recipe.Hop/Strength")).toBe(true);
  });
  test("recipes and extra settings use distinct wire fields and matching save bases", () => {
    let draft = editDraft(
      createDraft(server()),
      "recipes",
      "Recipe.Hop/Enabled",
      "false",
    );
    const saved = snapshot(draft);
    expect(saved.body.recipes).toEqual({ "Recipe.Hop/Enabled": "false" });
    expect(saved.body.baseRecipes).toEqual({ "Recipe.Hop/Enabled": "true" });
    expect(
      modulesFor(effectiveState(draft)).filter((module) =>
        isEnabled(effectiveState(draft), module),
      ),
    ).toHaveLength(2);
    draft = acknowledge(draft, saved, {
      recipes: [{ ...server().recipes[0], value: "false" }],
    });
    expect(editCount(draft)).toBe(0);
  });
});
