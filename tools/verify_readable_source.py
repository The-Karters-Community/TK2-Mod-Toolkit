"""Verify native reconstruction provenance without executing the game."""
import json
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from studio import core

manifest = json.loads((core.ROOT / "src/Reconstructed/provenance.json").read_text())
binary = core.DEFAULT_GAME / "GameAssembly.dll"
if core.sha256(binary) != manifest["gameAssemblySha256"]: raise RuntimeError("Different native game build")
for method in manifest["methods"]:
    path = core.contained(core.ROOT, core.ROOT / method["evidence"])
    if core.sha256(path) != method["evidenceSha256"]: raise RuntimeError("Changed native evidence: " + method["method"])
    if not (core.ROOT / method["source"]).exists(): raise RuntimeError("Missing editable source")
with binary.open("rb") as stream:
    stream.seek(int(manifest["constant"]["fileOffset"], 16))
    if stream.read(4).hex() != manifest["constant"]["bytes"]: raise RuntimeError("Camera threshold changed")
print(f"Verified {len(manifest['methods'])} reconstruction evidence bodies and native camera threshold.")
