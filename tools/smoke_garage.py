"""Static UI wiring smoke check. Browser visual inspection is a separate required check."""
from html.parser import HTMLParser
import json
from pathlib import Path
import re
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from studio import core

class Page(HTMLParser):
    def __init__(self): super().__init__(); self.ids = []; self.targets = []
    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if "id" in attrs: self.ids.append(attrs["id"])
        if "data-view" in attrs: self.targets.append(attrs["data-view"])

page = Page()
web = core.ROOT / "studio/web"
page.feed((web / "index.html").read_text(encoding="utf-8"))
assert len(page.ids) == len(set(page.ids)), "Duplicate UI ids"
assert all(target in page.ids for target in page.targets)
script = (web / "app.js").read_text(encoding="utf-8")
assert all(name in page.ids for name in re.findall(r"\$\('([^']+)'\)", script)), "Unknown control id"
css = (web / "style.css").read_text(encoding="utf-8")
assert "data-theme=dark" in css and "prefers-reduced-motion" in css
assert all((core.ROOT / "assets" / name).exists() for name in ("Logo.png", "library_600x900_2x.jpg"))
subprocess.run(["node", "--check", str(web / "app.js")], check=True, creationflags=core.CREATE_NO_WINDOW)
result = {"uniqueControls": len(page.ids), "views": page.targets, "themeTokens": True, "assetsPresent": True,
          "javascriptSyntax": "passed", "visualBrowserCheck": "blocked by declined browser permission; pending",
          "gameRuntimeTest": "not run"}
core.write_json(core.ROOT / "local/garage-smoke.json", result)
print(json.dumps(result, indent=2))
