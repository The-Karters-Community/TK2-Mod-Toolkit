"""Read literal recipe bindings for authoring; never execute imported C#."""
from pathlib import Path
import re
from . import core

INFO = {
    'MirrorRace': ('Mirror race', 'Reflect supported offline track geometry and route data before the game builds its caches. Applies on the next track load.', 'community'),
    'TrackInspector': ('Track inspector', 'Identify invisible walls, respawn boundaries and linked kill zones with clear in-race outlines. Convex and unreadable colliders are sampled from their actual physics surface. Press the configured key for the overlay.', 'garage'),
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
            if key == 'Enabled': continue  # The module header owns this shared host binding.
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
        features.append({'id':'Recipe.' + name, 'name':title, 'category':'Graphics' if name == 'TrackInspector' else 'Driving',
                         'description':description, 'pack':pack, 'gameplay':bool(re.search(r'ChangesGameplay\s*=>\s*true', text)),
                         'origin':'Native-evidence implementation' if name in INFO else 'New recipe', 'settings':settings, 'source':str(file.relative_to(root)).replace('\\','/'),
                         'status': 'Manual in-game validation required.' if name in INFO else '',
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
