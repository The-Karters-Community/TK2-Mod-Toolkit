import { startWindowPresence } from "./shared/api.js";
import { bindModEvents } from "./features/mods/events.js";
import { bindWorkshopEvents } from "./features/workshop/events.js";
import { bindInstallationEvents } from "./features/installation/events.js";
import { bindSharingEvents } from "./features/workshop/sharing.js";
import { bindShellEvents } from "./features/shell/events.js";
import { store } from "./shared/store.js";
import { refresh } from "./shared/lifecycle.js";
import { showView } from "./features/shell/navigation.js";
import { notice } from "./shared/dom.js";

startWindowPresence();
bindModEvents();
bindWorkshopEvents();
bindInstallationEvents();
bindSharingEvents();
bindShellEvents();

showView(
  ["mods", "workshop", "installation"].includes(location.hash.slice(1))
    ? location.hash.slice(1)
    : "mods",
);
refresh()
  .then(() => {
    if (!store.state.setup?.ready && !store.state.game)
      showView("installation");
  })
  .catch((error) => {
    notice("Toolkit could not load: " + error.message, true);
  });
setInterval(() => {
  if (store.state && !store.busy && !document.hidden)
    refresh(true, true).catch(() => {});
}, 5000);
