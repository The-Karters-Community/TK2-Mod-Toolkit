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
  querySelectorAll(selector) {const tags = selector.split(',').map(value => value.trim().toUpperCase()); return descendants(this).filter(node => tags.includes(node.tagName));}
  replaceChildren(...nodes) {this.children = nodes;}
  setAttribute(name, value) {this.attrs[name] = value;}
  getAttribute(name) {return this.attrs[name];}
  checkValidity() {return this.valid !== false;}
  click() {return this.onclick?.({preventDefault(){}});}
  setRangeText(text, start, end) {this.value = this.value.slice(0, start) + text + this.value.slice(end);}
  dispatchEvent() {this.oninput?.();}
  focus() {document.activeElement = this;}
  setSelectionRange(start, end) {this.selectionStart = start; this.selectionEnd = end;}
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
const document = {documentElement: new Node(), getElementById: id => ids[id] || Object.values(ids).flatMap(descendants).find(node => node.id === id), createElement: tag => new Node(tag), createElementNS: (_namespace, tag) => new Node(tag), querySelector: selector => selector.startsWith('meta') ? meta : brand, querySelectorAll: query, addEventListener(){}};
const fixture = {installed: false, packCurrent: false, pluginCount: 0, game: 'test game', logs: [], backups: [],
  recipes: [{section: 'Recipe.FrameLimiter', key: 'Enabled', type: 'Boolean', value: 'false', choices: [], range: '', description: 'Limit FPS'}],
  extraSettings: [{section: 'MK.BoostTrainer', key: 'Enabled', type: 'Boolean', value: 'false', choices: [], range: '', description: 'Trainer'}],
  extraPacks: {'MK.BoostTrainer': 'mks'},
  settings: {'OnlineProtection/Enabled': true, 'OnlineProtection/BlockLeaderboardUploads': true, 'OnlineProtection/BlockOnlineLobbyJoins': true, 'Audio/Enabled': false, 'Audio/MasterVolume': 1, 'Physics/Enabled': false, 'Camera/Enabled': false, 'Camera/FieldOfView': 65, 'Camera/AimAtKart':true, 'Camera/PanelHotkey':'F8', 'Audio/LargeCount':120, 'BobbyGang/OriginalName':'Legacy Racer', 'BobbyGang/ReplacementName':'Bobby'}, configHash: 'first-hash',
  packs: [{id:'garage', name:'Toolkit Essentials', features:['OnlineProtection','Audio','Camera']}, {id:'mks', name:"Community Mods", features:['Physics','BobbyGang']}, {id:'community', name:'Community Pack', features:[]}],
  moduleSources: {Camera:['plugins/TK2.Customization/CameraFeature.cs','plugins/TK2.Customization/CameraSettings.cs'], Audio:['plugins/TK2.Customization/AudioFeature.cs'], BobbyGang:['plugins/TK2.Customization/LegacyMK.cs'], 'Recipe.FrameLimiter':['plugins/TK2.Customization/Recipes/FrameLimiter.cs']},
  files: ['src/Reconstructed/KartLogic.cs','plugins/TK2.Customization/CameraFeature.cs','plugins/TK2.Customization/CameraSettings.cs','plugins/TK2.Customization/AudioFeature.cs','plugins/TK2.Customization/Recipes/FrameLimiter.cs'], features: [
    {id:'OnlineProtection', name:'Online protection', category:'Safety', description:'Mandatory protection', origin:'New', locked:true, settings:[['BlockLeaderboardUploads','Block leaderboard uploads','bool',true],['BlockOnlineLobbyJoins','Block online rooms','bool',true]]},
    {id:'Audio', name:'Audio mixer', category:'Audio', description:'Volume adjustment', origin:'New', settings:[['MasterVolume','Volume','float',1,0,1],['LargeCount','Large count','int',120,0,20000]]},
    {id:'Physics', name:'Fast fall', category:'Driving', description:'Physics description', origin:'Adapted', gameplay:true, settings:[]},
    {id:'Camera', name:'Camera', category:'Camera', description:'Keep your kart in view', origin:'New', settings:[['FieldOfView','Field of view','float',65,35,110],['AimAtKart','Aim at kart','bool',true,null,null],['PanelHotkey','Panel hotkey','text','F8',null,null,'Camera key',['F8','C','F6']]]}
  ]};
const calls = []; let sourceConflict = false, settingsFailure, stateFailure = false, deferSave, pendingSave, hashCounter = 0, deferFunctions, pendingFunctions, functionFailure;
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
    else if (url.startsWith('/api/source?')) data = {file:decodeURIComponent(url.split('file=')[1]),content:'// source',hash:'source-hash'};
    else if (url.startsWith('/api/functions?')) {
      ok = !functionFailure;
      data = functionFailure ? {error:'Catalog temporarily unavailable'} : {total:2,indexed:32350,reconstructed:12,methods:[{id:url.includes('offset=80')?'1:2':'1:1',type:'PixelKartPhysics',name:'AddVelocity',label:'Add Velocity',signature:'internal void AddVelocity(Vector3 velocity);',source:'src/Reconstructed/KartLogic.cs'}],next:url.includes('offset=80')?null:80};
      if (deferFunctions) data = await new Promise(resolve => {pendingFunctions = () => resolve(data);});
    }
    else if (url.startsWith('/api/function?')) data = {type:'PixelKartPhysics',name:'AddVelocity',label:'Add Velocity',access:'internal',status:'Editable reconstructed C# available',source:'src/Reconstructed/KartLogic.cs',declaration:'internal void AddVelocity(Vector3 velocity);'};
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
  assert.equal(control('OnlineProtection/Enabled'), undefined, 'mandatory protection has no enable toggle');
  assert.equal(control('OnlineProtection/BlockLeaderboardUploads').disabled, true, 'leaderboard protection cannot be edited');
  assert.equal(control('OnlineProtection/BlockOnlineLobbyJoins').disabled, true, 'lobby protection cannot be edited');
  run(`resetFeatures(state.features.filter(feature => feature.id === 'OnlineProtection')); syncDependencies()`);
  assert.equal(run('settingEdits.has("OnlineProtection/Enabled")'), false, 'reset cannot turn off mandatory protection');
  assert.equal(control('OnlineProtection/BlockLeaderboardUploads').disabled, true, 'dependency refresh preserves the lock');
  const characterName = run(`settingsControl('BobbyGang/OriginalName','Original name','text','Saved custom racer',null,null,'Choose a built-in or saved custom name.',['Mia','Bobby','Bubble'],()=>{},'setting','Bubble')`);
  assert.equal(characterName.field.tagName, 'SELECT', 'original character name is selected from a menu');
  assert.ok(characterName.field.children.some(option => option.value === 'Saved custom racer' && /Saved custom/.test(option.text)), 'legacy custom names remain selectable');
  const replacementName = run(`settingsControl('BobbyGang/ReplacementName','Replacement name','text','My racer',null,null,'Replacement text.',null,()=>{},'setting','Bobby')`);
  assert.equal(replacementName.field.tagName, 'INPUT'); assert.equal(replacementName.field.type, 'text', 'replacement character name remains editable');
  run(`state.setup={ready:true};state.installed=true;state.packCurrent=false;renderInstallation()`);
  assert.equal(ids['plugin-update-top'].hidden, false, 'available plugin updates are shown in the top bar');
  assert.equal(ids['install-state'].textContent, 'Plugin update available');
  run(`state.packCurrent=true;renderInstallation()`);
  assert.equal(ids['plugin-update-top'].hidden, true, 'up-to-date plugins do not show an update action');
  const accordion = descendants(ids['feature-list']).find(node => node.classes.has('module-expand'));
  const panelId = accordion.getAttribute('aria-controls'), accordionPanel = descendants(ids['feature-list']).find(node => node.id === panelId);
  const chevron = accordion.children.find(node => node.tagName === 'SVG');
  assert.equal(chevron.getAttribute('viewBox'), '0 0 24 24');
  assert.match(chevron.children[0].getAttribute('d'), /M16\.59 8\.59/);
  accordion.click(); assert.equal(accordion.getAttribute('aria-expanded'), 'true'); assert.equal(accordionPanel.hidden, false, 'module accordion opens and updates its accessible state');
  assert.equal(run("document.querySelectorAll('[data-accordion-key]').length"), 6, 'all current accordion controls can be selected');
  ids['expand-all-accordions'].click();
  const accordions = [...descendants(ids['feature-list']), ...descendants(ids['recipe-controls'])].filter(node => node.dataset.accordionKey);
  assert.ok(accordions.length > 2, 'all module and recipe accordions are included');
  assert.ok(accordions.every(node => node.tagName === 'DETAILS' ? node.open : node.getAttribute('aria-expanded') === 'true'), 'Expand all opens visible module and recipe sections');
  ids['collapse-all-accordions'].click();
  assert.ok(accordions.every(node => node.tagName === 'DETAILS' ? !node.open : node.getAttribute('aria-expanded') === 'false'), 'Collapse all closes visible module and recipe sections');
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
  nav[1].click(); await settle(); assert.equal(ids.code.value, '', 'workshop opens an empty editor instead of an unrelated game implementation');
  const cameraEdit = query('[data-edit-module]').find(node => node.dataset.editModule === 'Camera');
  assert.equal(cameraEdit.disabled,false); cameraEdit.click(); await settle(); assert.equal(ids.code.value, '// source');
  assert.equal(run('source.file'),'plugins/TK2.Customization/CameraFeature.cs');
  assert.equal(ids['module-focus'].value,'Camera');
  assert.equal(ids['pack-files'].children.length,2, 'module navigation shows its behavior and settings files');
  assert.equal(ids['reconstructed-files'].children.length,0, 'module navigation does not mix unrelated recovered code');
  ids.code.value = '// edited'; ids.code.oninput(); sourceConflict = true; await ids['save-source'].click();
  assert.equal(ids.code.value, '// edited'); assert.equal(run('sourceDirty'), true); assert.match(ids.notice.textContent, /Source changed/);
  sourceConflict = false; await ids['save-source'].click(); assert.equal(run('sourceDirty'), false); assert.equal(run('source.hash'), 'saved-source-hash');
  assert.match(ids['editor-state'].textContent,/File saved/);
  ids['code-find'].value='edited'; ids['find-next'].click(); assert.equal(ids.code.selectionStart,3); assert.equal(ids.code.selectionEnd,9);
  document.activeElement = undefined;
  await ids['open-source-folder'].click(); assert.ok(calls.some(call => call.url === '/api/open-source-folder'));
  ids['tab-api'].click(); await settle();
  assert.equal(ids['panel-editor'].hidden,true); assert.equal(ids['panel-api'].hidden,false);
  assert.ok(calls.some(call => call.url.includes('topic=important')), 'API opens with important game classes');
  const firstFunction = ids['function-list'].children[0];
  assert.equal(firstFunction.children[0].textContent,'Add Velocity');
  assert.ok(firstFunction.children.every(node => node.tagName !== 'CODE'), 'API navigation contains names rather than code blocks');
  await firstFunction.click(); assert.equal(ids['function-detail'].textContent,'internal void AddVelocity(Vector3 velocity);');
  assert.equal(ids['function-example'].hidden,false); assert.match(ids['function-usage'].textContent,/ReadableGame.AddVelocity/);
  const beforePages = calls.filter(call => call.url.startsWith('/api/functions?')).length;
  ids['function-scroll'].scrollHeight=400; ids['function-scroll'].scrollTop=250; ids['function-scroll'].clientHeight=200;
  deferFunctions=true; ids['function-scroll'].onscroll(); ids['function-scroll'].onscroll(); await settle();
  assert.equal(calls.filter(call => call.url.startsWith('/api/functions?')).length,beforePages+1,'only one next-page request at a time');
  assert.equal(ids['function-loading'].hidden,false,'scroll loading shows a skeleton');
  pendingFunctions(); deferFunctions=false; await settle(); assert.equal(ids['function-loading'].hidden,true);
  assert.equal(ids['function-list'].children.length,2); assert.equal(run('nextFunctions'),null);
  ids['function-scroll'].onscroll(); await settle(); assert.equal(calls.filter(call => call.url.startsWith('/api/functions?')).length,beforePages+1,'end of results stops loading');
  ids['function-topic'].value='camera'; ids['function-topic'].onchange(); await settle(); assert.equal(ids['function-list'].children.length,1, 'new category clears previous results');
  functionFailure=true; await ids['function-search'].click(); assert.match(ids['function-end'].textContent,/Could not load/); assert.equal(run('functionLoading'),false);
  functionFailure=false; await ids['function-search'].click(); assert.equal(ids['function-list'].children.length,1,'failed search can be retried');
  await ids['function-list'].children[0].click(); ids['open-function-source'].click(); await settle();
  assert.equal(run('source.file'),'src/Reconstructed/KartLogic.cs'); assert.equal(ids['panel-editor'].hidden,false);
  ids['tab-tutorial'].click(); assert.equal(ids['panel-tutorial'].hidden,false); assert.equal(ids['panel-api'].hidden,true);
  assert.equal(ids['functions-more'],undefined,'infinite scrolling replaces the Load more button');
  assert.ok(html.includes('Your first mod: a kart hop') && html.includes('Recipes/MyKartHop.cs'));
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
  ids['expand-all-accordions'].click();
  assert.ok(groups.every(node => node.open),'Expand all includes nested parameter accordions');
  ids['collapse-all-accordions'].click();
  assert.ok(groups.every(node => !node.open),'Collapse all includes nested parameter accordions');
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
  const optionCheckbox = control('Camera/AimAtKart');
  assert.equal(typeof optionCheckbox.onchange,'function','boolean options need a real change handler');
  optionCheckbox.checked = false; optionCheckbox.onchange();
  assert.equal(run("state.settings['Camera/AimAtKart']"),false);
  assert.equal(ids['save-settings'].disabled,false,'checkbox change prompts saving');
  await run('refresh(true,true)');
  assert.equal(control('Camera/AimAtKart').checked,false,'poll preserves unsaved checkbox edits');
  await ids['save-settings'].click();
  assert.equal(latestSave().values['Camera/AimAtKart'],false,'saving checkbox sends a boolean');
  const hotkey = control('Camera/PanelHotkey');
  assert.equal(typeof hotkey.onchange,'function','hotkey dropdowns need a change handler');
  hotkey.value = 'C'; hotkey.onchange();
  assert.equal(run("state.settings['Camera/PanelHotkey']"),'C');
  assert.equal(ids['save-settings'].disabled,false,'key change prompts saving');
  await run('refresh(true,true)');
  assert.equal(control('Camera/PanelHotkey').value,'C','poll preserves unsaved key edits');
  await ids['save-settings'].click();
  assert.equal(latestSave().values['Camera/PanelHotkey'],'C');
  const plainNumber = control('Audio/LargeCount');
  assert.equal(plainNumber.slider,undefined);
  assert.equal(typeof plainNumber.oninput,'function','number inputs without sliders need handlers');
  plainNumber.value = '180'; plainNumber.oninput();
  assert.equal(run("state.settings['Audio/LargeCount']"),180);
  await ids['save-settings'].click();
  assert.equal(latestSave().values['Audio/LargeCount'],180);
  const cameraValue = control('Camera/FieldOfView');
  assert.ok(cameraValue.slider, 'bounded numeric options have an interactive slider');
  cameraValue.slider.value = '82'; cameraValue.slider.oninput();
  assert.equal(run("state.settings['Camera/FieldOfView']"),82,'slider stages a typed value');
  assert.equal(Number(cameraValue.value),82,'slider keeps the number field synchronized');
  cameraValue.linkedControls[0].click();
  assert.equal(run("state.settings['Camera/FieldOfView']"),65,'value reset restores its own default');
  assert.equal(Number(cameraValue.slider.value),65,'reset synchronizes the slider');
  const headerResets = descendants(ids['feature-list']).filter(node => node.classes.has('module-reset'));
  assert.ok(headerResets.length >= fixture.features.length,'every module has a reset without expansion');
  ids['tab-sharing'].click(); assert.equal(ids['panel-sharing'].hidden,false);
  ids['export-select-all'].click(); assert.equal(run('exportSelection.size'),fixture.features.length);
  assert.equal(ids['export-package'].disabled,false,'selected modules enable export');
  ids['export-select-none'].click(); assert.equal(run('exportSelection.size'),0); assert.equal(ids['export-package'].disabled,true);
  run("stageImportedSettings({'Camera/FieldOfView':72,'Camera/Enabled':false})");
  assert.equal(run("state.settings['Camera/FieldOfView']"),72,'imported preset is staged without writing the config');
  assert.equal(run('settingEdits.has("Camera/FieldOfView")'),true,'imported values use the normal safe save flow');
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
