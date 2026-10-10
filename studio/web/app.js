'use strict';
let nextFunctions = null, functionSequence = 0, functionLoading = false, detailSequence = 0, sourceSequence = 0;
let nextPseudocode = null, pseudocodeSequence = 0, pseudocodeLoading = false, pseudocodeDetailSequence = 0;
let workshopTab = 'editor', selectedModule = 'all', functionQueryTimer;
const $ = id => document.getElementById(id);
const token = document.querySelector('meta[name="tk2-session"]').content;
let state, category = 'All', source, sourceDirty = false, settingsDirty = false, busy = false, actionInFlight;
let noticeTimer, revision = 0, refreshSequence = 0, conflictKeys = [];
let livePreviewEnabled = localStorage.getItem('tk2-live-preview') === 'true', livePreviewTimer;
const settingEdits = new Map(), recipeEdits = new Map(), extraEdits = new Map(), expandedModules = new Set();
let visibleModules = 0, visibleRecipeModules = 0;
const workshopTabs = ['editor', 'api', 'sharing', 'tutorial'];
const exportSelection = new Set();
let previewPackage;
const themeMedia = matchMedia('(prefers-color-scheme: dark)');
function applyTheme(theme) {
  document.documentElement.dataset.theme = theme;
  $('theme').textContent = theme === 'dark' ? '☀ Light mode' : '☾ Dark mode';
  $('theme').setAttribute('aria-label', `Switch to ${theme === 'dark' ? 'light' : 'dark'} mode`);
}
applyTheme(localStorage.getItem('tk2-theme') || (themeMedia.matches ? 'dark' : 'light'));
$('theme').onclick = () => {const theme = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark'; localStorage.setItem('tk2-theme', theme); applyTheme(theme);};
themeMedia.addEventListener('change', event => {if (!localStorage.getItem('tk2-theme')) applyTheme(event.matches ? 'dark' : 'light');});
function notice(message, error = false) {
  clearTimeout(noticeTimer); $('notice').textContent = message; $('notice').hidden = false;
  $('notice').classList.toggle('error', error);
  if (!error) noticeTimer = setTimeout(() => {$('notice').hidden = true;}, 6000);
}
async function api(path, body) {
  const response = await fetch('/api/' + path, {method: body === undefined ? 'GET' : 'POST', headers: {'X-TK2-Token': token, ...(body === undefined ? {} : {'Content-Type': 'application/json'})}, ...(body === undefined ? {} : {body: JSON.stringify(body)})});
  const data = await response.json();
  if (!response.ok) {const error = new Error(data.error || 'Request failed. Try again.'); error.details = data; throw error;}
  return data;
}
function el(tag, cls, text) {const node = document.createElement(tag); if (cls) node.className = cls; if (text !== undefined) node.textContent = text; return node;}
function updateSaveState() {
  const count = settingEdits.size + recipeEdits.size + extraEdits.size;
  settingsDirty = count > 0; $('save-settings').disabled = busy || !settingsDirty;
  $('save-settings').textContent = actionInFlight === 'settings' ? 'Saving…' : 'Save changes';
  $('settings-state').textContent = count ? `${count} unsaved setting${count === 1 ? '' : 's'}${livePreviewEnabled ? ' · eligible edits auto-save after a short pause.' : '.'}` : 'All changes saved.';
}
function recordEdit(edits, key, before, value) {
  const previous = edits.get(key), base = previous ? previous.base : before;
  // A reverted key stays dirty while an older value is being saved.
  if (Object.is(base, value) && actionInFlight !== 'settings') edits.delete(key);
  else edits.set(key, {base, value, revision: ++revision});
  updateSaveState();
}
function changeSetting(key, value, updateLibrary = true) {
  recordEdit(settingEdits, key, state.settings[key], value); state.settings[key] = value; syncModuleState(key.split('/')[0]); syncDependencies();
  if (updateLibrary && key.endsWith('/Enabled')) {renderCategoryFilters(); if (category === 'Enabled') {renderFeatures(); renderRecipes();}}
  else if (updateLibrary) scheduleLivePreview(key, settingEdits);
}
function changeEntry(entry, value, extra = false, updateLibrary = true) {
  recordEdit(extra ? extraEdits : recipeEdits, entry.section + '/' + entry.key, entry.value, value); entry.value = value;
  if (updateLibrary && entry.key === 'Enabled') {renderCategoryFilters(); if (category === 'Enabled') {renderFeatures(); renderRecipes();}}
  else if (updateLibrary) scheduleLivePreview(entry.section + '/' + entry.key, extra ? extraEdits : recipeEdits);
}
function scheduleLivePreview(key, edits) {
  if (!livePreviewEnabled || key.endsWith('/Enabled')) return;
  if (!edits.has(key) || !livePreviewEligible(key)) return;
  clearTimeout(livePreviewTimer);
  livePreviewTimer = setTimeout(() => {
    const keys = new Set([...settingEdits.keys(), ...recipeEdits.keys(), ...extraEdits.keys()].filter(livePreviewEligible));
    if (!keys.size) return;
    if (busy) {scheduleLivePreview(key, edits); return;}
    const snapshot = settingsSnapshot(keys);
    return runAction('settings', snapshot.body, snapshot);
  }, 600);
}
function livePreviewEligible(key) {
  if (key.endsWith('/Enabled')) return false;
  const section = key.slice(0, key.lastIndexOf('/'));
  const feature = state.features.find(item => item.id === section);
  return feature ? !feature.locked && feature.available !== false && state.settings[section + '/Enabled'] === true :
    [...(state.recipes || []), ...(state.extraSettings || [])].some(entry => entry.section === section && entry.key === 'Enabled' && entry.value.toLowerCase() === 'true');
}
function switchControl(label, value, disabled, change) {
  const wrapper = el('label', 'switch'); const input = el('input'); input.type = 'checkbox'; input.checked = value; input.disabled = disabled; input.setAttribute('aria-label', label);
  input.onchange = () => change(input.checked); wrapper.append(input, el('span')); return wrapper;
}
function bindAccordion(trigger, panel, key, expanded, onOpen) {
  panel.hidden = !expanded;
  trigger.dataset.accordionKey = key;
  trigger.setAttribute('aria-expanded', String(expanded));
  trigger.setAttribute('aria-controls', panel.id);
  trigger.onAccordionOpen = onOpen;
  if (expanded) onOpen?.();
  trigger.onclick = () => {
    const open = panel.hidden;
    panel.hidden = !open;
    trigger.setAttribute('aria-expanded', String(open));
    if (open) {expandedModules.add(key); trigger.onAccordionOpen?.();} else expandedModules.delete(key);
  };
}
function setAllAccordions(expanded) {
  let previousCount;
  do {
    const accordions = document.querySelectorAll('[data-accordion-key]');
    previousCount = accordions.length;
    accordions.forEach(accordion => {
      const key = accordion.dataset.accordionKey;
      if (expanded) expandedModules.add(key); else expandedModules.delete(key);
      if (accordion.tagName === 'DETAILS') {accordion.open = expanded; return;}
      const panel = $(accordion.getAttribute('aria-controls'));
      if (!panel) return;
      panel.hidden = !expanded;
      accordion.setAttribute('aria-expanded', String(expanded));
      if (expanded) accordion.onAccordionOpen?.();
    });
  } while (expanded && document.querySelectorAll('[data-accordion-key]').length > previousCount);
}
function chevronIcon() {
  const namespace = 'http://www.w3.org/2000/svg';
  const icon = document.createElementNS(namespace, 'svg'); icon.setAttribute('class', 'expand-arrow');
  icon.setAttribute('viewBox', '0 0 24 24'); icon.setAttribute('aria-hidden', 'true');
  const path = document.createElementNS(namespace, 'path');
  path.setAttribute('d', 'M16.59 8.59 12 13.17 7.41 8.59 6 10l6 6 6-6z'); icon.append(path);
  return icon;
}
function featureGroup(feature) {return feature.gameplay ? 'Gameplay' : feature.category || 'Other';}
function renderCategoryFilters() {
  if (!state) return;
  const filters = $('filters'); filters.replaceChildren();
  const counts = moduleCounts();
  const categories = [...new Set(state.features.map(featureGroup))].sort((a, b) => a.localeCompare(b));
  const options = [['All', 'All'], ...categories.map(name => [name, name])];
  options.forEach(([id, label]) => {
    const button = el('button', 'chip', label); button.dataset.category = id;
    button.classList.toggle('selected', category === id); button.setAttribute('aria-pressed', String(category === id));
    button.onclick = () => {category = id; renderCategoryFilters(); renderFeatures(); renderRecipes();};
    filters.append(button);
  });
  const enabled = $('show-enabled');
  enabled.textContent = `Enabled modules · ${counts.enabled}`;
  enabled.setAttribute('aria-pressed', String(category === 'Enabled'));
  enabled.classList.toggle('selected', category === 'Enabled');
  $('disable-all-enabled').hidden = category !== 'Enabled';
  $('disable-all-enabled').disabled = busy || counts.unlocked === 0;
  const preview = $('apply-adjustments');
  preview.textContent = `Apply as you adjust · ${livePreviewEnabled ? 'On' : 'Off'}`;
  preview.setAttribute('aria-pressed', String(livePreviewEnabled));
}
function countEnabledSections(entries, onlyEnabled = false) {
  const sections = new Map();
  (entries || []).forEach(entry => {
    if (!sections.has(entry.section)) sections.set(entry.section, []);
    sections.get(entry.section).push(entry);
  });
  let count = 0;
  sections.forEach(settings => {
    const toggle = settings.find(entry => entry.key === 'Enabled' && entry.type === 'Boolean');
    if (!onlyEnabled || toggle?.value.toLowerCase() === 'true') count++;
  });
  return count;
}
function moduleCounts() {
  const featureEnabled = state.features.filter(feature => feature.locked || state.settings[feature.id + '/Enabled']).length;
  const featureUnlocked = state.features.filter(feature => !feature.locked && state.settings[feature.id + '/Enabled']).length;
  const extraCount = countEnabledSections(state.extraSettings || []);
  const recipeCount = countEnabledSections(state.recipes || []);
  return {
    all: state.features.length + extraCount + recipeCount,
    enabled: featureEnabled + countEnabledSections(state.extraSettings || [], true) + countEnabledSections(state.recipes || [], true),
    unlocked: featureUnlocked + countEnabledSections(state.extraSettings || [], true) + countEnabledSections(state.recipes || [], true)
  };
}
function updateModuleCount() {
  const total = visibleModules + visibleRecipeModules, search = $('mod-search').value.trim();
  $('module-count').textContent = `${total} module${total === 1 ? '' : 's'} shown${search ? ' for “' + search + '”' : ''}.`;
}
function packsForState() {
  const packs = (state.packs || []).map(pack => ({...pack, features: (pack.features || []).map(id => typeof id === 'object' ? id : state.features.find(feature => feature.id === id)).filter(Boolean)}));
  const included = new Set(packs.flatMap(pack => pack.features.map(feature => feature.id)));
  state.features.filter(feature => !included.has(feature.id)).forEach(feature => {
    const id = feature.pack || (feature.origin?.toLowerCase().includes('mk') ? 'mks' : 'garage');
    let pack = packs.find(item => item.id === id);
    if (!pack) {pack = {id, name: id === 'mks' ? "Community Mods" : 'Toolkit Essentials', description: '', features: []}; packs.push(pack);}
    pack.features.push(feature);
  });
  return packs;
}
function settingsControl(key, label, kind, value, low, high, description, choices, onChange, dataset = 'setting', defaultValue) {
  const control = el('div', 'setting'), copy = el('label', 'setting-copy'); copy.append(el('span', 'setting-label', label));
  if (description) copy.append(el('small', 'setting-description', description));
  const defaultText = defaultValue === '' ? 'empty' : defaultValue === undefined ? 'not supplied' : String(defaultValue);
  const allowed = choices?.length ? (choices.length > 8 ? choices.slice(0,8).join(', ') + ' … (all choices in dropdown)' : choices.join(', ')) : kind === 'bool' ? 'on / off' : low !== undefined && low !== null ? `${low} to ${high}${kind === 'int' ? ' (whole numbers)' : ''}` : 'single-line text';
  copy.append(el('small', 'setting-hint', `Default: ${defaultText} · Allowed: ${allowed}`));
  control.append(copy);
  const input = el(choices?.length ? 'select' : 'input');
  if (choices?.length) {
    choices.forEach(choice => {const option = el('option', '', String(choice)); option.value = choice; input.append(option);});
    if (kind === 'text' && value !== undefined && value !== null && !choices.some(choice => String(choice) === String(value))) {
      const legacy = el('option', '', `Saved custom: ${value}`); legacy.value = value; input.append(legacy);
    }
  }
  else {
    input.type = kind === 'text' ? 'text' : kind === 'bool' ? 'checkbox' : 'number';
    if (!['text', 'bool'].includes(kind)) {if (low !== null && low !== undefined) input.min = low; if (high !== null && high !== undefined) input.max = high; input.step = kind === 'int' ? '1' : 'any';}
  }
  if (kind === 'bool' && !choices?.length) input.checked = Boolean(value); else input.value = value;
  input.id = 'setting-' + encodeURIComponent(key); copy.setAttribute('for', input.id);
  input.dataset[dataset] = key; input.disabled = !state.game || busy && actionInFlight !== 'settings'; input.setAttribute('aria-label', label);
  const changed = () => onChange(kind === 'bool' && !choices?.length ? input.checked : ['text', 'select'].includes(kind) || choices?.length ? input.value : input.value === '' ? '' : Number(input.value));
  input.oninput = changed; input.onchange = changed;
  const tools = el('div', 'value-tools'), reset = el('button', 'value-reset', 'Reset');
  reset.setAttribute('aria-label', 'Reset ' + label + ' to default'); reset.title = `Default: ${defaultText}`;
  reset.disabled = defaultValue === undefined || input.disabled;
  reset.onclick = () => {onChange(defaultValue); if (input.type === 'checkbox') input.checked = Boolean(defaultValue); else input.value = defaultValue; syncControls(); notice(label + ' restored.' + (livePreviewEnabled ? ' Applying live preview.' : ' Save changes to apply.'));};
  tools.append(input, reset); input.linkedControls = [reset]; control.field = input; control.tools = tools;
  if (['float','int'].includes(kind) && low !== null && low !== undefined && high !== null && high !== undefined && Number(high) - Number(low) <= 10000) {
    const slider = el('input', 'value-slider'); slider.type = 'range'; slider.min = low; slider.max = high;
    slider.step = kind === 'int' ? 1 : Math.max(.001, (Number(high) - Number(low)) / 1000); slider.value = value;
    slider.setAttribute('aria-label', label + ' slider'); slider.disabled = input.disabled;
    slider.oninput = () => {input.value = slider.value; changed();}; input.oninput = () => {slider.value = input.value; changed();};
    input.slider = slider; input.linkedControls.push(slider); const stack = el('div', 'value-stack'); stack.append(tools, slider); control.append(stack);
  } else control.append(tools);
  input.defaultAvailable = defaultValue !== undefined; return control;
}
function syncModuleState(id) {const feature = state.features.find(item => item.id === id); document.querySelectorAll('[data-module-state]').forEach(node => {if (node.dataset.moduleState === id) node.textContent = feature?.locked ? 'Always on' : state.settings[id + '/Enabled'] ? 'On' : 'Off';});}
function syncDependencies() {
  document.querySelectorAll('[data-setting]').forEach(input => {
    const feature = state.features.find(item => item.id === input.dataset.setting.split('/')[0]);
    input.disabled = !state.game || busy && actionInFlight !== 'settings' || feature?.available === false || feature?.locked === true || Boolean(input.dataset.requires && !state.settings[input.dataset.requires]);
    if (input.dataset.requires) input.setAttribute('aria-description', input.disabled ? 'Turn on this parameter’s override switch to edit its custom value.' : 'Custom override value.');
  });
  document.querySelectorAll('[data-setting], [data-recipe], [data-extra]').forEach(input => {
    (input.linkedControls || []).forEach(link => {link.disabled = link.tagName === 'BUTTON' ? !state.game || busy && actionInFlight !== 'settings' || !input.defaultAvailable : input.disabled;});
    if (input.slider && input.slider !== document.activeElement) input.slider.value = input.value;
  });
}
function parameterGroup(feature, key) {
  key = key.replace(/^Override/, '');
  if (feature.id === 'BoostParameters') {
    if (/Pad/.test(key)) return 'Boost pads';
    if (/Air|Landing/.test(key)) return 'Air and landing';
    if (/Reserves|Triple|Continuous/.test(key)) return 'Reserves and speed';
    return 'Drift boost timing and strength';
  }
  if (/InstantBoost/.test(key)) return 'Boost delivery';
  if (/Brak|Break|Friction|Drag/.test(key)) return 'Braking and friction';
  if (/Steer|Drift|Counter/.test(key)) return 'Steering and drifting';
  if (/Jump|Vertical/.test(key)) return 'Jump and vertical motion';
  if (/Mass/.test(key)) return 'Kart body';
  return 'Speed and acceleration';
}
function featureSettings(feature, search) {
  const container = el('div');
  const required = new Set(feature.settings.map(setting => setting[8]).filter(Boolean));
  const groups = new Map();
  feature.settings.filter(setting => !required.has(setting[0])).forEach(setting => {
    const group = feature.settingGroups?.[setting[0]] || (feature.settings.length >= 16 ? parameterGroup(feature, setting[0]) : '');
    if (!groups.has(group)) groups.set(group, []); groups.get(group).push(setting);
  });
  groups.forEach((settings, name) => {
    const controls = el('div', 'settings-grid');
    settings.forEach(([key, label, kind, fallback, low, high, description, choices, requires]) => {
      const fullKey = feature.id + '/' + key;
      const control = settingsControl(fullKey, label, kind, state.settings[fullKey] ?? fallback, low, high, description, choices, value => changeSetting(fullKey, value), 'setting', fallback);
      if (feature.locked) control.querySelectorAll('input,select,button').forEach(input => {input.disabled = true;});
      if (requires) {
        const requireKey = requires.includes('/') ? requires : feature.id + '/' + requires;
        const input = control.field; input.dataset.requires = requireKey; input.disabled = busy && actionInFlight !== 'settings' || !state.settings[requireKey];
        const toggle = switchControl('Override ' + label, Boolean(state.settings[requireKey]), busy && actionInFlight !== 'settings', value => changeSetting(requireKey, value));
        toggle.children[0].dataset.setting = requireKey;
        const paired = el('div', 'parameter-controls'), resetOverride = el('button', 'value-reset', 'Reset override');
        resetOverride.onclick = () => {changeSetting(requireKey, false); syncControls();}; paired.append(toggle, resetOverride);
        control.prepend ? control.insertBefore(paired, control.children[1]) : control.append(paired);
      }
      controls.append(control);
    });
    if (name) {
      const details = el('details', 'parameter-group'), identity = feature.id + ':' + name;
      details.dataset.accordionKey = identity;
      details.open = expandedModules.has(identity) || Boolean(search && settings.some(setting => [name, setting[0], setting[1], setting[6], setting[8], setting[8] ? 'Override ' + setting[1] : ''].join(' ').toLowerCase().includes(search)));
      details.ontoggle = () => {if (details.open) expandedModules.add(identity); else expandedModules.delete(identity);};
      details.append(el('summary', '', name), controls); container.append(details);
    } else container.append(controls);
  });
  if (!feature.settings.length && feature.available !== false) container.append(el('p', 'module-note', 'This module has no additional settings.'));
  return container;
}
function populateFeaturePanel(panel, feature, search) {
  if (!panel.dataset.lazyModule) return;
  delete panel.dataset.lazyModule;
  if (feature.available === false) panel.append(el('p', 'module-note', feature.reason || 'This module needs a compatibility update before it can be enabled.'));
  if (feature.status && feature.status !== 'verified') panel.append(el('p', 'module-note', feature.status));
  const moduleTools = el('div', 'module-tools');
  moduleTools.append(el('small', '', feature.locked ? 'Mandatory protection stays on. Online actions are blocked while unapproved modules or plugins are active.' : 'Reset restores all defaults and switches this module off.'));
  panel.append(moduleTools);
  const edit = el('button', 'secondary', 'Edit code'); edit.dataset.editModule = feature.id;
  edit.disabled = busy || !state.moduleSources?.[feature.id]?.length;
  edit.onclick = () => editModule(feature.id); moduleTools.append(edit);
  panel.append(featureSettings(feature, search));
}
function extraPackId(entry) {
  const id = entry.pack || state.extraPacks?.[entry.section] || (/^(MK\.|Alternate|AutoBoost|BoostTrainer|Bobby|Boring|CNK|CustomPhysics|Dash|FastRespawn|Mirror|Reverse|Proximity|SaveState|Supra|Teleport)/.test(entry.section) ? 'mks' : 'community');
  return id === 'mks' && !state.packs?.some(pack => pack.id === 'mks') ? 'community' : id;
}
function renderFeatureCard(feature, search) {
  const unavailable = feature.available === false;
  const row = el('article', 'module'); row.dataset.feature = feature.id;
  const heading = el('div', 'module-heading'), expand = el('button', 'module-expand'), info = el('span', 'module-info');
  info.append(el('span', 'module-name', feature.name));
  if (feature.description) info.append(el('span', 'module-description', feature.description));
  const enabled = feature.locked || Boolean(state.settings[feature.id + '/Enabled']);
  const status = el('span', 'module-state', feature.locked ? 'Always on' : enabled ? 'On' : 'Off'); status.dataset.moduleState = feature.id;
  const panel = el('div', 'module-body'); panel.id = 'module-' + feature.id; panel.hidden = !(expandedModules.has(feature.id) || Boolean(search));
  panel.dataset.lazyModule = feature.id;
  expand.append(info, status, chevronIcon());
  bindAccordion(expand, panel, feature.id, !panel.hidden, () => populateFeaturePanel(panel, feature, search));
  const toggle = feature.locked ? el('span', 'module-lock-badge', 'Locked') : switchControl('Enable ' + feature.name, enabled, unavailable || busy && actionInFlight !== 'settings', value => changeSetting(feature.id + '/Enabled', value));
  if (!feature.locked) toggle.children[0].dataset.setting = feature.id + '/Enabled';
  const headerReset = el('button', 'quiet module-reset', 'Reset');
  headerReset.setAttribute('aria-label', 'Reset ' + feature.name + ' to defaults'); headerReset.hidden = Boolean(feature.locked);
  headerReset.onclick = () => resetFeatures([feature]);
  heading.append(expand, headerReset, toggle); row.append(heading, panel);
  return row;
}
function renderFeatures() {
  const list = $('feature-list'); list.replaceChildren();
  const search = $('mod-search').value.toLowerCase().trim(); let count = 0, moduleCount = 0;
  packsForState().forEach(pack => {
    const features = pack.features.filter(feature => {
      const matches = category === 'All' || category === 'Enabled' && (feature.locked || state.settings[feature.id + '/Enabled']) || category === featureGroup(feature);
      return matches && [pack.name, feature.name, feature.description, ...feature.settings.map(setting => setting[1])].join(' ').toLowerCase().includes(search);
    });
    const extras = (state.extraSettings || []).filter(entry => extraPackId(entry) === pack.id);
    const extraGroups = extraGroupsForFilter(extras, search, pack.name);
    if (!features.length && !extraGroups.size) return;
    count += features.length + extraGroups.size; moduleCount += features.length + extraGroups.size;
    const section = el('section', 'mod-pack'); section.dataset.pack = pack.id;
    const header = el('header', 'pack-heading'), copy = el('div'); copy.append(el('h2', '', pack.name));
    if (pack.description) copy.append(el('p', '', pack.description));
    const packActions = el('div', 'pack-actions'), reset = el('button', 'quiet', 'Reset pack'); reset.onclick = () => resetFeatures(pack.features); packActions.append(el('small', '', `${features.length + extraGroups.size} modules`), reset); header.append(copy, packActions); section.append(header);
    const grouped = new Map();
    features.forEach(feature => {const group = featureGroup(feature); if (!grouped.has(group)) grouped.set(group, []); grouped.get(group).push(feature);});
    grouped.forEach((groupFeatures, groupName) => {
      const categorySection = el('section', 'feature-category'); categorySection.dataset.featureCategory = groupName;
      const categoryHeading = el('header', 'feature-category-heading');
      categoryHeading.append(el('h3', '', groupName), el('small', '', `${groupFeatures.length} module${groupFeatures.length === 1 ? '' : 's'}`));
      categorySection.append(categoryHeading);
      groupFeatures.forEach(feature => categorySection.append(renderFeatureCard(feature, search)));
      section.append(categorySection);
    });
    appendEntryGroups(section, extraGroups, true); list.append(section);
  });
  visibleModules = moduleCount; updateModuleCount();
  if (!count) list.append(el('p', 'empty-state', category === 'Enabled' ? 'No modules are enabled. Choose All to add one.' : 'No settings match. Try another search or filter.'));
}
function extraGroupsForFilter(entries, search, packName = '') {
  const groups = new Map();
  entries.forEach(entry => {if (!groups.has(entry.section)) groups.set(entry.section, []); groups.get(entry.section).push(entry);});
  groups.forEach((settings, name) => {
    const enabled = settings.find(entry => entry.key === 'Enabled')?.value === 'true';
    if (category === 'Enabled' && !enabled || category !== 'All' && category !== 'Enabled' && category !== 'Gameplay' || ![packName, name, ...settings.flatMap(entry => [entry.key, entry.description])].join(' ').toLowerCase().includes(search)) groups.delete(name);
  });
  return groups;
}
function appendEntryGroups(container, groups, extra = false) {
  groups.forEach((settings, name) => {
    const title = name.replace(/^(Recipe\.|MK\.|Community\.)/, '').replace(/([a-z])([A-Z])/g, '$1 $2');
    const row = el('article', 'module'), heading = el('div', 'module-heading'), expand = el('button', 'module-expand');
    const info = el('span', 'module-info'); info.append(el('span', 'module-name', title));
    const enabled = settings.find(entry => entry.key === 'Enabled' && entry.type === 'Boolean');
    if (enabled?.description) info.append(el('span', 'module-description', enabled.description));
    const panel = el('div', 'module-body'); panel.id = 'section-' + name.replace(/[^A-Za-z0-9_-]/g, '-'); panel.hidden = !(expandedModules.has(name) || Boolean($('mod-search').value));
    expand.append(info, chevronIcon());
    bindAccordion(expand, panel, name, !panel.hidden);
    heading.append(expand);
    if (enabled) {
      const toggle = switchControl('Enable ' + title, enabled.value.toLowerCase() === 'true', busy && actionInFlight !== 'settings', value => changeEntry(enabled, String(value), extra));
      toggle.children[0].dataset[extra ? 'extra' : 'recipe'] = enabled.section + '/' + enabled.key; heading.append(toggle);
    }
    row.append(heading);
    const controls = el('div', 'settings-grid'); settings.filter(entry => entry !== enabled).forEach(entry => {
      const bounds = /^From (.+) to (.+)$/.exec(entry.range || ''), kind = entry.type === 'Boolean' ? 'select' : entry.type === 'Int32' ? 'int' : ['Single', 'Double'].includes(entry.type) ? 'float' : 'text';
      controls.append(settingsControl(entry.section + '/' + entry.key, entry.key.replace(/([a-z])([A-Z])/g, '$1 $2'), kind, entry.value, bounds?.[1], bounds?.[2], entry.description, entry.type === 'Boolean' ? ['false', 'true'] : entry.choices, value => changeEntry(entry, String(value), extra), extra ? 'extra' : 'recipe', entry.hasDefault === false ? undefined : entry.default));
    });
    if (settings.length === 1 && enabled) controls.append(el('p', 'module-note', 'This module has no additional settings.'));
    const reset = el('button', 'quiet', 'Reset module to defaults'); reset.onclick = () => {settings.forEach(entry => {if (entry.default !== undefined && entry.hasDefault !== false) changeEntry(entry, entry.default, extra);}); syncControls(); notice('Defaults restored. Save changes to apply.');};
    reset.className = 'quiet module-reset'; reset.textContent = 'Reset'; reset.setAttribute('aria-label', 'Reset ' + title + ' to defaults'); heading.append(reset);
    const tools = el('div', 'module-tools');
    const edit = el('button', 'secondary', 'Edit code'); edit.dataset.editModule = name; edit.disabled = busy || !state.moduleSources?.[name]?.length;
    edit.onclick = () => editModule(name); tools.append(edit); panel.append(tools, controls); row.append(panel); container.append(row);
  });
}
function renderRecipes() {
  const list = $('recipe-controls'); list.replaceChildren();
  const entries = state.recipes || []; visibleRecipeModules = 0; if (!entries.length) {updateModuleCount(); return;}
  const search = $('mod-search').value.toLowerCase().trim(), groups = extraGroupsForFilter(entries, search, 'Your recipes');
  visibleRecipeModules = groups.size;
  if (!groups.size) {updateModuleCount(); return;}
  const section = el('section', 'mod-pack'), header = el('header', 'pack-heading'); header.append(el('h2', '', 'Your recipes')); section.append(header);
  appendEntryGroups(section, groups); list.append(section); updateModuleCount();
}
function renderFiles() {
  $('reconstructed-files').replaceChildren(); $('pack-files').replaceChildren();
  const select = $('module-focus'); select.replaceChildren();
  [['all', 'All project files'], ['reconstructed', 'Reconstructed game logic'], ...state.features.map(f => [f.id, f.name]),
    ...Object.keys(state.moduleSources || {}).filter(id => id.startsWith('Recipe.') && !state.features.some(f => f.id === id)).map(id => [id, id.slice(7).replace(/([a-z])([A-Z])/g, '$1 $2') + ' (recipe)'])]
    .forEach(([value, label]) => {const option = el('option', '', label); option.value = value; select.append(option);});
  select.value = selectedModule;
  const allowed = state.moduleSources?.[selectedModule], query = $('file-query').value.toLowerCase().trim();
  $('module-source-note').textContent = selectedModule === 'reconstructed' ? 'Reviewed translations of selected native methods. Evidence and limits are in their comments.' : selectedModule === 'all' ? 'Select a module to show its behavior and settings files.' : 'These files implement this module. Shared files can affect other modules in the same pack.';
  state.files.forEach(file => {
    if (allowed && !allowed.includes(file) || selectedModule === 'reconstructed' && !file.startsWith('src/Reconstructed/') || !file.toLowerCase().includes(query)) return;
    const button = el('button', 'file-button', file.split('/').at(-1)); button.dataset.file = file; button.title = file; button.disabled = busy;
    const role = file.includes('/Recipes/') ? 'Your recipe' : /Settings|Plugin.cs|PackModules.cs/.test(file) ? 'Behavior & settings' : /Panel/.test(file) ? 'In-game panel' : file.startsWith('src/Reconstructed/') ? 'Reconstructed behavior' : 'C# implementation';
    button.append(el('small', 'file-role', role));
    button.classList.toggle('selected', source?.file === file); button.onclick = () => loadSource(file);
    $(file.startsWith('src/Reconstructed/') ? 'reconstructed-files' : 'pack-files').append(button);
  });
}
function renderInstallation() {
  const ready = state.setup || {};
  $('game-path').textContent = state.game || 'No game selected';
  if (document.activeElement !== $('game-directory')) $('game-directory').value = state.game || '';
  const compatibility = state.prebuiltCompatibility;
  $('plugin-status').textContent = `${state.installed ? 'Plugin installed' : 'Plugin not installed'} · ${state.pluginCount} active plugin DLL${state.pluginCount === 1 ? '' : 's'} · ${compatibility?.compatible ? 'prebuilt matches this game and loader' : compatibility?.reason || 'prebuilt compatibility has not been checked'}`;
  $('setup-message').textContent = ready.message || 'Check the installation.';
  $('setup-checks').replaceChildren();
  (ready.checks || []).forEach(check => {const row = el('li', check.ok ? 'check-ok' : 'check-pending', `${check.ok ? '✓' : '○'} ${check.name}`); $('setup-checks').append(row);});
  $('prepare-loader').disabled = busy || !state.game || !ready.loaderSource;
  $('prepare-loader').textContent = ready.stage === 'install-loader' ? 'Prepare BepInEx' : 'Repair BepInEx files';
  $('loader-help').textContent = ready.loaderSource ? 'BepInEx distribution included. Existing plugins and configs are preserved.' : `This portable package does not include BepInEx. Install the tested ${ready.loaderVersion || '6.0.0-be.788'} x64 loader first; the toolkit will verify it before installing.`;
  $('installation-install').disabled = busy || !ready.ready || state.packCurrent || compatibility?.compatible === false;
  $('installation-install').textContent = state.packCurrent ? 'Plugin up to date' : compatibility?.compatible === false ? 'Build for this installation first' : state.installed ? 'Update plugin (restart required)' : 'Install plugin';
  const updateAvailable = Boolean(ready.ready && state.installed && !state.packCurrent && compatibility?.compatible !== false);
  $('plugin-update-top').hidden = !updateAvailable;
  $('plugin-update-top').disabled = busy || !ready.ready;
  const release = state.release || {}, toolkitUpdate = $('toolkit-update-top');
  toolkitUpdate.hidden = release.state !== 'available';
  toolkitUpdate.disabled = busy;
  toolkitUpdate.textContent = release.canInstall ? `Update toolkit · ${release.version}` : `Toolkit ${release.version} available`;
  const plugins = state.plugins || [], others = plugins.filter(plugin => !plugin.toolkit);
  $('compatibility-summary').textContent = `Loader: ${ready.loaderVersion || 'not verified'} · ${ready.loaderCompatible ? 'supported build' : 'unsupported or not yet verified'}. ${others.length ? `${others.length} other plugin file${others.length === 1 ? '' : 's'} found; review them if the toolkit misbehaves.` : 'No other BepInEx plugins detected.'} Config files are never removed by this audit.`;
  const pluginList = $('plugin-audit-list'); pluginList.replaceChildren();
  plugins.forEach(plugin => {
    const row = el('li', 'compat-audit-item'), copy = el('span', 'compat-audit-copy');
    copy.append(el('span', '', plugin.relative), el('small', '', plugin.toolkit ? 'Toolkit plugin · protected' : plugin.enabled ? 'Other plugin · possible conflict, not confirmed' : 'Disabled plugin · available to restore'));
    row.append(copy);
    if (!plugin.toolkit) {
      const button = el('button', 'secondary', plugin.enabled ? 'Disable' : 'Enable'); button.disabled = busy;
      button.onclick = () => {if (!confirm(`${plugin.enabled ? 'Disable' : 'Enable'} ${plugin.relative}? The file is only renamed; nothing is deleted. Close the game first.`)) return; runAction('toggle-plugin', {path: plugin.relative});}; row.append(button);
    }
    pluginList.append(row);
  });
  if (!plugins.length) pluginList.append(el('li', 'empty-state', 'No plugin DLLs found.'));
  const configList = $('config-audit-list'); configList.replaceChildren();
  (state.configs || []).forEach(config => {
    const row = el('li', 'compat-audit-item'), copy = el('span', 'compat-audit-copy');
    copy.append(el('span', '', config.relative), el('small', '', config.enabled ? 'Config file · can be disabled and restored by renaming' : 'Disabled config · available to restore'));
    const button = el('button', 'secondary', config.enabled ? 'Disable config' : 'Restore config'); button.disabled = busy;
    button.onclick = () => {if (!confirm(`${config.enabled ? 'Disable' : 'Restore'} ${config.relative}? This only renames the file. Keep the associated plugin disabled if you do not want it to recreate a default config. Close the game first.`)) return; runAction('toggle-config', {path: config.relative});}; row.append(copy, button); configList.append(row);
  });
  if (!(state.configs || []).length) configList.append(el('li', 'empty-state', 'No other plugin configs found.'));
  $('open-game-folder').disabled = busy || !state.game;
  $('runtime-errors').textContent = [...(ready.runtimeErrors || []), ...(ready.runtimeWarnings || [])].join('\n') || 'No errors or warnings found in the available BepInEx log.';
  $('install-state').textContent = !ready.ready ? 'Setup needed' : state.installed ? (state.packCurrent ? 'Plugin up to date' : 'Plugin update available') : 'Ready to install';
  const list = $('backups'); list.replaceChildren();
  state.backups.forEach(backup => {
    const row = el('div', 'backup-row'), info = el('div'); info.append(el('p', '', backup.file), el('small', '', `${backup.id} · ${backup.existed ? 'restore previous contents' : 'remove newly installed file'}`));
    const button = el('button', 'secondary', 'Restore'); button.disabled = busy; button.onclick = () => runAction('restore', {id: backup.id}); row.append(info, button); list.append(row);
  });
  if (!state.backups.length) list.append(el('p', 'empty-state', 'Changes create backups here.'));
  $('logs').textContent = state.buildLog || state.logs.join('\n') || 'No build run in this session. Player installation uses the bundled plugin.';
  $('workshop-build-log').textContent = state.buildLog || 'No compiler run yet. Build pack validates your C#; Build & install also replaces the plugin with the game closed.';
}
function resetFeatures(features) {
  features.filter(feature => !feature.locked).forEach(feature => {changeSetting(feature.id + '/Enabled', false); feature.settings.forEach(setting => changeSetting(feature.id + '/' + setting[0], setting[3]));});
  syncControls(); notice('Defaults restored. Save changes to apply.');
}
function render() {renderCategoryFilters(); renderFeatures(); renderRecipes(); renderFiles(); renderInstallation(); renderSharing(); syncDependencies(); updateSaveState();}
function mergeState(next) {
  settingEdits.forEach((edit, key) => {next.settings[key] = edit.value;});
  [['recipes', recipeEdits], ['extraSettings', extraEdits]].forEach(([field, edits]) => {(next[field] || []).forEach(entry => {const edit = edits.get(entry.section + '/' + entry.key); if (edit) entry.value = edit.value;});});
  state = next;
}
function syncControls() {
  document.querySelectorAll('[data-setting]').forEach(input => {const value = state.settings[input.dataset.setting]; if (input === document.activeElement || value === undefined) return; if (input.type === 'checkbox') input.checked = Boolean(value); else input.value = value;});
  ['recipe', 'extra'].forEach(kind => {document.querySelectorAll('[data-' + kind + ']').forEach(input => {const entry = (state[kind === 'extra' ? 'extraSettings' : 'recipes'] || []).find(entry => entry.section + '/' + entry.key === input.dataset[kind]); if (entry && input !== document.activeElement) {if (input.type === 'checkbox') input.checked = entry.value.toLowerCase() === 'true'; else input.value = entry.value;}});});
  state.features.forEach(feature => syncModuleState(feature.id)); syncDependencies(); renderCategoryFilters(); updateSaveState();
}
async function refresh(preserveSettings = true, quiet = false) {
  const sequence = ++refreshSequence, next = await api('state'); if (sequence !== refreshSequence) return;
  const structure = value => [value.features, value.packs, value.files, ...( ['recipes', 'extraSettings'].map(field => (value[field] || []).map(entry => entry.section + '/' + entry.key)))];
  const changed = !state || JSON.stringify(structure(next)) !== JSON.stringify(structure(state));
  if (!preserveSettings) {settingEdits.clear(); recipeEdits.clear(); extraEdits.clear();}
  mergeState(next);
  if (!quiet || changed) render(); else {syncControls(); renderInstallation();}
}
function settingsSnapshot(onlyKeys) {
  const body = {values: {}, baseValues: {}, recipes: {}, baseRecipes: {}, extraSettings: {}, baseExtraSettings: {}, hash: state.configHash}, revisions = {};
  [['values', 'baseValues', settingEdits], ['recipes', 'baseRecipes', recipeEdits], ['extraSettings', 'baseExtraSettings', extraEdits]].forEach(([values, bases, edits]) => {
    revisions[values] = new Map(); edits.forEach((edit, key) => {if (onlyKeys && !onlyKeys.has(key)) return; body[values][key] = edit.value; body[bases][key] = edit.base; revisions[values].set(key, edit.revision);});
  }); return {body, revisions};
}
function acknowledgeSettings(snapshot, result) {
  // Newer keystrokes survive older responses and rebase against saved values.
  [['values', settingEdits], ['recipes', recipeEdits], ['extraSettings', extraEdits]].forEach(([field, edits]) => {
    snapshot.revisions[field].forEach((savedRevision, key) => {const current = edits.get(key); if (!current) return; if (current.revision === savedRevision || Object.is(current.value, snapshot.body[field][key])) edits.delete(key); else current.base = snapshot.body[field][key];});
  });
  if (result.settings) mergeState({...state, ...result, settings: {...result.settings}, recipes: result.recipes || state.recipes, extraSettings: result.extraSettings || state.extraSettings});
  conflictKeys = []; $('settings-error').hidden = true; $('reload-settings').hidden = true; updateSaveState();
}
async function saveSource() {
  if (!source || !sourceDirty) return;
  const content = $('code').value, result = await api('save-source', {file: source.file, hash: source.hash, content});
  source.hash = result.hash; source.content = content; sourceDirty = $('code').value !== content; $('save-source').disabled = !sourceDirty;
  updateEditorState();
}
async function loadSource(file, discard = false) {
  if (sourceDirty && !discard) {notice('Save the current C# before switching files, or use Reload file to discard your edits.', true); return false;}
  const sequence = ++sourceSequence; $('code').readOnly = true; $('editor-state').textContent = 'Loading file…';
  try {
    const loaded = await api('source?file=' + encodeURIComponent(file)); if (sequence !== sourceSequence) return false;
    source = loaded; $('code').value = source.content; $('source-name').textContent = file;
    sourceDirty = false; $('code').readOnly = busy; $('save-source').disabled = true; $('reload-source').disabled = busy;
    $('open-source-folder').disabled = busy; $('code').scrollTop = 0; updateEditorState(); renderFiles(); return true;
  } catch (error) {if (sequence === sourceSequence) {$('code').readOnly = busy || !source; updateEditorState(); notice(error.message, true);} return false;}
}
function updateEditorState() {
  const lines = $('code').value.split('\n').length;
  $('line-numbers').textContent = Array.from({length: lines}, (_, i) => i + 1).join('\n');
  $('editor-state').textContent = !source ? 'Choose a file' : `${sourceDirty ? 'Unsaved edits' : 'File saved'} · ${lines} lines`;
  $('line-numbers').scrollTop = $('code').scrollTop;
}
function editModule(id) {
  if (sourceDirty) {notice('Save your current C# before opening another module.', true); return;}
  const files = state.moduleSources?.[id]; if (!files?.length) {notice('Editable source is not included for this module.', true); return;}
  selectedModule = id; showView('workshop'); showWorkshopTab('editor'); renderFiles(); loadSource(files[0]);
}
function showWorkshopTab(name) {
  workshopTab = name;
  workshopTabs.forEach(tab => {const active = tab === name; $('panel-' + tab).hidden = !active; const button = $('tab-' + tab); button.classList.toggle('selected', active); button.setAttribute('aria-selected', String(active)); button.tabIndex = active ? 0 : -1;});
  if (name === 'api' && functionSequence === 0) searchFunctions().catch(error => notice(error.message, true));
}
function setBusy(value) {
  busy = value; ['workshop-install', 'build', 'create-recipe', 'diagnose', 'prepare-loader', 'installation-install', 'plugin-update-top', 'toolkit-update-top', 'scan-games', 'select-game', 'browse-game', 'browse-package', 'preview-package'].forEach(id => {$(id).disabled = value;});
  $('save-source').disabled = value || !sourceDirty; $('code').readOnly = value || !source; $('reload-source').disabled = value || !source; $('open-source-folder').disabled = value || !source; updateSaveState();
  document.querySelectorAll('[data-edit-module]').forEach(button => {button.disabled = value || !state?.moduleSources?.[button.dataset.editModule]?.length;});
  // Keep controls mounted and editable during a settings save.
  document.querySelectorAll('[data-setting], [data-recipe], [data-extra]').forEach(input => {const feature = state?.features.find(item => item.id === input.dataset.setting?.split('/')[0]); input.disabled = value && actionInFlight !== 'settings' || feature?.available === false || !state?.game;});
  syncDependencies(); if (state) {renderInstallation(); renderCategoryFilters(); updateSharingButtons();}
}
function settingsError(error) {
  conflictKeys = error.details?.conflicts || error.details?.conflictKeys || []; if (!Array.isArray(conflictKeys)) conflictKeys = Object.keys(conflictKeys);
  const conflict = conflictKeys.length > 0 || error.details?.code === 'settings_conflict';
  $('settings-error-message').textContent = conflict ? `${error.message} Your edits are still here. Reload the conflicting values, then adjust them again.` : `${error.message} Your edits are still here. Check the values and try saving again.`;
  $('settings-error').hidden = false; $('reload-settings').hidden = !conflict;
}
async function runAction(action, body = {}, snapshot) {
  if (busy) return; actionInFlight = action; setBusy(true);
  if (action !== 'settings') notice(action === 'build' || action === 'build-install' ? 'Compiling your plugin…' : action === 'install' ? 'Installing the bundled plugin…' : 'Working…');
  try {
    if (action === 'build' || action === 'build-install') await saveSource();
    const result = await api(action, body);
    if (action === 'settings') {
      acknowledgeSettings(snapshot, result); syncControls();
      // The write already succeeded. A failed follow-up poll must not turn it
      // into a misleading save error; the idle poll will retry.
      await refresh(true, true).catch(() => {});
      renderCategoryFilters(); renderFeatures(); renderRecipes();
    }
    else if (action === 'diagnose') {await refresh(true, true); notice(result.message);}
    else {await refresh(true); notice(result.message || 'Done');}
    if (action === 'create-recipe') {selectedModule = 'Recipe.' + body.name; showWorkshopTab('editor'); await loadSource(result.file);}
  } catch (error) {if (action === 'settings') settingsError(error); else notice(error.message, true);}
  finally {actionInFlight = undefined; setBusy(false); if (action !== 'settings') await refresh(true, true).catch(() => {});}
}
function showView(name) {
  document.querySelectorAll('.view').forEach(view => {view.hidden = view.id !== name;});
  document.querySelectorAll('.nav').forEach(button => {button.classList.toggle('active', button.dataset.view === name); button.setAttribute('aria-current', button.dataset.view === name ? 'page' : 'false');});
  $('breadcrumb').textContent = 'TOOLKIT / ' + ({mods: 'MOD PACKS', workshop: 'WORKSHOP', installation: 'INSTALLATION'}[name]); history.replaceState(null, '', '#' + name);
  if (name === 'workshop') showWorkshopTab(workshopTab);
}
document.querySelectorAll('.nav').forEach(button => button.onclick = () => showView(button.dataset.view));
document.querySelector('.brand').onclick = event => {event.preventDefault(); showView('mods');};
$('mod-search').oninput = () => {renderFeatures(); renderRecipes();};
$('apply-adjustments').onclick = () => {
  livePreviewEnabled = !livePreviewEnabled; localStorage.setItem('tk2-live-preview', String(livePreviewEnabled));
  if (!livePreviewEnabled) clearTimeout(livePreviewTimer);
  renderCategoryFilters();
  notice(livePreviewEnabled ? 'Live preview is on for edits to enabled modules. Module on/off changes still need Save changes.' : 'Live preview is off. All edits wait for Save changes.');
};
$('show-enabled').onclick = () => {category = category === 'Enabled' ? 'All' : 'Enabled'; renderCategoryFilters(); renderFeatures(); renderRecipes();};
$('disable-all-enabled').onclick = () => {
  if (busy) return;
  let disabled = 0;
  state.features.filter(feature => !feature.locked && state.settings[feature.id + '/Enabled']).forEach(feature => {changeSetting(feature.id + '/Enabled', false, false); disabled++;});
  ['extraSettings', 'recipes'].forEach(field => {
    (state[field] || []).forEach(entry => {if (entry.key === 'Enabled' && entry.type === 'Boolean' && entry.value.toLowerCase() === 'true') {changeEntry(entry, 'false', field === 'extraSettings', false); disabled++;}});
  });
  renderCategoryFilters(); renderFeatures(); renderRecipes();
  notice(`${disabled} unlocked module${disabled === 1 ? '' : 's'} disabled. Online protection remains on; save changes to apply.`);
};
$('expand-all-accordions').onclick = () => setAllAccordions(true);
$('collapse-all-accordions').onclick = () => setAllAccordions(false);
$('workshop-install').onclick = () => runAction('build-install');
$('installation-install').onclick = () => runAction('install'); $('build').onclick = () => runAction('build');
$('plugin-update-top').onclick = () => runAction('install');
$('toolkit-update-top').onclick = async () => {
  const release = state?.release || {};
  if (release.canInstall) {
    try {const result = await api('update-toolkit', {}); notice(result.message || 'Toolkit update is ready.');}
    catch (error) {notice(error.message, true);}
  } else {
    try {const result = await api('open-release', {}); notice(result.message);}
    catch (error) {notice(error.message, true);}
  }
};
$('create-recipe').onclick = () => {if (sourceDirty) {notice('Save the current C# before creating another recipe.', true); return;} runAction('create-recipe', {name: $('recipe-name').value.trim()});};
$('diagnose').onclick = () => runAction('diagnose');
$('prepare-loader').onclick = () => runAction('install-loader');
$('open-game-folder').onclick = () => runAction('open-game-folder');
$('scan-games').onclick = async () => {try {const result = await api('scan-games', {}); $('game-candidates').replaceChildren(); result.games.forEach(path => {const button = el('button', 'file-button', path); button.onclick = () => {$('game-directory').value = path;}; $('game-candidates').append(button);}); if (!result.games.length) notice('No Steam installation found. Paste the game folder below.', true);} catch (error) {notice(error.message, true);}};
$('select-game').onclick = async () => {if (settingsDirty || sourceDirty) {notice('Save your edits before switching game installations.', true); return;} await runAction('select-game', {path: $('game-directory').value.trim(), discardEdits: true});};
$('save-settings').onclick = () => {
  const invalid = [...document.querySelectorAll('[data-setting], [data-recipe], [data-extra]')].find(input => {
    const dirty = input.dataset.setting ? settingEdits.has(input.dataset.setting) : input.dataset.recipe ? recipeEdits.has(input.dataset.recipe) : extraEdits.has(input.dataset.extra);
    return dirty && !input.checkValidity();
  });
  if (invalid) {settingsError(new Error(`${invalid.getAttribute('aria-label') || 'A setting'} is outside its allowed range${invalid.min && invalid.max ? ` (${invalid.min} to ${invalid.max})` : ''}.`)); return;}
  const snapshot = settingsSnapshot(); return runAction('settings', snapshot.body, snapshot);
};
$('reload-settings').onclick = async () => {
  if (!confirm('Discard your edits to the conflicting settings and reload their current values? Other edits will be kept.')) return;
  if (conflictKeys.length) conflictKeys.forEach(key => {settingEdits.delete(key); recipeEdits.delete(key); extraEdits.delete(key);});
  else {settingEdits.clear(); recipeEdits.clear(); extraEdits.clear();}
  try {await refresh(true); conflictKeys = []; $('settings-error').hidden = true; $('reload-settings').hidden = true;} catch (error) {settingsError(error);}
};
$('code').oninput = () => {sourceDirty = Boolean(source) && $('code').value !== source.content; $('save-source').disabled = busy || !sourceDirty; updateEditorState();};
$('code').onscroll = () => {$('line-numbers').scrollTop = $('code').scrollTop;};
$('file-query').oninput = renderFiles;
$('module-focus').onchange = () => {if (sourceDirty) {$('module-focus').value = selectedModule; notice('Save your C# before switching modules.', true); return;} selectedModule = $('module-focus').value; renderFiles(); const files = state.moduleSources?.[selectedModule]; if (files?.length) loadSource(files[0]);};
$('open-source-folder').onclick = () => {if (source) runAction('open-source-folder', {file: source.file});};
$('find-next').onclick = () => {
  const text = $('code').value, query = $('code-find').value; if (!query) return;
  const start = $('code').selectionEnd || 0; let index = text.toLowerCase().indexOf(query.toLowerCase(), start); if (index < 0) index = text.toLowerCase().indexOf(query.toLowerCase());
  if (index < 0) {notice('No matching text in this file.'); return;}
  $('code').focus(); $('code').setSelectionRange(index, index + query.length);
  $('code').scrollTop = Math.max(0, (text.slice(0, index).split('\n').length - 4) * 22.1); $('code').onscroll();
};
$('code-find').onkeydown = event => {if (event.key === 'Enter') $('find-next').click();};
workshopTabs.forEach((name, index, names) => {const button = $('tab-' + name); button.onclick = () => showWorkshopTab(name); button.onkeydown = event => {if (['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) {event.preventDefault(); const next = event.key === 'Home' ? 0 : event.key === 'End' ? names.length - 1 : (index + (event.key === 'ArrowRight' ? 1 : names.length - 1)) % names.length; showWorkshopTab(names[next]); $('tab-' + names[next]).focus();}};});
$('tutorial-create').onclick = () => {if (sourceDirty) {notice('Save your current C# first.', true); return;} $('recipe-name').value = 'MyKartHop'; return runAction('create-recipe', {name: 'MyKartHop'});};
$('tutorial-frame').onclick = () => editModule('Recipe.FrameLimiter');
$('save-source').onclick = async () => {try {await saveSource(); notice('C# saved. Build the plugin to apply your code.');} catch (error) {notice(error.message, true);}};
$('reload-source').onclick = () => {if (sourceDirty && !confirm('Discard unsaved editor changes and reload this file?')) return; loadSource(source.file, true);};
$('code').onkeydown = event => {if (event.key === 'Tab') {event.preventDefault(); const node = event.target; node.setRangeText('    ', node.selectionStart, node.selectionEnd, 'end'); node.dispatchEvent(new Event('input'));}};
document.addEventListener('keydown', event => {if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {event.preventDefault(); if (location.hash === '#workshop' && sourceDirty && !busy) $('save-source').click(); else if (settingsDirty && !busy) $('save-settings').click();}});
window.addEventListener('beforeunload', event => {if (sourceDirty || settingsDirty || busy) {event.preventDefault(); event.returnValue = '';}});
$('pseudocode-search').onclick = () => searchPseudocode().catch(error => notice(error.message, true));
$('pseudocode-query').onkeydown = event => {if (event.key === 'Enter') $('pseudocode-search').click();};
$('pseudocode-more').onclick = () => searchPseudocode(true).catch(error => notice(error.message, true));
showView(['mods', 'workshop', 'installation'].includes(location.hash.slice(1)) ? location.hash.slice(1) : 'mods');
refresh().then(() => {if (!state.setup?.ready && !state.game) showView('installation');}).catch(error => {notice('Toolkit could not load: ' + error.message, true);});
setInterval(() => {if (state && !busy && !document.hidden) refresh(true, true).catch(() => {});}, 5000);

async function searchFunctions(more = false) {
  if (more && (functionLoading || nextFunctions == null)) return;
  const sequence = ++functionSequence, offset = more ? nextFunctions : 0;
  functionLoading = true; $('function-loading').hidden = false; $('function-end').hidden = true; $('function-scroll').setAttribute('aria-busy', 'true');
  if (!more) {
    nextFunctions = null; $('function-list').replaceChildren(); $('function-scroll').scrollTop = 0; $('function-count').textContent = 'Loading functions…';
    ++detailSequence; $('function-name').textContent = 'Choose a function'; $('function-owner').textContent = '';
    $('function-status').textContent = 'Select a method name to inspect its API.'; $('function-detail').textContent = 'The signature appears here.';
    $('open-function-source').hidden = true; $('function-example').hidden = true; $('function-guidance').textContent = '';
  }
  try {
    const result = await api('functions?q=' + encodeURIComponent($('function-query').value) + '&offset=' + offset + '&recovered=' + $('functions-recovered').checked + '&topic=' + $('function-topic').value);
    if (sequence !== functionSequence) return;
    $('function-count').textContent = result.message || `${result.total.toLocaleString()} results · ${result.indexed.toLocaleString()} indexed declarations · ${result.reconstructed} reconstructed implementations`;
    (result.methods || []).forEach(method => {
      const name = method.name || method.signature.split('(')[0].split(' ').at(-1);
      const button = el('button', 'function-row'); button.dataset.function = method.id;
      button.append(el('strong', '', method.label || name), el('small', '', method.type), el('span', 'api-badge', method.source ? 'Reconstructed C#' : 'API declaration'));
      button.setAttribute('aria-pressed', 'false'); button.onclick = () => selectFunction(method); $('function-list').append(button);
    });
    nextFunctions = result.next;
    $('function-end').hidden = nextFunctions != null; $('function-end').textContent = result.total ? 'End of results' : 'No functions match. Try another category or All indexed functions.';
  } catch (error) {
    if (sequence === functionSequence) {$('function-end').hidden = false; $('function-end').textContent = 'Could not load functions. Choose Search to retry.'; notice(error.message, true);}
  } finally {
    if (sequence === functionSequence) {functionLoading = false; $('function-loading').hidden = true; $('function-scroll').setAttribute('aria-busy', 'false');}
  }
}
async function selectFunction(method) {
  const sequence = ++detailSequence;
  document.querySelectorAll('[data-function]').forEach(button => {const active = button.dataset.function === method.id; button.classList.toggle('selected', active); button.setAttribute('aria-pressed', String(active));});
  $('function-name').textContent = method.label || method.name || method.type; $('function-owner').textContent = method.type;
  $('function-status').textContent = 'Loading reference…'; $('open-function-source').hidden = true; $('function-example').hidden = true;
  try {
    const detail = await api('function?id=' + encodeURIComponent(method.id)); if (sequence !== detailSequence) return;
    $('function-name').textContent = detail.label || method.name || method.type; $('function-owner').textContent = detail.type;
    $('function-status').textContent = detail.status + (detail.confidence ? '. ' + detail.confidence : '.'); $('function-detail').textContent = detail.declaration;
    $('function-guidance').textContent = detail.access === 'private' || detail.access === 'internal' ? `${detail.access} game method: inspect existing adapters or a complete-signature Harmony hook before using it. This declaration has no editable body.` : 'This signature lists the return type and parameters. A declaration alone does not prove runtime behavior.';
    $('open-function-source').hidden = !detail.source; $('open-function-source').onclick = () => {if (sourceDirty) {notice('Save your current C# first.', true); return;} selectedModule = 'reconstructed'; showWorkshopTab('editor'); loadSource(detail.source);};
    const usage = {
      'PixelKartPhysics.AddVelocity': 'ReadableGame.AddVelocity(kart, Vector3.up * _hop.Value);',
      'PixelKartPhysics.JumpInput': 'ReadableGame.JumpInput(kart, true);',
      'PixelGameKartCamera.GetCamera': 'var gameCamera = ReadableGame.GetCamera(camera);',
      'PixelGameKartCamera.IsCameraASpectatorCamera': 'bool spectator = ReadableGame.IsSpectator(camera);',
    }[detail.type + '.' + detail.name];
    $('function-example').hidden = !usage; $('function-usage').textContent = usage || '';
  } catch (error) {if (sequence === detailSequence) {$('function-status').textContent = 'Reference unavailable. Select the function to retry.'; notice(error.message, true);}}
}
async function searchPseudocode(more = false) {
  if (more && (pseudocodeLoading || nextPseudocode == null)) return;
  const sequence = ++pseudocodeSequence, offset = more ? nextPseudocode : 0;
  pseudocodeLoading = true; $('pseudocode-more').disabled = true;
  if (!more) {
    nextPseudocode = null; $('pseudocode-list').replaceChildren(); $('pseudocode-more').hidden = true;
    $('pseudocode-count').textContent = 'Searching local Ghidra pseudocode…';
    ++pseudocodeDetailSequence; $('pseudocode-name').textContent = 'Choose a result';
    $('pseudocode-address').textContent = ''; $('pseudocode-viewer').textContent = 'Select a match to inspect its native pseudocode.';
  }
  try {
    const result = await api('pseudocode?q=' + encodeURIComponent($('pseudocode-query').value) + '&offset=' + offset);
    if (sequence !== pseudocodeSequence) return;
    const status = result.message || 'Ghidra native pseudocode.';
    $('pseudocode-count').textContent = `${result.total.toLocaleString()} matches across ${result.indexed.toLocaleString()} local exports. ${status}`;
    (result.results || []).forEach(item => {
      const button = el('button', 'pseudocode-row'); button.dataset.pseudocode = item.id;
      button.append(el('strong', '', item.name), el('small', '', item.address));
      button.append(el('span', 'pseudocode-snippet', item.snippet));
      button.setAttribute('aria-pressed', 'false'); button.onclick = () => openPseudocode(item);
      $('pseudocode-list').append(button);
    });
    nextPseudocode = result.next;
    $('pseudocode-more').hidden = nextPseudocode == null;
  } catch (error) {
    if (sequence === pseudocodeSequence) {$('pseudocode-count').textContent = 'Could not search local pseudocode. Choose Search to retry.'; notice(error.message, true);}
  } finally {
    if (sequence === pseudocodeSequence) {pseudocodeLoading = false; $('pseudocode-more').disabled = false;}
  }
}
async function openPseudocode(item) {
  const sequence = ++pseudocodeDetailSequence;
  document.querySelectorAll('[data-pseudocode]').forEach(button => {const active = button.dataset.pseudocode === item.id; button.classList.toggle('selected', active); button.setAttribute('aria-pressed', String(active));});
  $('pseudocode-name').textContent = item.name; $('pseudocode-address').textContent = 'VA 0x' + item.address;
  $('pseudocode-viewer').textContent = 'Loading native pseudocode…';
  try {
    const result = await api('pseudocode?id=' + encodeURIComponent(item.id));
    if (sequence !== pseudocodeDetailSequence) return;
    $('pseudocode-viewer').textContent = result.code + (result.truncated ? '\n\n' + result.message : '');
  } catch (error) {if (sequence === pseudocodeDetailSequence) {$('pseudocode-viewer').textContent = 'Could not open this pseudocode file.'; notice(error.message, true);}}
}
$('function-topic').value = 'important';
$('function-search').onclick = () => searchFunctions().catch(error => notice(error.message, true));
$('function-query').onkeydown = event => {if (event.key === 'Enter') $('function-search').click();};
$('function-query').oninput = () => {clearTimeout(functionQueryTimer); functionQueryTimer = setTimeout(() => $('function-search').click(), 300);};
$('function-topic').onchange = () => $('function-search').click();
$('functions-recovered').onchange = () => $('function-search').click();
$('function-scroll').onscroll = () => {const list = $('function-scroll'); if (list.scrollHeight - list.scrollTop - list.clientHeight < 180) searchFunctions(true).catch(error => notice(error.message, true));};

$('browse-game').onclick = () => {if (settingsDirty || sourceDirty) {notice('Save your edits before switching game installations.',true); return;} runAction('browse-game');};

function updateSharingButtons() {
  $('export-package').disabled = busy || !exportSelection.size;
  $('import-package').disabled = busy || !previewPackage;
  $('export-count').textContent = `${exportSelection.size} selected`;
}
function renderSharing() {
  const list = $('export-modules'); list.replaceChildren();
  packsForState().forEach(pack => {list.append(el('h4','export-pack',pack.name)); pack.features.forEach(feature => {
    const row=el('label','export-module'), input=el('input'); input.type='checkbox'; input.checked=exportSelection.has(feature.id); input.dataset.exportModule=feature.id;
    input.onchange=() => {if (input.checked) exportSelection.add(feature.id); else exportSelection.delete(feature.id); updateSharingButtons();}; row.append(input,el('span','',feature.name)); list.append(row);
  });}); updateSharingButtons();
}
async function authoringAction(action, body, callback) {
  if (busy) return; actionInFlight=action; setBusy(true); notice('Working…');
  try {const result=await api(action,body); await refresh(true); if (callback) await callback(result); notice(result.message || 'Done');}
  catch (error) {notice(error.message,true);} finally {actionInFlight=undefined; setBusy(false);}
}
function stageImportedSettings(values) {
  Object.entries(values || {}).forEach(([key,value]) => {
    if (Object.hasOwn(state.settings,key)) changeSetting(key,value);
    else {const entry=[...(state.recipes||[]),...(state.extraSettings||[])].find(e=>e.section+'/'+e.key===key); if (entry) changeEntry(entry,String(value),(state.extraSettings||[]).includes(entry));}
  }); syncControls();
}
$('browse-package').onclick = () => authoringAction('browse-file',{kind:'package'},result => {if (result.path) {$('package-file').value=result.path; previewPackage=undefined; updateSharingButtons();}});
$('export-select-all').onclick = () => {state.features.forEach(f=>exportSelection.add(f.id));renderSharing();};
$('export-select-none').onclick = () => {exportSelection.clear();renderSharing();};
$('export-package').onclick = () => {if (sourceDirty) {notice('Save your C# before exporting its source.',true);return;} authoringAction('export-package',{ids:[...exportSelection],name:$('package-name').value.trim(),values:state.settings},async result => {
  const response=await fetch('/api/download?id='+encodeURIComponent(result.id),{headers:{'X-TK2-Token':token}}); if (!response.ok) throw new Error('Could not download the package.');
  const url=URL.createObjectURL(await response.blob()), link=el('a'); link.href=url; link.download=result.filename; document.body.append(link); link.click(); link.remove(); setTimeout(()=>URL.revokeObjectURL(url),10000);
});};
$('package-file').oninput = () => {previewPackage=undefined;updateSharingButtons();};
$('preview-package').onclick = () => authoringAction('preview-package',{path:$('package-file').value.trim()}, result => {
  previewPackage=result; const panel=$('package-preview'); panel.replaceChildren(); panel.append(el('h4','',`${result.modules.length} modules`));
  result.modules.forEach(module=>panel.append(el('p','',typeof module==='string'?module:module.name || module.id)));
  const files=el('ul'); (result.files || []).forEach(file=>files.append(el('li','',typeof file==='string'?file:file.path || file.name))); panel.append(files);
  if (result.conflicts?.length) panel.append(el('p','',`${result.conflicts.length} existing files differ. Replacing shared source can affect other modules.`));
  else panel.append(el('p','muted','No source conflicts. Imported modules start off.')); $('replace-package-files').checked=false; updateSharingButtons();
});
$('import-package').onclick = () => {if (sourceDirty) {notice('Save your C# before importing source files.',true);return;} if (!previewPackage) return;
  authoringAction('import-package',{path:previewPackage.path,hash:previewPackage.hash,replace:$('replace-package-files').checked}, result => {stageImportedSettings(result.settings); previewPackage=undefined; updateSharingButtons();});
};
