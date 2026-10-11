"""Static UI wiring smoke check. Browser visual inspection is a separate required check."""
from html.parser import HTMLParser
import json
from pathlib import Path
import re
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from studio import core
from studio.frontend import render_html

class Page(HTMLParser):
    def __init__(self): super().__init__(); self.ids = []; self.targets = []
    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if "id" in attrs: self.ids.append(attrs["id"])
        if "data-view" in attrs: self.targets.append(attrs["data-view"])

page = Page()
web = core.ROOT / "studio/web"
html = render_html(web / 'index.html', web)
page.feed(html)
assert len(page.ids) == len(set(page.ids)), "Duplicate UI ids"
assert all(target in page.ids for target in page.targets)
scripts = [web / 'app.js', *sorted((web / 'features').rglob('*.js')), *sorted((web / 'shared').rglob('*.js'))]
for script in scripts:
    source = script.read_text(encoding='utf-8')
    assert all(name in page.ids for name in re.findall(r'''\$\(["']([^"']+)["']\)''', source)), f'Unknown control id in {script.name}'
css = '\n'.join(path.read_text(encoding='utf-8') for path in sorted((web / 'styles').glob('*.css')))
assert 'data-theme' in css and 'prefers-reduced-motion' in css
assert '.module-expand' in css and '.topbar-update' in css
assert '/styles/index.css' in html
assert 'max-width: 1450px' not in css and 'min-height: 100vh' in css
assert '__APP_VERSION__' in html
assert 'id="expand-all-accordions"' in html and 'id="collapse-all-accordions"' in html
assert 'aria-expanded="true"' in css and 'oklch(84% 0.16 72)' in css
assert all((core.ROOT / "assets" / name).exists() for name in ("Logo.png", "library_600x900_2x.jpg"))
for script in scripts:
    subprocess.run(['node', '--check', str(script)], check=True, creationflags=core.CREATE_NO_WINDOW)
result = {"uniqueControls": len(page.ids), "views": page.targets, "themeTokens": True, "assetsPresent": True,
          "componentStyles": "passed",
          "javascriptSyntax": "passed", "visualBrowserCheck": "separate browser check required",
          "gameRuntimeTest": "not run"}
core.write_json(core.ROOT / "local/garage-smoke.json", result)
print(json.dumps(result, indent=2))
