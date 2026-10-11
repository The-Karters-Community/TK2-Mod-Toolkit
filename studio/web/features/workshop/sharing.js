import { $, el, notice } from "./../../shared/dom.js";
import { store } from "./../../shared/store.js";
import { packsForState } from "./../mods/catalog.js";
import { refresh, setBusy } from "./../../shared/lifecycle.js";
import { api } from "./../../shared/api.js";
import { changeEntry, changeSetting } from "./../mods/settings-state.js";
import { syncControls } from "./../mods/controls.js";

export function updateSharingButtons() {
  $("export-package").disabled = store.busy || !store.exportSelection.size;
  $("import-package").disabled = store.busy || !store.previewPackage;
  $("export-count").textContent = `${store.exportSelection.size} selected`;
}

export function renderSharing() {
  const list = $("export-modules");
  list.replaceChildren();
  packsForState().forEach((pack) => {
    list.append(el("h4", "export-pack", pack.name));
    pack.features.forEach((feature) => {
      const row = el("label", "export-module"),
        input = el("input");
      input.type = "checkbox";
      input.checked = store.exportSelection.has(feature.id);
      input.dataset.exportModule = feature.id;
      input.onchange = () => {
        if (input.checked) store.exportSelection.add(feature.id);
        else store.exportSelection.delete(feature.id);
        updateSharingButtons();
      };
      row.append(input, el("span", "", feature.name));
      list.append(row);
    });
  });
  updateSharingButtons();
}

export async function authoringAction(action, body, callback) {
  if (store.busy) return;
  store.actionInFlight = action;
  setBusy(true);
  notice("Working…");
  try {
    const result = await api(action, body);
    await refresh(true);
    if (callback) await callback(result);
    notice(result.message || "Done");
  } catch (error) {
    notice(error.message, true);
  } finally {
    store.actionInFlight = undefined;
    setBusy(false);
  }
}

export function stageImportedSettings(values) {
  Object.entries(values || {}).forEach(([key, value]) => {
    if (Object.hasOwn(store.state.settings, key)) changeSetting(key, value);
    else {
      const entry = [
        ...(store.state.recipes || []),
        ...(store.state.extraSettings || []),
      ].find((e) => e.section + "/" + e.key === key);
      if (entry)
        changeEntry(
          entry,
          String(value),
          (store.state.extraSettings || []).includes(entry),
        );
    }
  });
  syncControls();
}

export function bindSharingEvents() {
  $("browse-package").onclick = () =>
    authoringAction("browse-file", { kind: "package" }, (result) => {
      if (result.path) {
        $("package-file").value = result.path;
        store.previewPackage = undefined;
        updateSharingButtons();
      }
    });
  $("export-select-all").onclick = () => {
    store.state.features.forEach((f) => store.exportSelection.add(f.id));
    renderSharing();
  };
  $("export-select-none").onclick = () => {
    store.exportSelection.clear();
    renderSharing();
  };
  $("export-package").onclick = () => {
    if (store.sourceDirty) {
      notice("Save your C# before exporting its source.", true);
      return;
    }
    authoringAction(
      "export-package",
      {
        ids: [...store.exportSelection],
        name: $("package-name").value.trim(),
        values: store.state.settings,
      },
      async (result) => {
        const response = await fetch(
          "/api/download?id=" + encodeURIComponent(result.id),
          { headers: { "X-TK2-Token": token } },
        );
        if (!response.ok) throw new Error("Could not download the package.");
        const url = URL.createObjectURL(await response.blob()),
          link = el("a");
        link.href = url;
        link.download = result.filename;
        document.body.append(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 10000);
      },
    );
  };
  $("package-file").oninput = () => {
    store.previewPackage = undefined;
    updateSharingButtons();
  };
  $("preview-package").onclick = () =>
    authoringAction(
      "preview-package",
      { path: $("package-file").value.trim() },
      (result) => {
        store.previewPackage = result;
        const panel = $("package-preview");
        panel.replaceChildren();
        panel.append(el("h4", "", `${result.modules.length} modules`));
        result.modules.forEach((module) =>
          panel.append(
            el(
              "p",
              "",
              typeof module === "string" ? module : module.name || module.id,
            ),
          ),
        );
        const files = el("ul");
        (result.files || []).forEach((file) =>
          files.append(
            el(
              "li",
              "",
              typeof file === "string" ? file : file.path || file.name,
            ),
          ),
        );
        panel.append(files);
        if (result.conflicts?.length)
          panel.append(
            el(
              "p",
              "",
              `${result.conflicts.length} existing files differ. Replacing shared source can affect other modules.`,
            ),
          );
        else
          panel.append(
            el(
              "p",
              "muted",
              "No source conflicts. Imported modules start off.",
            ),
          );
        $("replace-package-files").checked = false;
        updateSharingButtons();
      },
    );
  $("import-package").onclick = () => {
    if (store.sourceDirty) {
      notice("Save your C# before importing source files.", true);
      return;
    }
    if (!store.previewPackage) return;
    authoringAction(
      "import-package",
      {
        path: store.previewPackage.path,
        hash: store.previewPackage.hash,
        replace: $("replace-package-files").checked,
      },
      (result) => {
        stageImportedSettings(result.settings);
        store.previewPackage = undefined;
        updateSharingButtons();
      },
    );
  };
}
