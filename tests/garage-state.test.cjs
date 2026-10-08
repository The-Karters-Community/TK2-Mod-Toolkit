// Offline UI state tests using an in-memory DOM stub. No browser or network access.
const fs = require('node:fs');
const vm = require('node:vm');
let assertions = 0;
const assert = new Proxy(require('node:assert/strict'), {get(target, key) {
  const value = Reflect.get(target, key);
  return typeof value === 'function' ? (...args) => {assertions++; return value(...args);} : value;
}});
const path = require('node:path');
const root = path.resolve(__dirname, '..');
class Node {
  constructor(tag = 'div') {this.tagName = tag.toUpperCase(); this.children = []; this.dataset = {}; this.value = ''; this.disabled = false; this.hidden = false; this.classes = new Set(); this.attrs = {}; this.classList = {toggle: (name, force) => {if (force) this.classes.add(name); else this.classes.delete(name);}};}
  set className(value) {this.classes = new Set(value.split(' '));}
  set textContent(value) {this.text = value; this.children = [];}
  get textContent() {return this.text;}
  get firstChild() {if (!this.children[0]) this.children[0] = new Node('text'); return this.children[0];}
  append(...nodes) {this.children.push(...nodes);}
  replaceChildren(...nodes) {this.children = nodes;}
  setAttribute(name, value) {this.attrs[name] = value;}
  checkValidity() {return true;}
  click() {return this.onclick?.({preventDefault(){}});}
  setRangeText(text, start, end) {this.value = this.value.slice(0, start) + text + this.value.slice(end);}
  dispatchEvent() {this.oninput?.();}
}
const html = fs.readFileSync(path.join(root, 'studio/web/index.html'), 'utf8');
const ids = Object.fromEntries([...html.matchAll(/id="([^"]+)"/g)].map(match => [match[1], new Node()]));
const views = ['mods', 'workshop', 'installation'].map(id => {ids[id].id = id; return ids[id];});
const nav = views.map(view => {const node = new Node('button'); node.dataset.view = view.id; return node;});
const filters = ['All', 'Visual', 'Audio', 'Gameplay'].map(category => {const node = new Node('button'); node.dataset.category = category; return node;});
const brand = new Node('a'); const meta = {content: 'test-token'};
function descendants(node) {return [node, ...node.children.flatMap(descendants)];}
const document = {documentElement: new Node(), getElementById: id => ids[id], createElement: tag => new Node(tag),
  querySelector: selector => selector.startsWith('meta') ? meta : brand,
  querySelectorAll: selector => selector === '.view' ? views : selector === '.nav' ? nav : selector === '[data-category]' ? filters : selector === '[data-setting]' ? Object.values(ids).flatMap(descendants).filter(node => node.dataset.setting) : [],
  addEventListener() {}};
const fixture = {installed: false, packCurrent: false, pluginCount: 0, game: 'test game', logs: [], backups: [], recipes: [],
  settings: {'Audio/Enabled': false, 'Audio/MasterVolume': 1, 'Physics/Enabled': false}, configHash: 'first-hash',
  files: ['src/Reconstructed/KartLogic.cs'], features: [
    {id: 'Audio', name: 'Audio mixer', category: 'Audio', description: 'Audio description', origin: 'New', settings: [['MasterVolume', 'Volume', 'float', 1, 0, 1]]},
    {id: 'Physics', name: 'Fast fall', category: 'Driving', description: 'Physics description', origin: 'Adapted', gameplay: true, settings: []}
  ]};
const calls = []; let conflict = false;
const context = {document, console, location: {hash: '#mods'}, history: {replaceState(_a,_b,hash){context.location.hash = hash;}},
  localStorage: {data: {}, getItem(key){return this.data[key] || null;}, setItem(key,value){this.data[key] = value;}},
  matchMedia: () => ({matches: false, addEventListener(){}}), setTimeout: () => 1, clearTimeout(){}, window: {addEventListener(){}}, confirm: () => true, Event: class {},
  fetch: async (url, options) => {
    calls.push({url, options}); let data;
    if (url === '/api/state') data = structuredClone(fixture);
    else if (url.startsWith('/api/source?')) data = {file: fixture.files[0], content: '// source', hash: 'source-hash'};
    else if (url === '/api/save-source') data = conflict ? {error: 'Source changed outside the app'} : {hash: 'saved-hash'};
    else data = {message: 'done'};
    return {ok: !(url === '/api/save-source' && conflict), json: async () => data};
  }};
vm.createContext(context);
vm.runInContext(fs.readFileSync(path.join(root, 'studio/web/app.js'), 'utf8'), context);
const run = expression => vm.runInContext(expression, context);
const settle = () => new Promise(resolve => setImmediate(resolve));
(async () => {
  await settle();
  assert.equal(ids['feature-list'].children.length, 2);
  assert.equal(document.documentElement.dataset.theme, 'light');
  ids.theme.click(); assert.equal(document.documentElement.dataset.theme, 'dark');
  assert.equal(context.localStorage.data['tk2-theme'], 'dark');
  const rows = ids['feature-list'].children;
  assert.equal(rows[1].children.at(-1).children[0].disabled, true, 'gameplay gate must be visibly disabled');
  const volume = rows[0].children[2].children[0].children[1];
  volume.value = '.4'; volume.oninput();
  assert.equal(run('state.settings["Audio/MasterVolume"]'), .4);
  assert.equal(ids['save-settings'].disabled, false);
  fixture.configHash = 'externally-updated'; await run('refresh(true)');
  assert.equal(run('state.settings["Audio/MasterVolume"]'), .4, 'refresh must preserve unsaved settings');
  assert.equal(run('state.configHash'), 'first-hash', 'refresh must preserve stale hash to reject external overwrite');
  nav[1].click(); await settle();
  assert.equal(ids.code.value, '// source', 'workshop must open actual reconstructed C#');
  ids.code.value = '// edited'; ids.code.oninput();
  conflict = true; await ids['save-source'].click();
  assert.equal(ids.code.value, '// edited');
  assert.equal(run('sourceDirty'), true, 'failed save must preserve unsaved code');
  assert.match(ids.notice.textContent, /Source changed/);
  conflict = false; await ids['save-source'].click();
  assert.equal(run('sourceDirty'), false);
  assert.equal(run('source.hash'), 'saved-hash');
  assert.ok(calls.every(call => call.options.headers['X-TK2-Token'] === 'test-token'));
  console.log(`Offline frontend state: ${assertions} assertions passed. Visual browser verification remains pending.`);
})().catch(error => {console.error(error); process.exitCode = 1;});
