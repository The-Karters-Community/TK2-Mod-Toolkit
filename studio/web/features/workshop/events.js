import { $, el, notice } from "./../../shared/dom.js";
import { runAction } from "./../../shared/lifecycle.js";
import { store } from "./../../shared/store.js";
import {
  editModule,
  loadSource,
  renderFiles,
  saveSource,
  showWorkshopTab,
  updateEditorState,
} from "./editor.js";
import { createGameApiBrowser } from "./game-api-browser.js";
import { api } from "./../../shared/api.js";

export function bindWorkshopEvents() {
  $("workshop-install").onclick = () => runAction("build-install");
  $("build").onclick = () => runAction("build");
  $("create-recipe").onclick = () => {
    if (store.sourceDirty) {
      notice("Save the current C# before creating another recipe.", true);
      return;
    }
    runAction("create-recipe", { name: $("recipe-name").value.trim() });
  };
  $("code").oninput = () => {
    store.sourceDirty =
      Boolean(store.source) && $("code").value !== store.source.content;
    $("save-source").disabled = store.busy || !store.sourceDirty;
    updateEditorState();
  };
  $("code").onscroll = () => {
    $("line-numbers").scrollTop = $("code").scrollTop;
  };
  $("file-query").oninput = renderFiles;
  $("module-focus").onchange = () => {
    if (store.sourceDirty) {
      $("module-focus").value = store.selectedModule;
      notice("Save your C# before switching modules.", true);
      return;
    }
    store.selectedModule = $("module-focus").value;
    renderFiles();
    const files = store.state.moduleSources?.[store.selectedModule];
    if (files?.length) loadSource(files[0]);
  };
  $("open-source-folder").onclick = () => {
    if (store.source)
      runAction("open-source-folder", { file: store.source.file });
  };
  $("find-next").onclick = () => {
    const text = $("code").value,
      query = $("code-find").value;
    if (!query) return;
    const start = $("code").selectionEnd || 0;
    let index = text.toLowerCase().indexOf(query.toLowerCase(), start);
    if (index < 0) index = text.toLowerCase().indexOf(query.toLowerCase());
    if (index < 0) {
      notice("No matching text in this file.");
      return;
    }
    $("code").focus();
    $("code").setSelectionRange(index, index + query.length);
    $("code").scrollTop = Math.max(
      0,
      (text.slice(0, index).split("\n").length - 4) * 22.1,
    );
    $("code").onscroll();
  };
  $("code-find").onkeydown = (event) => {
    if (event.key === "Enter") $("find-next").click();
  };
  store.workshopTabs.forEach((name, index, names) => {
    const button = $("tab-" + name);
    button.onclick = () => showWorkshopTab(name);
    button.onkeydown = (event) => {
      if (["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) {
        event.preventDefault();
        const next =
          event.key === "Home"
            ? 0
            : event.key === "End"
              ? names.length - 1
              : (index + (event.key === "ArrowRight" ? 1 : names.length - 1)) %
                names.length;
        showWorkshopTab(names[next]);
        $("tab-" + names[next]).focus();
      }
    };
  });
  $("tutorial-create").onclick = () => {
    if (store.sourceDirty) {
      notice("Save your current C# first.", true);
      return;
    }
    $("recipe-name").value = "MyKartHop";
    return runAction("create-recipe", { name: "MyKartHop" });
  };
  $("tutorial-frame").onclick = () => editModule("Recipe.FrameLimiter");
  $("save-source").onclick = async () => {
    try {
      await saveSource();
      notice("C# saved. Build the plugin to apply your code.");
    } catch (error) {
      notice(error.message, true);
    }
  };
  $("reload-source").onclick = () => {
    if (
      store.sourceDirty &&
      !confirm("Discard unsaved editor changes and reload this file?")
    )
      return;
    loadSource(store.source.file, true);
  };
  $("code").onkeydown = (event) => {
    if (event.key === "Tab") {
      event.preventDefault();
      const node = event.target;
      node.setRangeText("    ", node.selectionStart, node.selectionEnd, "end");
      node.dispatchEvent(new Event("input"));
    }
  };
  store.gameApiBrowser = createGameApiBrowser({
    $,
    api,
    el,
    notice,
    openFunctionSource: (file) => {
      if (store.sourceDirty) {
        notice("Save your current C# first.", true);
        return;
      }
      store.selectedModule = "reconstructed";
      showWorkshopTab("editor");
      loadSource(file);
    },
  });
}
