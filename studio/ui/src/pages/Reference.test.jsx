import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, vi } from "vitest";
import { api } from "../lib/api.js";
import Reference from "./Reference.jsx";

vi.mock("../lib/api.js", () => ({api: vi.fn()}));
vi.mock("../lib/useToolkit.jsx", () => ({
  useToolkit: () => ({notify: vi.fn()}),
}));

const method = {
  id: "12:4",
  name: "FireSliderBoost",
  label: "Fire Slider Boost",
  type: "Ant_BoostManager",
  namespace: "",
  signature: "private void FireSliderBoost(int iBoostNumberIndex);",
  address: "180666590",
  source: null,
  behaviorSlice: {
    behavior: "Ant_BoostManager.FireSliderBoost strength curve slice",
    source: "src/Reconstructed/BoostLogic.cs",
    confidence: "curve only",
  },
};

beforeEach(() => {
  vi.clearAllMocks();
  api.mockImplementation(async (path) => {
    if (path.startsWith("functions?"))
      return {total: 1, indexed: 32350, reconstructed: 34, behaviorSlices: 1, methods: [method]};
    if (path.startsWith("function?"))
      return {...method, status: "Partial C# behavior slice available"};
    if (path.startsWith("pseudocode?"))
      return {
        total: 1,
        indexed: 14243,
        results: [{
          id: "local/ghidra/pseudocode/180666590_BoostInput.c",
          name: "Ant_BoostManager$$FireSliderBoost",
          address: "180666590",
          snippet: "Ghidra pseudocode, not original C#",
          reconstruction: null,
        }],
      };
    if (path.startsWith("pseudocode?id="))
      return {code: "/* native evidence */", truncated: false, reconstruction: null};
    throw new Error("Unexpected API call: " + path);
  });
});

test("API declaration launches native search at its address", async () => {
  render(<Reference onSource={vi.fn()} />);

  fireEvent.change(screen.getByRole("searchbox", {name: "Find a game function"}), {
    target: {value: "FireSliderBoost"},
  });
  await waitFor(() =>
    expect(api.mock.calls.some(([path]) => path.startsWith("functions?q=FireSliderBoost"))).toBe(true),
  );
  fireEvent.click(await screen.findByText("Fire Slider Boost", {exact: true}));
  fireEvent.click(await screen.findByRole("button", {name: /Search native pseudocode at 0x180666590/}));

  await waitFor(() =>
    expect(api.mock.calls.some(([path]) => path.startsWith("pseudocode?q=180666590"))).toBe(true),
  );
  expect(await screen.findByText("Ant_BoostManager$$FireSliderBoost", {exact: true})).toBeTruthy();
});
