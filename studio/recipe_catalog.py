"""Read literal recipe bindings for authoring; never execute imported C#."""
from pathlib import Path
import re
from . import core

INFO = {
    'SlipstreamSling': ('Slipstream sling', 'Charge behind another kart, then release a straight-line dash.', 'mechanics'),
    'DriftCapacitor': ('Drift capacitor', 'Bank charge while drifting and spend it on a burst when you choose.', 'mechanics'),
    'AirGlider': ('Air glider', 'Trade a limited air fuel supply for lift and steering while airborne.', 'mechanics'),
    'EchoRewind': ('Echo rewind', 'Rewind your kart along a short recorded trail without rewinding race progress.', 'mechanics'),
    'RepulsorPulse': ('Repulsor pulse', 'Release a timed proximity pulse that pushes nearby rival karts away.', 'mechanics'),
    'GravitySurf': ('Gravity surf', 'Build energy downhill and use it to climb or release a momentum burst.', 'mechanics'),
    'LandingCombo': ('Landing combo', 'Time your landing input to chain increasingly strong forward impulses.', 'mechanics'),
    'CosmeticModel': ('Imported kart model', 'Attach your imported static model to the local kart and tune its placement.', 'cosmetics'),
}
KEYS = ['F' + str(i) for i in range(1, 13)] + list('ABCDEFGHIJKLMNOPQRSTUVWXYZ') + ['Alpha' + str(i) for i in range(10)] + ['Space','LeftShift','RightShift','LeftControl','RightControl','UpArrow','DownArrow','LeftArrow','RightArrow','None']


def arguments(text, start):
    """Split C# arguments respecting strings, nesting and generic commas."""
    result, begin, depth, quoted, escaped = [], start, 0, False, False
    for index in range(start, len(text)):
        char = text[index]
        if quoted:
            if escaped: escaped = False
            elif char == '\\': escaped = True
            elif char == '"': quoted = False
            continue
        if char == '"': quoted = True
        elif char in '(<[': depth += 1
        elif char in ')>]':
            if char == ')' and depth == 0:
                return result + [text[begin:index].strip()]
            depth -= 1
        elif char == ',' and depth == 0: result.append(text[begin:index].strip()); begin = index + 1
    raise ValueError('Incomplete configuration binding')


def literal(text):
    text = text.strip()
    if text.startswith('"') and text.endswith('"'):
        import json
        try: return json.loads(text)
        except ValueError: return None
    if text == 'string.Empty': return ''
    if text in ('true', 'false'): return text == 'true'
    if re.fullmatch(r'KeyCode\.\w+', text): return text.split('.')[-1]
    if re.fullmatch(r'-?(?:\d+(?:\.\d*)?|\.\d+)[fFdD]?', text):
        return float(text.rstrip('fFdD')) if '.' in text or text[-1:] in 'fFdD' else int(text)
    return None


def source_features(source_root=None):
    root = source_root or core.ROOT / 'plugins/TK2.Customization'
    features = []
    for file in sorted(root.rglob('*.cs')):
        if any(p in ('bin', 'obj') for p in file.relative_to(root).parts): continue
        text = file.read_text(encoding='utf-8-sig')
        name = re.search(r'public\s+(?:override\s+)?string\s+Name\s*=>\s*"([A-Za-z][A-Za-z0-9]{1,63})"', text)
        if not name: continue
        name = name[1]
        title, description, pack = INFO.get(name, (re.sub(r'(?<=[a-z])(?=[A-Z])', ' ', name), 'Your editable C# recipe.', 'recipes'))
        settings, unsupported = [], []
        for bind in re.finditer(r'\bconfig\.Bind(?:<[^>]+>)?\s*\(', text):
            try: args = arguments(text, bind.end())
            except ValueError: unsupported.append('Incomplete Bind'); continue
            if len(args) < 3 or not re.fullmatch(r'"Recipe\."\s*\+\s*Name', args[0]): continue
            key, default = literal(args[1]), literal(args[2])
            if not isinstance(key, str) or default is None: unsupported.append(args[1]); continue
            low = high = None
            bounds = re.search(r'new\s+AcceptableValueRange<(float|int)>\s*\(([^,]+),\s*([^\)]+)\)', ','.join(args[3:]))
            if bounds: low, high = literal(bounds[2]), literal(bounds[3])
            kind = 'bool' if isinstance(default, bool) else 'int' if isinstance(default, int) else 'float' if isinstance(default, float) else 'text'
            if kind in ('float','int') and (low is None or high is None): unsupported.append(key); continue
            desc = literal(args[3]) if len(args) > 3 else ''
            if not isinstance(desc, str):
                match = re.search(r'ConfigDescription\(\s*("(?:[^"\\]|\\.)*")', ','.join(args[3:]))
                desc = literal(match[1]) if match else ''
            choices = list(dict.fromkeys([str(default), *KEYS])) if args[2].startswith('KeyCode.') else None
            settings.append((key, re.sub(r'(?<=[a-z0-9])(?=[A-Z])', ' ', key), kind, default, low, high, desc, choices, None))
        if name == 'CosmeticModel':
            settings = [('ModelPath','Model path','text','',None,None,'Relative to BepInEx/models; choose a model in Workshop → Models.',None,None),
                        ('AssetName','Bundle prefab','text','',None,None,'Empty selects the first prefab in a Unity bundle.',None,None)]
            for key, label, default, low, high in [('Scale','Scale',1.0,.01,100), *[(f'Offset{a}',f'Position {a}',0.0,-10,10) for a in 'XYZ'], *[(f'Rotation{a}',f'Rotation {a}',0.0,-180,180) for a in 'XYZ']]:
                settings.append((key,label,'float',default,low,high,'Cosmetic transform; physics and collision stay unchanged.',None,None))
            settings += [('HideOriginalKart','Hide original kart','bool',False,None,None,'Keep the driver visible.',None,None),
                         ('MirrorX','Convert OBJ handedness','bool',True,None,None,'Reloads geometry when changed.',None,None),
                         ('Tint','Diffuse tint','text','#FFFFFF',None,None,'Use a #RRGGBB colour.',None,None),
                         ('ReloadToken','Reload revision','int',0,0,2147483647,'Increase after editing textures or materials.',None,None)]
            unsupported = []
        features.append({'id':'Recipe.' + name, 'name':title, 'category':'Driving' if pack == 'mechanics' else 'Graphics',
                         'description':description, 'pack':pack, 'gameplay':bool(re.search(r'ChangesGameplay\s*=>\s*true', text)) or pack == 'mechanics',
                         'origin':'New recipe', 'settings':settings, 'source':str(file.relative_to(root)).replace('\\','/'),
                         'catalogWarnings':unsupported})
    return features


def features():
    result = source_features()
    compiled = core.ROOT / 'artifacts/TK2.Customization/recipe-catalog.json'
    import json
    stored = {f['id']:f for f in json.loads(compiled.read_text(encoding='utf-8'))} if compiled.is_file() else {}
    for feature in result:
        feature['compiled'] = feature['id'] in stored
        if feature['compiled']: feature['settings'] = stored[feature['id']]['settings']
    return result
