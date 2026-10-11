import { applyTheme, showView, themeMedia } from "./navigation.js";
import { $ } from "./../../shared/dom.js";
import { store } from "./../../shared/store.js";

export function bindShellEvents() {
  applyTheme(
    localStorage.getItem("tk2-theme") ||
      (themeMedia.matches ? "dark" : "light"),
  );
  $("theme").onclick = () => {
    const theme =
      document.documentElement.dataset.theme === "dark" ? "light" : "dark";
    localStorage.setItem("tk2-theme", theme);
    applyTheme(theme);
  };
  themeMedia.addEventListener("change", (event) => {
    if (!localStorage.getItem("tk2-theme"))
      applyTheme(event.matches ? "dark" : "light");
  });
  document
    .querySelectorAll(".nav")
    .forEach(
      (button) => (button.onclick = () => showView(button.dataset.view)),
    );
  document.querySelector(".brand").onclick = (event) => {
    event.preventDefault();
    showView("mods");
  };
  document.addEventListener("keydown", (event) => {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
      event.preventDefault();
      if (location.hash === "#workshop" && store.sourceDirty && !store.busy)
        $("save-source").click();
      else if (store.settingsDirty && !store.busy) $("save-settings").click();
    }
  });
  window.addEventListener("beforeunload", (event) => {
    if (store.sourceDirty || store.settingsDirty || store.busy) {
      event.preventDefault();
      event.returnValue = "";
    }
  });
}
