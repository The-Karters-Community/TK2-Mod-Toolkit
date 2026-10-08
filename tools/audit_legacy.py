"""Read-only audit of named legacy Harmony targets against the local current dump."""
import collections
import json
from pathlib import Path
import re
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from studio import core

catalog = json.loads((core.ROOT / "local/catalog.json").read_text())
declarations = collections.defaultdict(list)
for entry in catalog["types"]: declarations[entry["name"]].append(entry)
sources = [core.ROOT.parent / name for name in ("TKMA-New-Mod-Template", "The-Karters-Modding-Assistant-SDK", "TheKarters2Mods")]
sources.append(core.ROOT / "local/managed")
pattern = re.compile(r'\[HarmonyPatch\(typeof\(([^)]+)\),\s*(?:nameof\([^)]*\.([A-Za-z0-9_]+)\)|"([A-Za-z0-9_]+)")')
records = []
file_counts = {}
for source in sources:
    files = sorted(source.rglob("*.cs"))
    file_counts[source.name] = len(files)
    for path in files:
        text = path.read_text(encoding="utf-8-sig")
        for match in pattern.finditer(text):
            name, from_nameof, literal = match.groups()
            method = from_nameof or literal
            imports = set(re.findall(r"^using ([A-Za-z0-9_.]+);", text, re.MULTILINE))
            candidates = [e for e in declarations.get(name, []) if not e["namespace"] or e["namespace"] in imports]
            entry = candidates[0] if len(candidates) == 1 else None
            signatures = [] if entry is None else [l.strip() for l in core.read_declaration(catalog, entry).splitlines()
                if re.search(r"\b" + re.escape(method) + r"\s*\(", l)]
            status = "ambiguous type" if len(candidates) > 1 else "type absent" if entry is None else "method absent" if not signatures else "overload review" if len(signatures) > 1 else "named target present"
            records.append({"file": str(path.relative_to(source)), "source": source.name, "line": text[:match.start()].count("\n") + 1,
                            "type": name, "method": method, "status": status, "signatures": signatures})
counts = collections.Counter(r["status"] for r in records)
core.write_json(core.ROOT / "local/legacy-audit.json", {"files": file_counts, "counts": counts, "targets": records})
lines = ["# Legacy Harmony target audit", "", "Generated with `python tools/audit_legacy.py` against the indexed current dump.", "",
         "This is a static name/signature audit. A present target does not establish compatible fields, hook semantics, native hookability, or working runtime behavior. Duplicated targets are counted separately because they occur in different modules. Current declarations cannot reveal a legacy binary's compile-time overload requirements.", "", "| Source | C# files scanned |", "|---|---:|"]
lines += [f"| {name} | {count} |" for name, count in file_counts.items()]
lines += ["", "| Result | Patch declarations |", "|---|---:|"]
lines += [f"| {name} | {count} |" for name, count in sorted(counts.items())]
lines += ["", "| Source file and line | Target | Static result |", "|---|---|---|"]
lines += [f"| {r['source']}/{r['file']}:{r['line']} | `{r['type']}.{r['method']}` | {r['status']} |" for r in records]
lines += ["", "## Migration notes", "", "- `HpBarController.Hit` has both three- and four-argument overloads in current metadata. Specify the intended types; do not pick the first reflection result.",
          "- `PixelKartPhysics.JumpInputTheKarters` is absent; `JumpInput(bool)` exists but is not assumed to be a drop-in replacement. Its native body includes input buffering, replay grounding and coyote-time logic.",
          "- The Twitch SDK carries live network credentials in config in old examples; keep generated config/backups local. The new studio does not initialize external accounts.",
          "- Old TKMA hooks dereference kartController before testing the instance; correct null/lifecycle ordering when porting. Event subscriptions also need explicit disposal when unloading.",
          "- The old config reloader and wrappers remain independent dependencies. The starter plugin implements its own main-thread reload and has no dependency on those SDK DLLs."]
(core.ROOT / "docs/LEGACY-AUDIT.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
print(json.dumps({"files": file_counts, "counts": counts, "targets": len(records)}, indent=2))
