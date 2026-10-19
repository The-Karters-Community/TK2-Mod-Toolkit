import { fireEvent, render, screen } from "@testing-library/react";
import { vi } from "vitest";
import { Setting } from "./Controls.jsx";

test("keyboard shortcuts are selected instead of typed", () => {
  const change = vi.fn();
  render(
    <Setting
      label="Rewind key"
      kind="text"
      value="Backspace"
      choices={["None", "Backspace", "Space", "F5"]}
      onChange={change}
    />,
  );
  const menu = screen.getByRole("combobox", { name: "Rewind key" });
  fireEvent.change(menu, { target: { value: "Space" } });
  expect(change).toHaveBeenCalledWith("Space");
  expect(screen.queryByRole("textbox")).toBeNull();
});

test("controller menu names physical buttons and the disabled option", () => {
  const change = vi.fn();
  render(
    <Setting
      label="Controller button"
      kind="text"
      value=""
      choices={["", "Gamepad: A / Cross", "Gamepad: LB / L1"]}
      onChange={change}
    />,
  );
  expect(screen.getByRole("option", { name: "None (disabled)" })).toBeTruthy();
  fireEvent.change(
    screen.getByRole("combobox", { name: "Controller button" }),
    { target: { value: "Gamepad: LB / L1" } },
  );
  expect(change).toHaveBeenCalledWith("Gamepad: LB / L1");
});

test("suggested numeric ranges do not clamp experimental values", () => {
  const change = vi.fn();
  render(
    <Setting
      label="Lateral velocity"
      kind="float"
      value={1000}
      low={0}
      high={null}
      description="Suggested 12-30; no upper limit."
      onChange={change}
    />,
  );
  const field = screen.getByRole("spinbutton", { name: "Lateral velocity" });
  expect(field.hasAttribute("max")).toBe(false);
  fireEvent.change(field, { target: { value: "10000" } });
  expect(change).toHaveBeenCalledWith(10000);
  expect(screen.queryByRole("slider")).toBeNull();
});
