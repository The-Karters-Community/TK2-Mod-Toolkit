"""Create and lay out every tab without modifying the installation or launching the game."""
import json
import sys
import tempfile
from unittest.mock import patch
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from studio.gui import Studio
from studio import core

app = Studio()
app.geometry("1200x820+10000+10000")
app.update()
results = []
for name, frame in app.tab_frames.items():
    app.tabs.select(frame)
    app.update()
    results.append({"tab": name, "children": len(frame.winfo_children()),
                    "width": frame.winfo_width(), "height": frame.winfo_height()})
assert len(results) == 7
assert all(tab["width"] > 1000 and tab["height"] > 500 for tab in results)
assert len(app.sources) >= 3
assert len(app.setting_vars) == 10
assert app.catalog and app.catalog["typeCount"] > 20000
app.browser_mode.set("Native pseudocode")
app.refresh_browser()
assert app.browser_items
app.browser_list.selection_set(0)
app.show_browser_item()
assert "Ghidra pseudocode" in app.browser_text.get("1.0", "end")
app.browser_mode.set("Native function index")
app.search.set("HpBarController$$Hit")
app.refresh_browser()
assert len(app.browser_items) >= 2
app.browser_list.selection_clear(0, "end")
app.browser_list.selection_set(0)
app.show_browser_item()
assert "HpBarController$$Hit" in app.browser_text.get("1.0", "end")
index = next(i for i, p in enumerate(app.configs) if p.name == "MKsKartersMods.cfg")
app.cfg_combo.current(index)
app.open_config()
assert len(app.cfg_entries) == 90
toggle = next(i for i, e in enumerate(app.cfg_entries) if e["section"] == "ModsToEnable" and e["type"] == "Boolean")
app.cfg_tree.selection_set(str(toggle))
app.select_cfg_setting()
app.cfg_value.set("false")
app.apply_cfg_setting()
assert app.cfg_text.edit_modified()  # edited in memory only; do not save to the game
with tempfile.TemporaryDirectory() as directory:
    path = Path(directory) / "Plugin.cs"
    path.write_text("original", encoding="utf-8")
    app.load_source(path)
    path.write_text("external edit", encoding="utf-8")
    assert app.save_source()  # no GUI edits: leave external edits intact
    assert path.read_text() == "external edit"
    app.editor.insert("end", "studio edit")
    app.editor.edit_modified(True)
    with patch("studio.gui.messagebox.showerror"):
        assert not app.save_source()
    assert path.read_text() == "external edit"
app.destroy()
core.write_json(core.ROOT / "local/gui-smoke.json", {"tabs": results, "result": "passed", "configControls": 90,
                 "externalEditorProtection": True, "nativeOverloadBrowser": True, "visibleInteractionTested": False})
print(json.dumps(results, indent=2))
