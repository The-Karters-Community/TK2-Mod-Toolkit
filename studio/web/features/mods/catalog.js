import { store } from "./../../shared/store.js";
import { $, el } from "./../../shared/dom.js";
import { renderFeatures } from "./library.js";
import { renderRecipes } from "./recipes.js";

export function featureGroup(feature) {
  return feature.gameplay ? "Gameplay" : feature.category || "Other";
}

export function renderCategoryFilters() {
  if (!store.state) return;
  const filters = $("filters");
  filters.replaceChildren();
  const counts = moduleCounts();
  const categories = [...new Set(store.state.features.map(featureGroup))].sort(
    (a, b) => a.localeCompare(b),
  );
  const options = [["All", "All"], ...categories.map((name) => [name, name])];
  options.forEach(([id, label]) => {
    const button = el("button", "chip", label);
    button.dataset.category = id;
    button.classList.toggle("selected", store.category === id);
    button.setAttribute("aria-pressed", String(store.category === id));
    button.onclick = () => {
      store.category = id;
      renderCategoryFilters();
      renderFeatures();
      renderRecipes();
    };
    filters.append(button);
  });
  const enabled = $("show-enabled");
  enabled.textContent = `Enabled modules · ${counts.enabled}`;
  enabled.setAttribute("aria-pressed", String(store.category === "Enabled"));
  enabled.classList.toggle("selected", store.category === "Enabled");
  $("disable-all-enabled").hidden = store.category !== "Enabled";
  $("disable-all-enabled").disabled = store.busy || counts.unlocked === 0;
  const preview = $("apply-adjustments");
  preview.textContent = `Apply as you adjust · ${store.livePreviewEnabled ? "On" : "Off"}`;
  preview.setAttribute("aria-pressed", String(store.livePreviewEnabled));
}

export function countEnabledSections(entries, onlyEnabled = false) {
  const sections = new Map();
  (entries || []).forEach((entry) => {
    if (!sections.has(entry.section)) sections.set(entry.section, []);
    sections.get(entry.section).push(entry);
  });
  let count = 0;
  sections.forEach((settings) => {
    const toggle = settings.find(
      (entry) => entry.key === "Enabled" && entry.type === "Boolean",
    );
    if (!onlyEnabled || toggle?.value.toLowerCase() === "true") count++;
  });
  return count;
}

export function moduleCounts() {
  const featureEnabled = store.state.features.filter(
    (feature) =>
      feature.locked || store.state.settings[feature.id + "/Enabled"],
  ).length;
  const featureUnlocked = store.state.features.filter(
    (feature) =>
      !feature.locked && store.state.settings[feature.id + "/Enabled"],
  ).length;
  const extraCount = countEnabledSections(store.state.extraSettings || []);
  const recipeCount = countEnabledSections(store.state.recipes || []);
  return {
    all: store.state.features.length + extraCount + recipeCount,
    enabled:
      featureEnabled +
      countEnabledSections(store.state.extraSettings || [], true) +
      countEnabledSections(store.state.recipes || [], true),
    unlocked:
      featureUnlocked +
      countEnabledSections(store.state.extraSettings || [], true) +
      countEnabledSections(store.state.recipes || [], true),
  };
}

export function updateModuleCount() {
  const total = store.visibleModules + store.visibleRecipeModules,
    search = $("mod-search").value.trim();
  $("module-count").textContent =
    `${total} module${total === 1 ? "" : "s"} shown${search ? " for “" + search + "”" : ""}.`;
}

export function packsForState() {
  const packs = (store.state.packs || []).map((pack) => ({
    ...pack,
    features: (pack.features || [])
      .map((id) =>
        typeof id === "object"
          ? id
          : store.state.features.find((feature) => feature.id === id),
      )
      .filter(Boolean),
  }));
  const included = new Set(
    packs.flatMap((pack) => pack.features.map((feature) => feature.id)),
  );
  store.state.features
    .filter((feature) => !included.has(feature.id))
    .forEach((feature) => {
      const id =
        feature.pack ||
        (feature.origin?.toLowerCase().includes("mk") ? "mks" : "garage");
      let pack = packs.find((item) => item.id === id);
      if (!pack) {
        pack = {
          id,
          name: id === "mks" ? "Community Mods" : "Toolkit Essentials",
          description: "",
          features: [],
        };
        packs.push(pack);
      }
      pack.features.push(feature);
    });
  return packs;
}

export function extraPackId(entry) {
  const id =
    entry.pack ||
    store.state.extraPacks?.[entry.section] ||
    (/^(MK\.|Alternate|AutoBoost|BoostTrainer|Bobby|Boring|CNK|CustomPhysics|Dash|FastRespawn|Mirror|Reverse|Proximity|SaveState|Supra|Teleport)/.test(
      entry.section,
    )
      ? "mks"
      : "community");
  return id === "mks" && !store.state.packs?.some((pack) => pack.id === "mks")
    ? "community"
    : id;
}
