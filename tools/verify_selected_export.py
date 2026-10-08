"""Workspace integration check for address-specific export of an overloaded game method."""
import json
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from studio import core

_, hits = core.search_native_functions("HpBarController$$Hit")
overloads = [e for e in hits if e["name"] == "HpBarController$$Hit"]
assert len(overloads) == 2
selected = overloads[1]
home = core.ROOT.parent / "ghidra_12.1.4_PUBLIC_20260921/ghidra_12.1.4_PUBLIC"
project = core.ROOT.parent / "ghidraOutput/TK2_1_4_18.gpr"
summary = core.export_ghidra(home, project, lambda message: None, selected["name"], selected["address"])
assert summary["exported"] == 1 and summary["failed"] == 0
assert summary["sha256"] == core.sha256(core.DEFAULT_GAME / "GameAssembly.dll")
assert "Ghidra pseudocode" in core.native_description(selected)
core.write_json(core.ROOT / "local/selected-export-validation.json", {"selected": selected, "summary": summary})
print(json.dumps({"selected": selected, "summary": summary}, indent=2))
