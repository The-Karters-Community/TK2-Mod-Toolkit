"""Validate compiled UI references and size; browser inspection remains separate."""
from html.parser import HTMLParser
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WEB = ROOT / "studio/web-dist"


class Page(HTMLParser):
    def __init__(self):
        super().__init__()
        self.assets = []

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        url = attrs.get("src", attrs.get("href", ""))
        if url.startswith("/assets/") and url not in ("/assets/logo.png", "/assets/app.ico"):
            self.assets.append(url)


def main():
    html = (WEB / "index.html").read_text(encoding="utf-8")
    assert "__SESSION_TOKEN__" in html and "__APP_VERSION__" in html
    page = Page()
    page.feed(html)
    assert page.assets, "Missing compiled assets; run npm run build"
    for url in page.assets:
        assert (WEB / url.lstrip("/")).is_file(), f"Missing {url}"
    files = [path for path in WEB.rglob("*") if path.is_file()]
    assert not any(path.suffix == ".map" for path in files)
    size = sum(path.stat().st_size for path in files)
    assert size < 500_000, f"UI exceeds 500 KB raw budget: {size}"
    result = {"compiledFiles": len(files), "bytes": size, "assetReferences": "passed",
              "visualBrowserCheck": "separate browser check required"}
    output = ROOT / "local/garage-smoke.json"
    output.parent.mkdir(exist_ok=True)
    output.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
