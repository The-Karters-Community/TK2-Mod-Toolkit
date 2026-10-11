import { $, el, notice } from "./../../shared/dom.js";
import { store } from "./../../shared/store.js";
import { api } from "./../../shared/api.js";
import { showView } from "./../shell/navigation.js";

export function renderFiles() {
  $("reconstructed-files").replaceChildren();
  $("pack-files").replaceChildren();
  const select = $("module-focus");
  select.replaceChildren();
  [
    ["all", "All project files"],
    ["reconstructed", "Reconstructed game logic"],
    ...store.state.features.map((f) => [f.id, f.name]),
    ...Object.keys(store.state.moduleSources || {})
      .filter(
        (id) =>
          id.startsWith("Recipe.") &&
          !store.state.features.some((f) => f.id === id),
      )
      .map((id) => [
        id,
        id.slice(7).replace(/([a-z])([A-Z])/g, "$1 $2") + " (recipe)",
      ]),
  ].forEach(([value, label]) => {
    const option = el("option", "", label);
    option.value = value;
    select.append(option);
  });
  select.value = store.selectedModule;
  const allowed = store.state.moduleSources?.[store.selectedModule],
    query = $("file-query").value.toLowerCase().trim();
  $("module-source-note").textContent =
    store.selectedModule === "reconstructed"
      ? "Reviewed translations of selected native methods. Evidence and limits are in their comments."
      : store.selectedModule === "all"
        ? "Select a module to show its behavior and settings files."
        : "These files implement this module. Shared files can affect other modules in the same pack.";
  store.state.files.forEach((file) => {
    if (
      (allowed && !allowed.includes(file)) ||
      (store.selectedModule === "reconstructed" &&
        !file.startsWith("src/Reconstructed/")) ||
      !file.toLowerCase().includes(query)
    )
      return;
    const button = el("button", "file-button", file.split("/").at(-1));
    button.dataset.file = file;
    button.title = file;
    button.disabled = store.busy;
    const role = file.includes("/Recipes/")
      ? "Your recipe"
      : /Settings|Plugin.cs|PackModules.cs/.test(file)
        ? "Behavior & settings"
        : /Panel/.test(file)
          ? "In-game panel"
          : file.startsWith("src/Reconstructed/")
            ? "Reconstructed behavior"
            : "C# implementation";
    button.append(el("small", "file-role", role));
    button.classList.toggle("selected", store.source?.file === file);
    button.onclick = () => loadSource(file);
    $(
      file.startsWith("src/Reconstructed/")
        ? "reconstructed-files"
        : "pack-files",
    ).append(button);
  });
}

export async function saveSource() {
  if (!store.source || !store.sourceDirty) return;
  const content = $("code").value,
    result = await api("save-source", {
      file: store.source.file,
      hash: store.source.hash,
      content,
    });
  store.source.hash = result.hash;
  store.source.content = content;
  store.sourceDirty = $("code").value !== content;
  $("save-source").disabled = !store.sourceDirty;
  updateEditorState();
}

export async function loadSource(file, discard = false) {
  if (store.sourceDirty && !discard) {
    notice(
      "Save the current C# before switching files, or use Reload file to discard your edits.",
      true,
    );
    return false;
  }
  const sequence = ++store.sourceSequence;
  $("code").readOnly = true;
  $("editor-state").textContent = "Loading file…";
  try {
    const loaded = await api("source?file=" + encodeURIComponent(file));
    if (sequence !== store.sourceSequence) return false;
    store.source = loaded;
    $("code").value = store.source.content;
    $("source-name").textContent = file;
    store.sourceDirty = false;
    $("code").readOnly = store.busy;
    $("save-source").disabled = true;
    $("reload-source").disabled = store.busy;
    $("open-source-folder").disabled = store.busy;
    $("code").scrollTop = 0;
    updateEditorState();
    renderFiles();
    return true;
  } catch (error) {
    if (sequence === store.sourceSequence) {
      $("code").readOnly = store.busy || !store.source;
      updateEditorState();
      notice(error.message, true);
    }
    return false;
  }
}

export function updateEditorState() {
  const lines = $("code").value.split("\n").length;
  $("line-numbers").textContent = Array.from(
    { length: lines },
    (_, i) => i + 1,
  ).join("\n");
  $("editor-state").textContent = !store.source
    ? "Choose a file"
    : `${store.sourceDirty ? "Unsaved edits" : "File saved"} · ${lines} lines`;
  $("line-numbers").scrollTop = $("code").scrollTop;
}

export function editModule(id) {
  if (store.sourceDirty) {
    notice("Save your current C# before opening another module.", true);
    return;
  }
  const files = store.state.moduleSources?.[id];
  if (!files?.length) {
    notice("Editable source is not included for this module.", true);
    return;
  }
  store.selectedModule = id;
  showView("workshop");
  showWorkshopTab("editor");
  renderFiles();
  loadSource(files[0]);
}

export function showWorkshopTab(name) {
  store.workshopTab = name;
  store.workshopTabs.forEach((tab) => {
    const active = tab === name;
    $("panel-" + tab).hidden = !active;
    const button = $("tab-" + tab);
    button.classList.toggle("selected", active);
    button.setAttribute("aria-selected", String(active));
    button.tabIndex = active ? 0 : -1;
  });
  if (name === "api" && store.gameApiBrowser)
    store.gameApiBrowser
      .searchFunctions()
      .catch((error) => notice(error.message, true));
}
