'use strict';
let nextFunctions = null, functionSequence = 0;
const $ = id => document.getElementById(id);
const token = document.querySelector('meta[name="tk2-session"]').content;
let state, category = 'All', source, sourceDirty = false, settingsDirty = false, busy = false, actionInFlight;
let noticeTimer, revision = 0, refreshSequence = 0, conflictKeys = [];
const settingEdits = new Map(), recipeEdits = new Map(), extraEdits = new Map(), expandedModules = new Set();
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
  $('settings-state').textContent = count ? `${count} unsaved setting${count === 1 ? '' : 's'}.` : 'All changes saved.';
}
function recordEdit(edits, key, before, value) {
  const previous = edits.get(key), base = previous ? previous.base : before;
  // A reverted key stays dirty while an older value is being saved.
  if (Object.is(base, value) && actionInFlight !== 'settings') edits.delete(key);
  else edits.set(key, {base, value, revision: ++revision});
  updateSaveState();
}
function changeSetting(key, value) {recordEdit(settingEdits, key, state.settings[key], value); state.settings[key] = value; syncModuleState(key.split('/')[0]); syncDependencies();}
function changeEntry(entry, value, extra = false) {recordEdit(extra ? extraEdits : recipeEdits, entry.section + '/' + entry.key, entry.value, value); entry.value = value;}
function switchControl(label, value, disabled, change) {
  const wrapper = el('label', 'switch'); const input = el('input'); input.type = 'checkbox'; input.checked = value; input.disabled = disabled; input.setAttribute('aria-label', label);
  input.onchange = () => change(input.checked); wrapper.append(input, el('span')); return wrapper;
}
function featureGroup(feature) {return feature.gameplay ? 'Gameplay' : feature.category === 'Audio' ? 'Audio' : 'Visual';}
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
  if (choices?.length) choices.forEach(choice => {const option = el('option', '', String(choice)); option.value = choice; input.append(option);});
  else {
    input.type = kind === 'text' ? 'text' : kind === 'bool' ? 'checkbox' : 'number';
    if (!['text', 'bool'].includes(kind)) {if (low !== null && low !== undefined) input.min = low; if (high !== null && high !== undefined) input.max = high; input.step = kind === 'int' ? '1' : 'any';}
  }
  if (kind === 'bool' && !choices?.length) input.checked = Boolean(value); else input.value = value;
  input.id = 'setting-' + encodeURIComponent(key); copy.setAttribute('for', input.id);
  input.dataset[dataset] = key; input.disabled = !state.game || busy && actionInFlight !== 'settings'; input.setAttribute('aria-label', label);
  const changed = () => onChange(kind === 'bool' && !choices?.length ? input.checked : ['text', 'select'].includes(kind) || choices?.length ? input.value : input.value === '' ? '' : Number(input.value));
  input.oninput = changed; input.onchange = changed; control.append(input); return control;
}
function syncModuleState(id) {document.querySelectorAll('[data-module-state]').forEach(node => {if (node.dataset.moduleState === id) node.textContent = state.settings[id + '/Enabled'] ? 'On' : 'Off';});}
function syncDependencies() {
  document.querySelectorAll('[data-setting]').forEach(input => {
    const feature = state.features.find(item => item.id === input.dataset.setting.split('/')[0]);
    input.disabled = !state.game || busy && actionInFlight !== 'settings' || feature?.available === false || Boolean(input.dataset.requires && !state.settings[input.dataset.requires]);
    if (input.dataset.requires) input.setAttribute('aria-description', input.disabled ? 'Turn on this parameter’s override switch to edit its custom value.' : 'Custom override value.');
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
      if (requires) {
        const requireKey = requires.includes('/') ? requires : feature.id + '/' + requires;
        const input = control.children[1]; input.dataset.requires = requireKey; input.disabled = busy && actionInFlight !== 'settings' || !state.settings[requireKey];
        const toggle = switchControl('Override ' + label, Boolean(state.settings[requireKey]), busy && actionInFlight !== 'settings', value => changeSetting(requireKey, value));
        toggle.children[0].dataset.setting = requireKey;
        const paired = el('div', 'parameter-controls'); paired.append(toggle, input); control.replaceChildren(control.children[0], paired);
      }
      controls.append(control);
    });
    if (name) {
      const details = el('details', 'parameter-group'), identity = feature.id + ':' + name;
      details.open = expandedModules.has(identity) || Boolean(search && settings.some(setting => [name, setting[0], setting[1], setting[6], setting[8], setting[8] ? 'Override ' + setting[1] : ''].join(' ').toLowerCase().includes(search)));
      details.ontoggle = () => {if (details.open) expandedModules.add(identity); else expandedModules.delete(identity);};
      details.append(el('summary', '', name), controls); container.append(details);
    } else container.append(controls);
  });
  if (!feature.settings.length && feature.available !== false) container.append(el('p', 'module-note', 'This module has no additional settings.'));
  return container;
}
function extraPackId(entry) {
  const id = entry.pack || state.extraPacks?.[entry.section] || (/^(MK\.|Alternate|AutoBoost|BoostTrainer|Bobby|Boring|CNK|CustomPhysics|Dash|FastRespawn|Mirror|Reverse|Proximity|SaveState|Supra|Teleport)/.test(entry.section) ? 'mks' : 'community');
  return id === 'mks' && !state.packs?.some(pack => pack.id === 'mks') ? 'community' : id;
}
function renderFeatures() {
  const list = $('feature-list'); list.replaceChildren();
  const search = $('mod-search').value.toLowerCase().trim(); let count = 0;
  packsForState().forEach(pack => {
    const features = pack.features.filter(feature => {
      const matches = category === 'All' || category === 'Enabled' && state.settings[feature.id + '/Enabled'] || category === featureGroup(feature);
      return matches && [pack.name, feature.name, feature.description, ...feature.settings.map(setting => setting[1])].join(' ').toLowerCase().includes(search);
    });
    const extras = (state.extraSettings || []).filter(entry => extraPackId(entry) === pack.id);
    const extraGroups = extraGroupsForFilter(extras, search, pack.name);
    if (!features.length && !extraGroups.size) return;
    count += features.length + extraGroups.size;
    const section = el('section', 'mod-pack'); section.dataset.pack = pack.id;
    const header = el('header', 'pack-heading'), copy = el('div'); copy.append(el('h2', '', pack.name));
    if (pack.description) copy.append(el('p', '', pack.description));
    const size = features.length + extraGroups.size;
    const packActions = el('div', 'pack-actions'), reset = el('button', 'quiet', 'Reset pack'); reset.onclick = () => resetFeatures(pack.features); packActions.append(el('small', '', `${size} modules`), reset); header.append(copy, packActions); section.append(header);
    features.forEach(feature => {
      const unavailable = feature.available === false;
      const row = el('article', 'module'); row.dataset.feature = feature.id;
      const heading = el('div', 'module-heading'), expand = el('button', 'module-expand'), info = el('span', 'module-info');
      info.append(el('span', 'module-name', feature.name));
      if (feature.description) info.append(el('span', 'module-description', feature.description));
      const enabled = Boolean(state.settings[feature.id + '/Enabled']), status = el('span', 'module-state', enabled ? 'On' : 'Off'); status.dataset.moduleState = feature.id;
      const panel = el('div', 'module-body'); panel.id = 'module-' + feature.id; panel.hidden = !(expandedModules.has(feature.id) || Boolean(search));
      expand.setAttribute('aria-expanded', String(!panel.hidden)); expand.setAttribute('aria-controls', panel.id);
      expand.append(info, status, el('span', 'expand-arrow', '⌄'));
      expand.onclick = () => {panel.hidden = !panel.hidden; expand.setAttribute('aria-expanded', String(!panel.hidden)); if (panel.hidden) expandedModules.delete(feature.id); else expandedModules.add(feature.id);};
      const toggle = switchControl('Enable ' + feature.name, enabled, unavailable || busy && actionInFlight !== 'settings', value => changeSetting(feature.id + '/Enabled', value));
      toggle.children[0].dataset.setting = feature.id + '/Enabled'; heading.append(expand, toggle); row.append(heading);
      if (unavailable) panel.append(el('p', 'module-note', feature.reason || 'This module needs a compatibility update before it can be enabled.'));
      if (feature.status && feature.status !== 'verified') panel.append(el('p', 'module-note', feature.status));
      const moduleTools = el('div', 'module-tools'), reset = el('button', 'quiet', 'Reset module to defaults'); reset.onclick = () => resetFeatures([feature]); moduleTools.append(reset, el('small', '', 'Defaults switch this module off.')); panel.append(moduleTools);
      panel.append(featureSettings(feature, search)); row.append(panel); section.append(row);
    });
    appendEntryGroups(section, extraGroups, true); list.append(section);
  });
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
    expand.setAttribute('aria-expanded', String(!panel.hidden)); expand.setAttribute('aria-controls', panel.id);
    expand.append(info, el('span', 'expand-arrow', '⌄')); expand.onclick = () => {panel.hidden = !panel.hidden; expand.setAttribute('aria-expanded', String(!panel.hidden)); if (panel.hidden) expandedModules.delete(name); else expandedModules.add(name);};
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
    const reset = el('button', 'quiet', 'Reset module to defaults'); reset.onclick = () => {settings.forEach(entry => {if (entry.default !== undefined && entry.hasDefault !== false) changeEntry(entry, entry.default, extra);}); syncControls(); notice('Defaults restored. Save changes to apply.');}; panel.append(reset, controls); row.append(panel); container.append(row);
  });
}
function renderRecipes() {
  const list = $('recipe-controls'); list.replaceChildren();
  const entries = state.recipes || []; if (!entries.length) return;
  const search = $('mod-search').value.toLowerCase().trim(), groups = extraGroupsForFilter(entries, search, 'Your recipes');
  if (!groups.size) return;
  const section = el('section', 'mod-pack'), header = el('header', 'pack-heading'); header.append(el('h2', '', 'Your recipes')); section.append(header);
  appendEntryGroups(section, groups); list.append(section);
}
function renderFiles() {
  $('reconstructed-files').replaceChildren(); $('pack-files').replaceChildren();
  state.files.forEach(file => {
    const button = el('button', 'file-button', file.split('/').at(-1)); button.dataset.file = file; button.title = file; button.disabled = busy;
    button.classList.toggle('selected', source?.file === file); button.onclick = () => loadSource(file);
    $(file.startsWith('src/Reconstructed/') ? 'reconstructed-files' : 'pack-files').append(button);
  });
}
function renderInstallation() {
  const ready = state.setup || {};
  $('game-path').textContent = state.game || 'No game selected';
  if (document.activeElement !== $('game-directory')) $('game-directory').value = state.game || '';
  $('plugin-status').textContent = `${state.installed ? 'Plugin installed' : 'Plugin not installed'} · ${state.pluginCount} active plugin DLL${state.pluginCount === 1 ? '' : 's'}`;
  $('setup-message').textContent = ready.message || 'Check the installation.';
  $('setup-checks').replaceChildren();
  (ready.checks || []).forEach(check => {const row = el('li', check.ok ? 'check-ok' : 'check-pending', `${check.ok ? '✓' : '○'} ${check.name}`); $('setup-checks').append(row);});
  $('prepare-loader').disabled = busy || !state.game || !ready.loaderSource;
  $('prepare-loader').textContent = ready.stage === 'install-loader' ? 'Prepare BepInEx' : 'Repair BepInEx files';
  $('loader-help').textContent = ready.loaderSource ? 'Loader distribution included. Existing mods and settings are preserved.' : 'Loader bundle not available. Add the Unity IL2CPP x64 distribution to vendor/BepInEx.';
  $('installation-install').disabled = busy || !ready.ready || state.packCurrent;
  $('installation-install').textContent = state.packCurrent ? 'Plugin up to date' : state.installed ? 'Update plugin (restart required)' : 'Install plugin';
  $('open-game-folder').disabled = busy || !state.game;
  $('runtime-errors').textContent = [...(ready.runtimeErrors || []), ...(ready.runtimeWarnings || [])].join('\n') || 'No errors or warnings found in the available BepInEx log.';
  $('install-state').textContent = !ready.ready ? 'Setup needed' : state.installed ? (state.packCurrent ? 'Plugin installed' : 'Plugin update available') : 'Ready to install';
  const list = $('backups'); list.replaceChildren();
  state.backups.forEach(backup => {
    const row = el('div', 'backup-row'), info = el('div'); info.append(el('p', '', backup.file), el('small', '', `${backup.id} · ${backup.existed ? 'restore previous contents' : 'remove newly installed file'}`));
    const button = el('button', 'secondary', 'Restore'); button.disabled = busy; button.onclick = () => runAction('restore', {id: backup.id}); row.append(info, button); list.append(row);
  });
  if (!state.backups.length) list.append(el('p', 'empty-state', 'Changes create backups here.'));
  $('logs').textContent = state.buildLog || state.logs.join('\n') || 'No build run in this session. Player installation uses the bundled plugin.';
}
function resetFeatures(features) {
  features.forEach(feature => {changeSetting(feature.id + '/Enabled', false); feature.settings.forEach(setting => changeSetting(feature.id + '/' + setting[0], setting[3]));});
  syncControls(); notice('Defaults restored. Save changes to apply.');
}
function render() {renderFeatures(); renderRecipes(); renderFiles(); renderInstallation(); syncDependencies(); updateSaveState();}
function mergeState(next) {
  settingEdits.forEach((edit, key) => {next.settings[key] = edit.value;});
  [['recipes', recipeEdits], ['extraSettings', extraEdits]].forEach(([field, edits]) => {(next[field] || []).forEach(entry => {const edit = edits.get(entry.section + '/' + entry.key); if (edit) entry.value = edit.value;});});
  state = next;
}
function syncControls() {
  document.querySelectorAll('[data-setting]').forEach(input => {const value = state.settings[input.dataset.setting]; if (input === document.activeElement || value === undefined) return; if (input.type === 'checkbox') input.checked = Boolean(value); else input.value = value;});
  ['recipe', 'extra'].forEach(kind => {document.querySelectorAll('[data-' + kind + ']').forEach(input => {const entry = (state[kind === 'extra' ? 'extraSettings' : 'recipes'] || []).find(entry => entry.section + '/' + entry.key === input.dataset[kind]); if (entry && input !== document.activeElement) {if (input.type === 'checkbox') input.checked = entry.value.toLowerCase() === 'true'; else input.value = entry.value;}});});
  state.features.forEach(feature => syncModuleState(feature.id)); syncDependencies(); updateSaveState();
}
async function refresh(preserveSettings = true, quiet = false) {
  const sequence = ++refreshSequence, next = await api('state'); if (sequence !== refreshSequence) return;
  const structure = value => [value.features, value.packs, value.files, ...( ['recipes', 'extraSettings'].map(field => (value[field] || []).map(entry => entry.section + '/' + entry.key)))];
  const changed = !state || JSON.stringify(structure(next)) !== JSON.stringify(structure(state));
  if (!preserveSettings) {settingEdits.clear(); recipeEdits.clear(); extraEdits.clear();}
  mergeState(next);
  if (!quiet || changed) render(); else {syncControls(); renderInstallation();}
}
function settingsSnapshot() {
  const body = {values: {}, baseValues: {}, recipes: {}, baseRecipes: {}, extraSettings: {}, baseExtraSettings: {}, hash: state.configHash}, revisions = {};
  [['values', 'baseValues', settingEdits], ['recipes', 'baseRecipes', recipeEdits], ['extraSettings', 'baseExtraSettings', extraEdits]].forEach(([values, bases, edits]) => {
    revisions[values] = new Map(); edits.forEach((edit, key) => {body[values][key] = edit.value; body[bases][key] = edit.base; revisions[values].set(key, edit.revision);});
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
}
async function loadSource(file, discard = false) {
  try {if (sourceDirty && !discard) {notice('Save the current C# before switching files, or use Reload to discard your edits.', true); return;}
    source = await api('source?file=' + encodeURIComponent(file)); $('code').value = source.content; $('source-name').textContent = file;
    sourceDirty = false; $('code').readOnly = false; $('save-source').disabled = true; $('reload-source').disabled = false; renderFiles();
  } catch (error) {notice(error.message, true);}
}
function setBusy(value) {
  busy = value; ['workshop-install', 'build', 'create-recipe', 'diagnose', 'prepare-loader', 'installation-install', 'scan-games', 'select-game', 'browse-game'].forEach(id => {$(id).disabled = value;});
  $('save-source').disabled = value || !sourceDirty; $('code').readOnly = value || !source; $('reload-source').disabled = value || !source; updateSaveState();
  // Keep controls mounted and editable during a settings save.
  document.querySelectorAll('[data-setting], [data-recipe], [data-extra]').forEach(input => {const feature = state?.features.find(item => item.id === input.dataset.setting?.split('/')[0]); input.disabled = value && actionInFlight !== 'settings' || feature?.available === false || !state?.game;});
  syncDependencies(); if (state) renderInstallation();
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
    }
    else if (action === 'diagnose') {await refresh(true, true); notice(result.message);}
    else {await refresh(true); notice(result.message || 'Done');}
    if (action === 'create-recipe') await loadSource(result.file);
  } catch (error) {if (action === 'settings') settingsError(error); else notice(error.message, true);}
  finally {actionInFlight = undefined; setBusy(false); if (action !== 'settings') await refresh(true, true).catch(() => {});}
}
function showView(name) {
  document.querySelectorAll('.view').forEach(view => {view.hidden = view.id !== name;});
  document.querySelectorAll('.nav').forEach(button => {button.classList.toggle('active', button.dataset.view === name); button.setAttribute('aria-current', button.dataset.view === name ? 'page' : 'false');});
  $('breadcrumb').textContent = 'TOOLKIT / ' + ({mods: 'MOD PACKS', workshop: 'WORKSHOP', installation: 'INSTALLATION'}[name]); history.replaceState(null, '', '#' + name);
  if (name === 'workshop' && state && !source) loadSource('src/Reconstructed/KartLogic.cs');
  if (name === 'workshop') searchFunctions().catch(error => notice(error.message, true));
}
document.querySelectorAll('.nav').forEach(button => button.onclick = () => showView(button.dataset.view));
document.querySelector('.brand').onclick = event => {event.preventDefault(); showView('mods');};
document.querySelectorAll('[data-category]').forEach(button => button.onclick = () => {category = button.dataset.category; document.querySelectorAll('[data-category]').forEach(b => b.classList.toggle('selected', b === button)); renderFeatures(); renderRecipes();});
$('mod-search').oninput = () => {renderFeatures(); renderRecipes();};
$('workshop-install').onclick = () => runAction('build-install');
$('installation-install').onclick = () => runAction('install'); $('build').onclick = () => runAction('build');
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
$('code').oninput = () => {sourceDirty = Boolean(source); $('save-source').disabled = !sourceDirty;};
$('save-source').onclick = async () => {try {await saveSource(); notice('C# saved. Build the plugin to apply your code.');} catch (error) {notice(error.message, true);}};
$('reload-source').onclick = () => {if (sourceDirty && !confirm('Discard unsaved editor changes and reload this file?')) return; loadSource(source.file, true);};
$('code').onkeydown = event => {if (event.key === 'Tab') {event.preventDefault(); const node = event.target; node.setRangeText('    ', node.selectionStart, node.selectionEnd, 'end'); node.dispatchEvent(new Event('input'));}};
document.addEventListener('keydown', event => {if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {event.preventDefault(); if (location.hash === '#workshop' && sourceDirty && !busy) $('save-source').click(); else if (settingsDirty && !busy) $('save-settings').click();}});
window.addEventListener('beforeunload', event => {if (sourceDirty || settingsDirty || busy) {event.preventDefault(); event.returnValue = '';}});
$('native-search').onclick = async () => {try {$('native-results').textContent = JSON.stringify(await api('native?q=' + encodeURIComponent($('native-query').value)), null, 2);} catch (error) {notice(error.message, true);}};
showView(['mods', 'workshop', 'installation'].includes(location.hash.slice(1)) ? location.hash.slice(1) : 'mods');
refresh().then(() => {if (!state.setup?.ready && !state.game) showView('installation'); if (location.hash === '#workshop' && !source) loadSource('src/Reconstructed/KartLogic.cs');}).catch(error => {notice('Toolkit could not load: ' + error.message, true);});
setInterval(() => {if (state && !busy && !document.hidden) refresh(true, true).catch(() => {});}, 5000);

async function searchFunctions(more = false) {
  const sequence = ++functionSequence, offset = more ? nextFunctions || 0 : 0;
  const result = await api('functions?q=' + encodeURIComponent($('function-query').value) + '&offset=' + offset + '&recovered=' + $('functions-recovered').checked);
  if (sequence !== functionSequence) return;
  if (!more) $('function-list').replaceChildren();
  $('function-count').textContent = result.message || `${result.total} matching functions · ${result.indexed} indexed · ${result.reconstructed} with reconstructed C#`;
  (result.methods || []).forEach(method => {
    const button = el('button', 'function-row'); button.append(el('strong', '', method.type), el('code', '', method.signature), el('small', '', method.source ? 'Reconstructed C#' : 'Declaration'));
    button.onclick = async () => {try {
      const detail = await api('function?id=' + encodeURIComponent(method.id));
      $('function-status').textContent = detail.status; $('function-detail').textContent = detail.declaration;
      $('open-function-source').hidden = !detail.source; $('open-function-source').onclick = () => loadSource(detail.source);
    } catch (error) {notice(error.message, true);}};
    $('function-list').append(button);
  });
  nextFunctions = result.next; $('functions-more').hidden = nextFunctions === null || nextFunctions === undefined;
}
$('function-search').onclick = () => searchFunctions().catch(error => notice(error.message, true));
$('function-query').onkeydown = event => {if (event.key === 'Enter') $('function-search').click();};
$('functions-recovered').onchange = () => $('function-search').click();
$('functions-more').onclick = () => searchFunctions(true).catch(error => notice(error.message, true));

$('browse-game').onclick = () => {if (settingsDirty || sourceDirty) {notice('Save your edits before switching game installations.',true); return;} runAction('browse-game');};
