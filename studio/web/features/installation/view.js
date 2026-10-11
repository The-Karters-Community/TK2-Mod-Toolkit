import { store } from "../../shared/store.js";
import { $, el } from "../../shared/dom.js";
import { runAction } from "../../shared/lifecycle.js";

export function renderInstallation() {
  const ready = store.state.setup || {};
  $("game-path").textContent = store.state.game || "No game selected";
  if (document.activeElement !== $("game-directory"))
    $("game-directory").value = store.state.game || "";
  const compatibility = store.state.prebuiltCompatibility;
  $("plugin-status").textContent =
    `${store.state.installed ? "Plugin installed" : "Plugin not installed"} · ${store.state.pluginCount} active plugin DLL${store.state.pluginCount === 1 ? "" : "s"} · ${compatibility?.compatible ? "prebuilt matches this game and loader" : compatibility?.reason || "prebuilt compatibility has not been checked"}`;
  $("setup-message").textContent = ready.message || "Check the installation.";
  $("setup-checks").replaceChildren();
  (ready.checks || []).forEach((check) => {
    const row = el(
      "li",
      check.ok ? "check-ok" : "check-pending",
      `${check.ok ? "✓" : "○"} ${check.name}`,
    );
    $("setup-checks").append(row);
  });
  $("prepare-loader").disabled =
    store.busy || !store.state.game || !ready.loaderSource;
  $("prepare-loader").textContent =
    ready.stage === "install-loader"
      ? "Prepare BepInEx"
      : "Repair BepInEx files";
  $("loader-help").textContent = ready.loaderSource
    ? "BepInEx distribution included. Existing plugins and configs are preserved."
    : `This portable package does not include BepInEx. Install the tested ${ready.loaderVersion || "6.0.0-be.788"} x64 loader first; the toolkit will verify it before installing.`;
  $("installation-install").disabled =
    store.busy ||
    !ready.ready ||
    store.state.packCurrent ||
    compatibility?.compatible === false;
  $("installation-install").textContent = store.state.packCurrent
    ? "Plugin up to date"
    : compatibility?.compatible === false
      ? "Build for this installation first"
      : store.state.installed
        ? "Update plugin (restart required)"
        : "Install plugin";
  const updateAvailable = Boolean(
    ready.ready &&
      store.state.installed &&
      !store.state.packCurrent &&
      compatibility?.compatible !== false,
  );
  $("plugin-update-top").hidden = !updateAvailable;
  $("plugin-update-top").disabled = store.busy || !ready.ready;
  const release = store.state.release || {},
    toolkitUpdate = $("toolkit-update-top");
  toolkitUpdate.hidden = release.state !== "available";
  toolkitUpdate.disabled = store.busy;
  toolkitUpdate.textContent = release.canInstall
    ? `Update toolkit · ${release.version}`
    : `Toolkit ${release.version} available`;
  const plugins = store.state.plugins || [],
    others = plugins.filter((plugin) => !plugin.toolkit);
  $("compatibility-summary").textContent =
    `Loader: ${ready.loaderVersion || "not verified"} · ${ready.loaderCompatible ? "supported build" : "unsupported or not yet verified"}. ${others.length ? `${others.length} other plugin file${others.length === 1 ? "" : "s"} found; review them if the toolkit misbehaves.` : "No other BepInEx plugins detected."} Config files are never removed by this audit.`;
  const pluginList = $("plugin-audit-list");
  pluginList.replaceChildren();
  plugins.forEach((plugin) => {
    const row = el("li", "compat-audit-item"),
      copy = el("span", "compat-audit-copy");
    copy.append(
      el("span", "", plugin.relative),
      el(
        "small",
        "",
        plugin.toolkit
          ? "Toolkit plugin · protected"
          : plugin.enabled
            ? "Other plugin · possible conflict, not confirmed"
            : "Disabled plugin · available to restore",
      ),
    );
    row.append(copy);
    if (!plugin.toolkit) {
      const button = el(
        "button",
        "secondary",
        plugin.enabled ? "Disable" : "Enable",
      );
      button.disabled = store.busy;
      button.onclick = () => {
        if (
          !confirm(
            `${plugin.enabled ? "Disable" : "Enable"} ${plugin.relative}? The file is only renamed; nothing is deleted. Close the game first.`,
          )
        )
          return;
        runAction("toggle-plugin", { path: plugin.relative });
      };
      row.append(button);
    }
    pluginList.append(row);
  });
  if (!plugins.length)
    pluginList.append(el("li", "empty-state", "No plugin DLLs found."));
  const configList = $("config-audit-list");
  configList.replaceChildren();
  (store.state.configs || []).forEach((config) => {
    const row = el("li", "compat-audit-item"),
      copy = el("span", "compat-audit-copy");
    copy.append(
      el("span", "", config.relative),
      el(
        "small",
        "",
        config.enabled
          ? "Config file · can be disabled and restored by renaming"
          : "Disabled config · available to restore",
      ),
    );
    const button = el(
      "button",
      "secondary",
      config.enabled ? "Disable config" : "Restore config",
    );
    button.disabled = store.busy;
    button.onclick = () => {
      if (
        !confirm(
          `${config.enabled ? "Disable" : "Restore"} ${config.relative}? This only renames the file. Keep the associated plugin disabled if you do not want it to recreate a default config. Close the game first.`,
        )
      )
        return;
      runAction("toggle-config", { path: config.relative });
    };
    row.append(copy, button);
    configList.append(row);
  });
  if (!(store.state.configs || []).length)
    configList.append(
      el("li", "empty-state", "No other plugin configs found."),
    );
  $("open-game-folder").disabled = store.busy || !store.state.game;
  $("runtime-errors").textContent =
    [...(ready.runtimeErrors || []), ...(ready.runtimeWarnings || [])].join(
      "\n",
    ) || "No errors or warnings found in the available BepInEx log.";
  $("install-state").textContent = !ready.ready
    ? "Setup needed"
    : store.state.installed
      ? store.state.packCurrent
        ? "Plugin up to date"
        : "Plugin update available"
      : "Ready to install";
  const list = $("backups");
  list.replaceChildren();
  store.state.backups.forEach((backup) => {
    const row = el("div", "backup-row"),
      info = el("div");
    info.append(
      el("p", "", backup.file),
      el(
        "small",
        "",
        `${backup.id} · ${backup.existed ? "restore previous contents" : "remove newly installed file"}`,
      ),
    );
    const button = el("button", "secondary", "Restore");
    button.disabled = store.busy;
    button.onclick = () => runAction("restore", { id: backup.id });
    row.append(info, button);
    list.append(row);
  });
  if (!store.state.backups.length)
    list.append(el("p", "empty-state", "Changes create backups here."));
  $("logs").textContent =
    store.state.buildLog ||
    store.state.logs.join("\n") ||
    "No build run in this session. Player installation uses the bundled plugin.";
  $("workshop-build-log").textContent =
    store.state.buildLog ||
    "No compiler run yet. Build pack validates your C#; Build & install also replaces the plugin with the game closed.";
}
