import { $, el, notice } from "../../shared/dom.js";
import { runAction } from "../../shared/lifecycle.js";
import { store } from "../../shared/store.js";
import { api } from "../../shared/api.js";

export function bindInstallationEvents() {
  $("installation-install").onclick = () => runAction("install");
  $("plugin-update-top").onclick = () => runAction("install");
  $("toolkit-update-top").onclick = async () => {
    const release = store.state?.release || {};
    if (release.canInstall) {
      try {
        const result = await api("update-toolkit", {});
        notice(result.message || "Toolkit update is ready.");
      } catch (error) {
        notice(error.message, true);
      }
    } else {
      try {
        const result = await api("open-release", {});
        notice(result.message);
      } catch (error) {
        notice(error.message, true);
      }
    }
  };
  $("diagnose").onclick = () => runAction("diagnose");
  $("prepare-loader").onclick = () => runAction("install-loader");
  $("open-game-folder").onclick = () => runAction("open-game-folder");
  $("scan-games").onclick = async () => {
    try {
      const result = await api("scan-games", {});
      $("game-candidates").replaceChildren();
      result.games.forEach((path) => {
        const button = el("button", "file-button", path);
        button.onclick = () => {
          $("game-directory").value = path;
        };
        $("game-candidates").append(button);
      });
      if (!result.games.length)
        notice(
          "No Steam installation found. Paste the game folder below.",
          true,
        );
    } catch (error) {
      notice(error.message, true);
    }
  };
  $("select-game").onclick = async () => {
    if (store.settingsDirty || store.sourceDirty) {
      notice("Save your edits before switching game installations.", true);
      return;
    }
    await runAction("select-game", {
      path: $("game-directory").value.trim(),
      discardEdits: true,
    });
  };
  $("launch-game").onclick = () => {
    if (store.settingsDirty || store.sourceDirty) {
      notice("Save your pending edits before launching the game.", true);
      return;
    }
    runAction("launch-game");
  };
  $("browse-game").onclick = () => {
    if (store.settingsDirty || store.sourceDirty) {
      notice("Save your edits before switching game installations.", true);
      return;
    }
    runAction("browse-game");
  };
}
