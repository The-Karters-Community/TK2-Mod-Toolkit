"""Keep first-launch GUI defaults identical to the compiled community bindings."""
import json
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]


class CommunityCatalogTests(unittest.TestCase):
    def test_all_sixteen_bindings_match_defaults_and_ranges(self):
        features = json.loads((ROOT / "studio/community_catalog.json").read_text(encoding="utf-8"))
        feature = next(f for f in features if f["id"] == "CommunityCommands")
        self.assertEqual(feature["id"], "CommunityCommands")
        self.assertTrue(feature["gameplay"])
        expected = {"Enabled": ("bool", False, None, None)}
        for key, _, kind, default, low, high in feature["settings"]:
            self.assertNotIn(key, expected)
            expected[key] = (kind, default, low, high)
        self.assertEqual(len(expected), 16)
        source = (ROOT / "plugins/TK2.Customization/CommunityMods.cs").read_text(encoding="utf-8")
        actual = {}
        for line in source.splitlines():
            match = re.search(r'cfg\.Bind\("CommunityCommands", "([^"]+)", ("[^"]*"|false|true|\d+(?:\.\d+)?f?),', line)
            if not match:
                continue
            key, literal = match.groups()
            if literal in ("false", "true"):
                kind, value = "bool", literal == "true"
            elif literal.startswith('"'):
                kind, value = "text", json.loads(literal)
            else:
                kind, value = ("float", float(literal[:-1])) if literal.endswith("f") else ("int", int(literal))
            bounds = re.search(r'AcceptableValueRange<[^>]+>\((\d+), (\d+)\)', line)
            low, high = (int(bounds[1]), int(bounds[2])) if bounds else (None, None)
            actual[key] = (kind, value, low, high)
        self.assertEqual(actual, expected)

    def test_nightmare_bindings_match_gui_catalog(self):
        features = json.loads((ROOT / "studio/community_catalog.json").read_text(encoding="utf-8"))
        feature = next(f for f in features if f["id"] == "NightmareAI")
        expected = {"Enabled": ("bool", False, None, None)}
        for key, _, kind, default, low, high in feature["settings"]:
            self.assertNotIn(key, expected)
            expected[key] = (kind, default, low, high)
        self.assertEqual(len(expected), 20)
        source = (ROOT / "plugins/TK2.Customization/NightmareAI.cs").read_text(encoding="utf-8")
        actual = {}
        for line in source.splitlines():
            match = re.search(r'cfg\.Bind\("NightmareAI", "([^"]+)", ("[^"]*"|false|true|\d+(?:\.\d+)?f?),', line)
            if not match:
                continue
            key, literal = match.groups()
            if literal in ("false", "true"):
                kind, value = "bool", literal == "true"
            elif literal.startswith('"'):
                kind, value = "text", json.loads(literal)
            else:
                kind, value = ("float", float(literal[:-1])) if literal.endswith("f") else ("int", int(literal))
            bounds = re.search(r'AcceptableValueRange<[^>]+>\((\d+(?:\.\d+)?)(?:f)?, (\d+(?:\.\d+)?)(?:f)?\)', line)
            low, high = (float(bounds[1]), float(bounds[2])) if bounds else (None, None)
            actual[key] = (kind, value, low, high)
        self.assertEqual(actual, expected)


if __name__ == "__main__":
    unittest.main()
