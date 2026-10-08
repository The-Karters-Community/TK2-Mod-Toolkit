"""Create and lay out every tab without modifying the installation or launching the game."""
import json
import sys
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
app.destroy()
core.write_json(core.ROOT / "local/gui-smoke.json", {"tabs": results, "result": "passed", "visibleInteractionTested": False})
print(json.dumps(results, indent=2))
