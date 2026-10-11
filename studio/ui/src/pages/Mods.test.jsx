import { fireEvent, render, screen } from "@testing-library/react";
import { vi } from "vitest";
import Mods from "./Mods.jsx";
import { createDraft, editDraft, effectiveState } from "../lib/settings.js";

let toolkit;
vi.mock("../lib/useToolkit.jsx", () => ({ useToolkit: () => toolkit }));
function setup() {
  const server = {
    game: "fixture",
    settings: {
      "OnlineProtection/Enabled": true,
      "GamepadOverlay/Enabled": true,
      "GamepadOverlay/Corner": "TopRight",
      "GamepadOverlay/PositionX": 50,
      "GamepadOverlay/PositionY": 0,
    },
    features: [
      {
        id: "OnlineProtection",
        name: "Online protection",
        locked: true,
        category: "Safety",
        settings: [],
      },
      {
        id: "GamepadOverlay",
        name: "Gamepad viewer",
        category: "Interface",
        settings: [
          [
            "Corner",
            "Screen position",
            "text",
            "TopCenter",
            null,
            null,
            "",
            ["TopCenter", "TopRight", "Custom"],
          ],
          ["PositionX", "Custom horizontal position", "float", 50, 0, 100],
          ["PositionY", "Custom vertical position", "float", 0, 0, 100],
        ],
      },
    ],
    recipes: [],
    extraSettings: [],
    packs: [],
    moduleSources: {},
  };
  let draft = createDraft(server),
    view;
  toolkit = {
    state: effectiveState(draft),
    busy: "",
    notify: vi.fn(),
    live: false,
    setLive: vi.fn(),
    editModule: vi.fn(),
    change: (field, key, value) => {
      draft = editDraft(draft, field, key, value);
      toolkit.state = effectiveState(draft);
      view.rerender(<Mods />);
    },
  };
  view = render(<Mods />);
}
test("enabled count updates immediately and disabled modules leave the filtered view", () => {
  setup();
  const enabled = screen.getByRole("button", { name: /Enabled 2/ });
  fireEvent.click(enabled);
  fireEvent.click(
    screen.getByRole("switch", { name: "Enable Gamepad viewer" }),
  );
  expect(screen.getByRole("button", { name: /Enabled 1/ })).toBeTruthy();
  expect(
    screen.queryByRole("switch", { name: "Enable Gamepad viewer" }),
  ).toBeNull();
  expect(
    screen.queryByRole("switch", { name: "Enable Online protection" }),
  ).toBeNull();
});
test("bulk disable preserves locked online protection", () => {
  setup();
  fireEvent.click(screen.getByRole("button", { name: /Enabled 2/ }));
  fireEvent.click(screen.getByRole("button", { name: "Disable enabled" }));
  expect(toolkit.state.settings["OnlineProtection/Enabled"]).toBe(true);
  expect(toolkit.state.settings["GamepadOverlay/Enabled"]).toBe(false);
});
test("position controls show preset coordinates and editing selects Custom", () => {
  setup();
  fireEvent.click(screen.getByRole("button", { name: "Expand all" }));
  expect(
    screen.getByRole("spinbutton", { name: "Custom horizontal position" })
      .value,
  ).toBe("100");
  expect(
    screen.getByRole("spinbutton", { name: "Custom vertical position" }).value,
  ).toBe("0");
  fireEvent.change(
    screen.getByRole("spinbutton", { name: "Custom horizontal position" }),
    { target: { value: "28" } },
  );
  expect(screen.getByRole("combobox", { name: "Screen position" }).value).toBe(
    "Custom",
  );
  expect(
    screen.getByRole("spinbutton", { name: "Custom vertical position" }).value,
  ).toBe("0");
  fireEvent.click(screen.getByRole("button", { name: "Collapse all" }));
  expect(
    screen.queryByRole("spinbutton", { name: "Custom horizontal position" }),
  ).toBeNull();
});
