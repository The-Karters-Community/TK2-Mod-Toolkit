import { $ } from "./../../shared/dom.js";
import { showWorkshopTab } from "./../workshop/editor.js";
import { store } from "./../../shared/store.js";

export const themeMedia = matchMedia("(prefers-color-scheme: dark)");

export function applyTheme(theme) {
  document.documentElement.dataset.theme = theme;
  $("theme").textContent = theme === "dark" ? "☀ Light mode" : "☾ Dark mode";
  $("theme").setAttribute(
    "aria-label",
    `Switch to ${theme === "dark" ? "light" : "dark"} mode`,
  );
}

export function showView(name) {
  document.querySelectorAll(".view").forEach((view) => {
    view.hidden = view.id !== name;
  });
  document.querySelectorAll(".nav").forEach((button) => {
    button.classList.toggle("active", button.dataset.view === name);
    button.setAttribute(
      "aria-current",
      button.dataset.view === name ? "page" : "false",
    );
  });
  $("breadcrumb").textContent =
    "TOOLKIT / " +
    { mods: "MOD PACKS", workshop: "WORKSHOP", installation: "INSTALLATION" }[
      name
    ];
  history.replaceState(null, "", "#" + name);
  if (name === "workshop") showWorkshopTab(store.workshopTab);
}
