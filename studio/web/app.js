'use strict';
const $ = id => document.getElementById(id);
const token = document.querySelector('meta[name="tk2-session"]').content;
let state, category = 'All', source, sourceDirty = false, settingsDirty = false, busy = false;
let noticeTimer;
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
  if (!error) noticeTimer = setTimeout(() => {$('notice').hidden = true;}, 9000);
}
async function api(path, body) {
  const response = await fetch('/api/' + path, {method: body === undefined ? 'GET' : 'POST', headers: {'X-TK2-Token': token, ...(body === undefined ? {} : {'Content-Type': 'application/json'})}, ...(body === undefined ? {} : {body: JSON.stringify(body)})});
  const data = await response.json();
  if (!response.ok) throw new Error(data.error || 'Request failed');
  return data;
}
function el(tag, cls, text) {const node = document.createElement(tag); if (cls) node.className = cls; if (text !== undefined) node.textContent = text; return node;}
function markSettings() {settingsDirty = true; $('save-settings').disabled = busy; $('settings-state').textContent = 'You have unsaved changes.';}
function switchControl(label, value, disabled, change) {
  const wrapper = el('label', 'switch'); const input = el('input'); input.type = 'checkbox'; input.checked = value; input.disabled = disabled; input.setAttribute('aria-label', label);
  input.onchange = () => change(input.checked); wrapper.append(input, el('span')); return wrapper;
}
function renderFeatures() {
  const list = $('feature-list'); list.replaceChildren();
  const search = $('mod-search').value.toLowerCase().trim();
  let count = 0;
  state.features.forEach((feature, i) => {
    const group = feature.gameplay ? 'Gameplay' : feature.category === 'Audio' ? 'Audio' : 'Visual';
    if ((category !== 'All' && category !== group) || !(`${feature.name} ${feature.description}`.toLowerCase().includes(search))) return;
    count++;
    const row = el('article', 'feature'); row.dataset.feature = feature.id;
    const info = el('div', 'feature-info'); const heading = el('h3', '', feature.name); const tag = el('span', 'tag', feature.origin);
    info.append(heading, tag, el('p', '', feature.description));
    if (feature.gameplay) info.append(el('span', 'locked-note', 'LOCKED · Awaiting in-game protection checks'));
    const settings = el('div', 'feature-settings');
    feature.settings.forEach(([key, label, kind, fallback, low, high]) => {
      const control = el('label', 'setting'); control.append(el('span', '', label)); const input = el('input');
      input.type = kind === 'text' ? 'text' : 'number'; input.value = state.settings[feature.id + '/' + key] ?? fallback;
      if (kind !== 'text') {input.min = low; input.max = high; input.step = kind === 'int' ? '1' : key === 'FieldOfView' || key === 'ShadowDistance' ? '1' : '.05';}
      input.dataset.setting = feature.id + '/' + key; input.disabled = busy; input.oninput = () => {state.settings[input.dataset.setting] = kind === 'text' ? input.value : input.value === '' ? '' : Number(input.value); markSettings();};
      control.append(input); settings.append(control);
    });
    const toggle = switchControl('Enable ' + feature.name, feature.gameplay ? false : Boolean(state.settings[feature.id + '/Enabled']), busy || feature.gameplay, enabled => {state.settings[feature.id + '/Enabled'] = enabled; markSettings();});
    row.append(el('span', 'feature-number', String(i + 1).padStart(2, '0')), info, settings, toggle); list.append(row);
  });
  if (!count) list.append(el('p', 'muted', 'No mods match. Try another search or category.'));
}
function renderRecipes() {
  const list = $('recipe-controls'); list.replaceChildren();
  if (!state.recipes.length) return;
  list.append(el('h2', 'backup-heading', 'Your recipes'));
  state.recipes.forEach(entry => {
    const row = el('label', 'backup-row'); row.append(el('span', '', entry.section.replace('Recipe.', '') + ' / ' + entry.key));
    const input = el(entry.choices.length || entry.type === 'Boolean' ? 'select' : 'input');
    if (input.tagName === 'SELECT') {
      (entry.type === 'Boolean' ? ['false', 'true'] : entry.choices).forEach(value => {const option = el('option', '', value); option.value = value; input.append(option);});
    } else {
      input.type = ['Single', 'Int32', 'Double'].includes(entry.type) ? 'number' : 'text';
      if (input.type === 'number') {
        input.step = entry.type === 'Int32' ? '1' : 'any';
        const bounds = /^From (.+) to (.+)$/.exec(entry.range);
        if (bounds) {input.min = bounds[1]; input.max = bounds[2];}
      }
    }
    input.title = entry.description;
    input.value = entry.value; input.disabled = busy; input.dataset.recipe = entry.section + '/' + entry.key;
    input.onchange = () => {entry.value = input.value; markSettings();}; row.append(input); list.append(row);
  });
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
  $('game-path').textContent = state.game;
  $('plugin-status').textContent = `${state.installed ? 'Garage pack installed' : 'Garage pack not installed'} · ${state.pluginCount} active plugin DLL${state.pluginCount === 1 ? '' : 's'} found`;
  $('install-state').textContent = state.installed ? (state.packCurrent ? 'Pack installed · all modules in one DLL' : 'Installed pack differs from the latest build') : 'One plugin · features start disabled';
  $('install').firstChild.textContent = state.installed ? 'Update mod pack ' : 'Install mod pack ';
  const list = $('backups'); list.replaceChildren();
  state.backups.forEach(backup => {
    const row = el('div', 'backup-row'); const info = el('div'); info.append(el('p', '', backup.file), el('small', '', `Backup ${backup.id} · ${backup.existed ? 'restore previous contents' : 'remove newly installed file'}`));
    const button = el('button', 'secondary', 'Restore'); button.disabled = busy; button.onclick = () => runAction('restore', {id: backup.id}); row.append(info, button); list.append(row);
  });
  if (!state.backups.length) list.append(el('p', 'muted', 'Your first install or settings save creates a backup here.'));
  $('logs').textContent = state.logs.join('\n') || 'No build run in this session.';
}
function render() {renderFeatures(); renderRecipes(); renderFiles(); renderInstallation();}
async function refresh(preserveSettings = false) {
  const next = await api('state');
  if (preserveSettings && state && settingsDirty) {next.settings = state.settings; next.configHash = state.configHash; next.recipes = state.recipes;}
  state = next; render();
}
async function saveSource() {
  if (!source || !sourceDirty) return;
  const result = await api('save-source', {file: source.file, hash: source.hash, content: $('code').value});
  source.hash = result.hash; source.content = $('code').value; sourceDirty = false; $('save-source').disabled = true;
}
async function loadSource(file, discard = false) {
  try {
    if (sourceDirty && !discard) {notice('Save the current C# before switching files, or use Reload to discard your edits.', true); return;}
    source = await api('source?file=' + encodeURIComponent(file)); $('code').value = source.content; $('source-name').textContent = file;
    sourceDirty = false; $('save-source').disabled = true; $('reload-source').disabled = false; renderFiles();
  } catch (error) {notice(error.message, true);}
}
function setBusy(value) {
  busy = value;
  ['install', 'workshop-install', 'build', 'create-recipe', 'diagnose'].forEach(id => {$(id).disabled = value;});
  $('save-settings').disabled = value || !settingsDirty;
  $('save-source').disabled = value || !sourceDirty; $('code').readOnly = value;
  $('reload-source').disabled = value || !source;
  render();
}
async function runAction(action, body = {}) {
  if (busy) return;
  setBusy(true);
  notice(action === 'install' || action === 'build' ? 'Compiling your pack. This can take a moment…' : 'Working…');
  try {
    if (action === 'build' || action === 'install') await saveSource();
    const result = await api(action, body);
    if (action === 'settings') {settingsDirty = false; $('settings-state').textContent = 'All changes saved.';}
    if (action === 'diagnose') {state.logs.push(JSON.stringify(result, null, 2)); $('logs').textContent = state.logs.join('\n'); notice('Installation checked. Details are in diagnostics.');}
    else {await refresh(action !== 'settings'); notice(result.message || 'Done');}
    if (action === 'create-recipe') await loadSource(result.file);
  } catch (error) {notice(error.message, true); try {const next = await api('state'); state.logs = next.logs; $('logs').textContent = state.logs.join('\n');} catch {} }
  finally {setBusy(false);}
}
function showView(name) {
  document.querySelectorAll('.view').forEach(view => {view.hidden = view.id !== name;});
  document.querySelectorAll('.nav').forEach(button => button.classList.toggle('active', button.dataset.view === name));
  $('breadcrumb').textContent = 'GARAGE / ' + ({mods: 'MY MODS', workshop: 'THE WORKSHOP', installation: 'PIT STOP'}[name]);
  history.replaceState(null, '', '#' + name);
  if (name === 'workshop' && state && !source) loadSource('src/Reconstructed/KartLogic.cs');
}
document.querySelectorAll('.nav').forEach(button => button.onclick = () => showView(button.dataset.view));
document.querySelector('.brand').onclick = event => {event.preventDefault(); showView('mods');};
document.querySelectorAll('[data-category]').forEach(button => button.onclick = () => {category = button.dataset.category; document.querySelectorAll('[data-category]').forEach(b => b.classList.toggle('selected', b === button)); renderFeatures();});
$('mod-search').oninput = renderFeatures;
$('install').onclick = $('workshop-install').onclick = () => runAction('install');
$('build').onclick = () => runAction('build');
$('create-recipe').onclick = () => {if (sourceDirty) {notice('Save the current C# before creating another recipe.', true); return;} runAction('create-recipe', {name: $('recipe-name').value.trim()});};
$('diagnose').onclick = () => runAction('diagnose');
$('save-settings').onclick = () => {
  if (![...document.querySelectorAll('[data-setting]')].every(input => input.checkValidity())) {notice('Check the setting ranges before saving.', true); return;}
  runAction('settings', {values: state.settings, hash: state.configHash, recipes: Object.fromEntries(state.recipes.map(e => [e.section + '/' + e.key, e.value]))});
};
$('code').oninput = () => {sourceDirty = Boolean(source); $('save-source').disabled = !sourceDirty;};
$('save-source').onclick = async () => {try {await saveSource(); notice('C# saved. Build the pack to apply the code.');} catch (error) {notice(error.message, true);}};
$('reload-source').onclick = () => {if (sourceDirty && !confirm('Discard unsaved editor changes and reload this file?')) return; loadSource(source.file, true);};
$('code').onkeydown = event => {
  if (event.key === 'Tab') {event.preventDefault(); const node = event.target; node.setRangeText('    ', node.selectionStart, node.selectionEnd, 'end'); node.dispatchEvent(new Event('input'));}
};
document.addEventListener('keydown', event => {if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {event.preventDefault(); if (sourceDirty && !busy) $('save-source').click(); else if (settingsDirty && !busy) $('save-settings').click();}});
window.addEventListener('beforeunload', event => {if (sourceDirty || settingsDirty || busy) {event.preventDefault(); event.returnValue = '';}});
$('shutdown').onclick = async () => {if (sourceDirty || settingsDirty || busy) {notice('Save your changes and let the current action finish before closing.', true); return;} try {const result = await api('shutdown', {}); notice(result.message);} catch (error) {notice(error.message, true);}};
$('native-search').onclick = async () => {try {const result = await api('native?q=' + encodeURIComponent($('native-query').value)); $('native-results').textContent = JSON.stringify(result, null, 2);} catch (error) {notice(error.message, true);}};
showView(['mods', 'workshop', 'installation'].includes(location.hash.slice(1)) ? location.hash.slice(1) : 'mods');
refresh().then(() => {if (location.hash === '#workshop' && !source) loadSource('src/Reconstructed/KartLogic.cs');}).catch(error => {notice('Garage could not load: ' + error.message, true);});
