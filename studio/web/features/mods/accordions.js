import { store } from "../../shared/store.js";
import { $ } from "../../shared/dom.js";

export function bindAccordion(trigger, panel, key, expanded, onOpen) {
  panel.hidden = !expanded;
  trigger.dataset.accordionKey = key;
  trigger.setAttribute("aria-expanded", String(expanded));
  trigger.setAttribute("aria-controls", panel.id);
  trigger.onAccordionOpen = onOpen;
  if (expanded) onOpen?.();
  trigger.onclick = () => {
    const open = panel.hidden;
    panel.hidden = !open;
    trigger.setAttribute("aria-expanded", String(open));
    if (open) {
      store.expandedModules.add(key);
      trigger.onAccordionOpen?.();
    } else store.expandedModules.delete(key);
  };
}

export function setAllAccordions(expanded) {
  let previousCount;
  do {
    const accordions = document.querySelectorAll("[data-accordion-key]");
    previousCount = accordions.length;
    accordions.forEach((accordion) => {
      const key = accordion.dataset.accordionKey;
      if (expanded) store.expandedModules.add(key);
      else store.expandedModules.delete(key);
      if (accordion.tagName === "DETAILS") {
        accordion.open = expanded;
        return;
      }
      const panel = $(accordion.getAttribute("aria-controls"));
      if (!panel) return;
      panel.hidden = !expanded;
      accordion.setAttribute("aria-expanded", String(expanded));
      if (expanded) accordion.onAccordionOpen?.();
    });
  } while (
    expanded &&
    document.querySelectorAll("[data-accordion-key]").length > previousCount
  );
}

export function chevronIcon() {
  const namespace = "http://www.w3.org/2000/svg";
  const icon = document.createElementNS(namespace, "svg");
  icon.setAttribute("class", "expand-arrow");
  icon.setAttribute("viewBox", "0 0 24 24");
  icon.setAttribute("aria-hidden", "true");
  const path = document.createElementNS(namespace, "path");
  path.setAttribute("d", "M16.59 8.59 12 13.17 7.41 8.59 6 10l6 6 6-6z");
  icon.append(path);
  return icon;
}
