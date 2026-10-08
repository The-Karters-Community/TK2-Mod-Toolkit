// Deterministic UI regression tests. No browser, game, or network access.
const fs = require('node:fs'), vm = require('node:vm'), path = require('node:path');
let assertions = 0;
const assert = new Proxy(require('node:assert/strict'), {get(target, key) {const value = Reflect.get(target, key); return typeof value === 'function' ? (...args) => {assertions++; return value(...args);} : value;}});
const root = path.resolve(__dirname, '..');
class Node {
  constructor(tag = 'div') {this.tagName = tag.toUpperCase(); this.children = []; this.dataset = {}; this.value = ''; this.disabled = false; this.hidden = false; this.classes = new Set(); this.attrs = {}; this.classList = {toggle: (name, force) => {if (force) this.classes.add(name); else this.classes.delete(name);}};}
  set className(value) {this.classes = new Set(value.split(' '));}
  set textContent(value) {this.text = value; this.children = [];}
  get textContent() {return this.text;}
  append(...nodes) {this.children.push(...nodes);}
  replaceChildren(...nodes) {this.children = nodes;}
  setAttribute(name, value) {this.attrs[name] = value;}
  getAttribute(name) {return this.attrs[name];}
  checkValidity() {return this.valid !== false;}
  click() {return this.onclick?.({preventDefault(){}});}
  setRangeText(text, start, end) {this.value = this.value.slice(0, start) + text + this.value.slice(end);}
  dispatchEvent() {this.oninput?.();}
}
const html = fs.readFileSync(path.join(root, 'studio/web/index.html'), 'utf8');
const ids = Object.fromEntries([...html.matchAll(/id="([^"]+)"/g)].map(match => [match[1], new Node()]));
const views = ['mods', 'workshop', 'installation'].map(id => {ids[id].id = id; return ids[id];});
const nav = views.map(view => {const node = new Node('button'); node.dataset.view = view.id; return node;});
const filters = ['All', 'Enabled', 'Visual', 'Audio', 'Gameplay'].map(category => {const node = new Node('button'); node.dataset.category = category; return node;});
const brand = new Node('a'), meta = {content: 'test-token'};
function descendants(node) {return [node, ...node.children.flatMap(descendants)];}
function query(selector) {
  if (selector === '.view') return views;
  if (selector === '.nav') return nav;
  if (selector === '[data-category]') return filters;
  const names = selector.split(',').map(part => /^\[data-([^\]]+)\]$/.exec(part.trim())?.[1]?.replace(/-([a-z])/g, (_match, letter) => letter.toUpperCase())).filter(Boolean);
  return Object.values(ids).flatMap(descendants).filter(node => names.some(name => node.dataset[name] !== undefined));
}
const document = {documentElement: new Node(), getElementById: id => ids[id], createElement: tag => new Node(tag), querySelector: selector => selector.startsWith('meta') ? meta : brand, querySelectorAll: query, addEventListener(){}};
const fixture = {installed: false, packCurrent: false, pluginCount: 0, game: 'test game', logs: [], backups: [],
  recipes: [{section: 'Recipe.FrameLimiter', key: 'Enabled', type: 'Boolean', value: 'false', choices: [], range: '', description: 'Limit FPS'}],
  extraSettings: [{section: 'MK.BoostTrainer', key: 'Enabled', type: 'Boolean', value: 'false', choices: [], range: '', description: 'Trainer'}],
  extraPacks: {'MK.BoostTrainer': 'mks'},
  settings: {'Audio/Enabled': false, 'Audio/MasterVolume': 1, 'Physics/Enabled': false, 'Camera/Enabled': false, 'Camera/FieldOfView': 65}, configHash: 'first-hash',
  packs: [{id:'garage', name:'Toolkit Essentials', features:['Audio','Camera']}, {id:'mks', name:"Community Mods", features:['Physics']}, {id:'community', name:'Community Pack', features:[]}],
  files: ['src/Reconstructed/KartLogic.cs'], features: [
    {id:'Audio', name:'Audio mixer', category:'Audio', description:'Volume adjustment', origin:'New', settings:[['MasterVolume','Volume','float',1,0,1]]},
    {id:'Physics', name:'Fast fall', category:'Driving', description:'Physics description', origin:'Adapted', gameplay:true, settings:[]},
    {id:'Camera', name:'Camera', category:'Camera', description:'Keep your kart in view', origin:'New', settings:[['FieldOfView','Field of view','float',65,35,110]]}
  ]};
const calls = []; let sourceConflict = false, settingsFailure, stateFailure = false, deferSave, pendingSave, hashCounter = 0;
function saveResponse(body) {
  Object.assign(fixture.settings, body.values);
  [['recipes','recipes'],['extraSettings','extraSettings']].forEach(([field, payload]) => fixture[field].forEach(entry => {const key = entry.section + '/' + entry.key; if (Object.hasOwn(body[payload], key)) entry.value = body[payload][key];}));
  fixture.configHash = 'saved-' + ++hashCounter;
  return {message:'Settings saved.', settings: structuredClone(fixture.settings), recipes:structuredClone(fixture.recipes), extraSettings:structuredClone(fixture.extraSettings), configHash:fixture.configHash};
}
const context = {document, console, location:{hash:'#mods'}, history:{replaceState(_a,_b,hash){context.location.hash = hash;}},
  localStorage:{data:{}, getItem(key){return this.data[key] || null;}, setItem(key,value){this.data[key] = value;}},
  matchMedia:()=>({matches:false,addEventListener(){}}), setTimeout:()=>1, clearTimeout(){}, setInterval:()=>1,
  window:{addEventListener(){}}, confirm:()=>true, Event:class{},
  fetch: async (url, options) => {
    calls.push({url, options}); let data, ok = true;
    if (url === '/api/state') {ok = !stateFailure; data = stateFailure ? {error:'Temporary state refresh failure.'} : structuredClone(fixture);}
    else if (url.startsWith('/api/source?')) data = {file:fixture.files[0],content:'// source',hash:'source-hash'};
    else if (url === '/api/save-source') {ok = !sourceConflict; data = sourceConflict ? {error:'Source changed outside the app'} : {hash:'saved-source-hash'};}
    else if (url === '/api/settings') {
      if (settingsFailure) {ok = false; data = settingsFailure;}
      else if (deferSave) {const body = JSON.parse(options.body); data = await new Promise(resolve => {pendingSave = () => {pendingSave = undefined; resolve(saveResponse(body));};});}
      else data = saveResponse(JSON.parse(options.body));
    } else data = {message:'done'};
    return {ok,json:async()=>data};
  }};
vm.createContext(context); vm.runInContext(fs.readFileSync(path.join(root, 'studio/web/app.js'), 'utf8'), context);
const run = expression => vm.runInContext(expression, context), settle = () => new Promise(resolve => setImmediate(resolve));
const control = key => query('[data-setting]').find(node => node.dataset.setting === key);
function edit(key, value) {const node = control(key); node.value = value; node.oninput();}
const latestSave = () => JSON.parse(calls.filter(call => call.url === '/api/settings').at(-1).options.body);
(async () => {
  await settle();
  assert.equal(ids['feature-list'].children.length, 2, 'modules must be grouped into packs');
  assert.equal(ids['feature-list'].children[0].dataset.pack, 'garage');
  assert.equal(document.documentElement.dataset.theme, 'light'); ids.theme.click();
  assert.equal(document.documentElement.dataset.theme, 'dark'); assert.equal(context.localStorage.data['tk2-theme'], 'dark');
  assert.equal(control('Physics/Enabled').disabled, false, 'requested gameplay testing must not be blanket locked');
  assert.equal(query('[data-extra]').length, 1, 'migrated modules must expose typed settings in their pack');
  edit('Audio/MasterVolume', '.4');
  assert.equal(run('state.settings["Audio/MasterVolume"]'), .4); assert.equal(ids['save-settings'].disabled, false);
  fixture.configHash = 'game-rewrote-format'; fixture.settings['Camera/FieldOfView'] = 70;
  const mounted = control('Audio/MasterVolume'); await run('refresh(true, true)');
  assert.equal(control('Audio/MasterVolume'), mounted, 'polling must keep input mounted');
  assert.equal(run('state.settings["Audio/MasterVolume"]'), .4, 'polling must keep unsaved values');
  assert.equal(run('state.settings["Camera/FieldOfView"]'), 70, 'polling must bring in untouched settings');
  assert.equal(run('state.configHash'), 'game-rewrote-format', 'fresh hash can change without losing semantic baseline');
  await ids['save-settings'].click();
  assert.deepEqual(latestSave().values, {'Audio/MasterVolume':.4}, 'send only edited keys');
  assert.deepEqual(latestSave().baseValues, {'Audio/MasterVolume':1}, 'send original per-key baseline');
  assert.equal(run('settingsDirty'), false); assert.equal(ids['settings-error'].hidden, true);
  edit('Camera/FieldOfView', '68'); stateFailure = true; await ids['save-settings'].click();
  assert.equal(run('settingsDirty'), false, 'successful save stays successful if follow-up polling fails');
  assert.equal(ids['settings-error'].hidden, true); stateFailure = false;
  control('Audio/MasterVolume').valid = false;
  const toggle = control('Camera/Enabled'); toggle.checked = true; toggle.onchange(); await ids['save-settings'].click();
  assert.deepEqual(latestSave().values, {'Camera/Enabled':true}, 'a second toggle save needs no retoggle workaround');
  assert.equal(run('settingsDirty'), false, 'an untouched legacy value must not block another module toggle');
  control('Audio/MasterVolume').valid = true;
  assert.equal(run('state.settings["Audio/MasterVolume"]'), .4);
  // An edit while a request is in flight must survive its response.
  deferSave = true; edit('Audio/MasterVolume', '.6'); const request = ids['save-settings'].click(); await settle();
  assert.equal(control('Audio/MasterVolume').disabled, false); edit('Audio/MasterVolume', '.7'); pendingSave(); await request;
  assert.equal(run('state.settings["Audio/MasterVolume"]'), .7); assert.equal(run('settingsDirty'), true);
  assert.equal(run('settingEdits.get("Audio/MasterVolume").base'), .6);
  deferSave = false; await ids['save-settings'].click();
  assert.deepEqual(latestSave().values, {'Audio/MasterVolume':.7}); assert.deepEqual(latestSave().baseValues, {'Audio/MasterVolume':.6});
  assert.equal(run('settingsDirty'), false);
  // Reverting to the pre-save baseline while saving is also a new edit.
  deferSave = true; edit('Audio/MasterVolume', '.2'); const reverting = ids['save-settings'].click(); await settle();
  edit('Audio/MasterVolume', '.7'); pendingSave(); await reverting;
  assert.equal(run('settingEdits.get("Audio/MasterVolume").base'), .2); assert.equal(run('state.settings["Audio/MasterVolume"]'), .7);
  deferSave = false; await ids['save-settings'].click();
  // Only true conflicts offer a reload action, and reloading preserves other edits.
  edit('Audio/MasterVolume', '.3'); edit('Camera/FieldOfView', '80'); fixture.settings['Audio/MasterVolume'] = .8;
  settingsFailure = {error:'Conflicting setting: Audio/MasterVolume.',conflicts:['Audio/MasterVolume']}; await ids['save-settings'].click();
  assert.equal(ids['settings-error'].hidden, false); assert.equal(ids['reload-settings'].hidden, false);
  assert.equal(run('state.settings["Audio/MasterVolume"]'), .3);
  settingsFailure = undefined; await ids['reload-settings'].click();
  assert.equal(run('state.settings["Audio/MasterVolume"]'), .8); assert.equal(run('state.settings["Camera/FieldOfView"]'), 80);
  assert.equal(run('settingEdits.size'), 1); await ids['save-settings'].click();
  edit('Camera/FieldOfView', '75'); settingsFailure = {error:'Temporary settings write failure.'}; await ids['save-settings'].click();
  assert.equal(ids['reload-settings'].hidden, true, 'transient errors should not suggest discarding edits');
  assert.equal(run('settingsDirty'), true); settingsFailure = undefined; await ids['save-settings'].click();
  edit('Camera/FieldOfView', '120'); control('Camera/FieldOfView').valid = false;
  const beforeInvalidSave = calls.filter(call => call.url === '/api/settings').length;
  await ids['save-settings'].click();
  assert.equal(calls.filter(call => call.url === '/api/settings').length, beforeInvalidSave, 'invalid changed values must be stopped before writing');
  assert.match(ids['settings-error-message'].textContent, /Field of view/);
  control('Camera/FieldOfView').valid = true; edit('Camera/FieldOfView', '75');
  const extra = query('[data-extra]')[0]; extra.checked = true; extra.onchange(); await ids['save-settings'].click();
  assert.deepEqual(latestSave().extraSettings, {'MK.BoostTrainer/Enabled':'true'});
  assert.deepEqual(latestSave().baseExtraSettings, {'MK.BoostTrainer/Enabled':'false'});
  ids['mod-search'].value = 'field of view'; ids['mod-search'].oninput();
  assert.equal(ids['feature-list'].children.length, 1, 'search settings as well as modules');
  ids['mod-search'].value = ''; ids['mod-search'].oninput();
  nav[1].click(); await settle(); assert.equal(ids.code.value, '// source');
  ids.code.value = '// edited'; ids.code.oninput(); sourceConflict = true; await ids['save-source'].click();
  assert.equal(ids.code.value, '// edited'); assert.equal(run('sourceDirty'), true); assert.match(ids.notice.textContent, /Source changed/);
  sourceConflict = false; await ids['save-source'].click(); assert.equal(run('sourceDirty'), false); assert.equal(run('source.hash'), 'saved-source-hash');
  const parameterNames = ['Mass','JumpStrength','MaxAccelForward','GroundFrictionFactor','DrivingOnGroundSteerFactor','InstantBoostAddFullInSeconds','InAirAccelerationFactor','MaxVerticalVelocity'];
  const parameterSettings = parameterNames.flatMap(key => [
    ['Override' + key,'Override ' + key,'bool',false,null,null,'Choose a custom value'],
    [key,key,'float',81,0,1000,'Applied only when its override is on',null,'Override' + key]
  ]);
  fixture.features.push({id:'KartParameters',name:'Physics parameters',category:'Driving',description:'Selective overrides',gameplay:true,settings:parameterSettings});
  fixture.packs[1].features.push('KartParameters');
  parameterNames.forEach(key => {fixture.settings['KartParameters/Override' + key] = false; fixture.settings['KartParameters/' + key] = 81;});
  fixture.settings['KartParameters/Enabled'] = false; await run('refresh(true)');
  const parameterValue = control('KartParameters/MaxAccelForward'), parameterToggle = control('KartParameters/OverrideMaxAccelForward');
  assert.equal(parameterValue.disabled,true,'custom value waits for its specific override switch');
  assert.equal(parameterValue.dataset.requires,'KartParameters/OverrideMaxAccelForward');
  assert.equal(query('[data-setting]').filter(node => node.dataset.setting === 'KartParameters/OverrideMaxAccelForward').length,1,'paired override must appear only once');
  const groups = descendants(ids['feature-list']).filter(node => node.classes.has('parameter-group'));
  assert.ok(groups.length >= 4,'large parameter modules need meaningful subsections');
  assert.ok(groups.every(node => !node.open),'large parameter subsections begin collapsed');
  parameterToggle.checked = true; parameterToggle.onchange();
  assert.equal(parameterValue.disabled,false,'switch immediately enables its custom value');
  assert.equal(control('KartParameters/GroundFrictionFactor').disabled,true,'unrelated override stays disabled');
  await ids['save-settings'].click();
  parameterToggle.checked = false; parameterToggle.onchange(); await ids['save-settings'].click();
  assert.equal(parameterValue.disabled,true,'finishing a save must not enable an inactive custom value');
  fixture.settings['KartParameters/OverrideMaxAccelForward'] = true; await run('refresh(true,true)');
  assert.equal(parameterValue.disabled,false,'external override changes synchronize dependency availability');
  ids['mod-search'].value = 'MaxAccelForward'; ids['mod-search'].oninput();
  const matchedGroups = descendants(ids['feature-list']).filter(node => node.classes.has('parameter-group'));
  assert.ok(matchedGroups.some(node => node.open),'search expands the matching parameter group');
  run("resetFeatures([state.features.find(f => f.id === 'Camera')])");
  assert.equal(run("state.settings['Camera/FieldOfView']"),65,'module reset restores its declared default');
  assert.equal(run("state.settings['Camera/Enabled']"),false,'module reset switches feature off');
  ids['mod-search'].value = ''; ids['mod-search'].oninput();
  const hints = descendants(ids['feature-list']).filter(node => node.classes.has('setting-hint'));
  assert.ok(hints.some(node => node.textContent.includes('Default: 65') && node.textContent.includes('35 to 110')),'default and allowed values remain visible');
  assert.ok(fs.readFileSync(path.join(root,'studio/web/style.css'),'utf8').includes('[hidden]{display:none!important}'),'component display cannot override hidden');
  assert.ok(calls.every(call => call.options.headers['X-TK2-Token'] === 'test-token'));
  assert.ok(!html.includes('Make it<br>your race.'), 'remove decorative hero');
  assert.ok(!html.includes('Gameplay modules are locked'), 'remove superseded blanket lock');
  assert.ok(html.includes('<title>TK2 Mod Toolkit</title>') && html.includes('/assets/app.ico'), 'renamed window and supplied favicon');
  assert.equal(ids.shutdown, undefined, 'window close replaces the redundant Close Garage button');
  assert.equal(ids.install, undefined, 'install action belongs only in Installation');
  assert.ok(!calls.some(call => /\/api\/(build|build-install|install)$/.test(call.url)), 'normal setting edits never request a build or install');
  fixture.packs = [{id:'garage', name:'Toolkit Essentials', features:['Audio','Camera']}, {id:'community', name:'Community Mods', features:['Physics','KartParameters']}];
  await run('refresh(true)');
  assert.equal(run("extraPackId({section:'MK.BoostTrainer'})"),'community','legacy extra settings route into the consolidated Community Mods pack');
  console.log(`Offline frontend state: ${assertions} assertions passed. Visual browser verification remains pending.`);
})().catch(error => {console.error(error);process.exitCode = 1;});
