"""Readable method catalog. Declarations and recovered implementations stay distinct."""
import json
from pathlib import Path
import re
import threading
from . import core

_lock = threading.Lock()
_cache = None
_stamp = None


def methods():
    global _cache, _stamp
    catalog_path = core.ROOT / "local/catalog.json"
    if not catalog_path.is_file():
        exported = core.ROOT / "exports/functions.json"
        if not exported.is_file(): return []
        stamp = (str(exported), exported.stat().st_mtime_ns)
        if _cache is None or _stamp != stamp:
            with _lock: _cache, _stamp = json.loads(exported.read_text(encoding="utf-8")), stamp
        return _cache
    stamp = (str(catalog_path), catalog_path.stat().st_mtime_ns)
    if _cache is not None and stamp == _stamp: return _cache
    with _lock:
        catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
        if not Path(catalog["source"]).is_file(): return []
        provenance = core.ROOT / "src/Reconstructed/provenance.json"
        recovered = {m["address"].lower(): m for m in json.loads(provenance.read_text())["methods"]} if provenance.is_file() else {}
        result = []
        for entry in catalog["types"]:
            if entry.get("assembly") != "Assembly-CSharp.dll": continue
            address = None; ordinal = 0
            for line in core.read_declaration(catalog, entry).splitlines():
                match = re.search(r'\bVA: 0x([a-fA-F0-9]+)', line)
                if match: address = match[1].lower()
                if not line.startswith("\t") or not re.search(r'\)\s*(?:\{\s*\}|;)\s*$', line): continue
                signature = re.sub(r'\s*\{\s*\}.*$', ';', line.strip())
                record = recovered.get(address, {})
                method_name = signature.split("(", 1)[0].split()[-1]
                if record.get("method") != entry["name"] + "." + method_name: record = {}
                constructor = entry["name"].split(".")[-1].split("<", 1)[0]
                signature = signature.replace("void .ctor", constructor)
                if ".cctor" in signature: signature = "static " + constructor + "();"
                result.append({"id": f"{entry['index']}:{ordinal}", "type": entry["name"], "namespace": entry["namespace"],
                    "signature": signature, "address": address, "source": record.get("source"), "confidence": record.get("confidence"),
                    "evidence": record.get("evidence")})
                ordinal += 1; address = None
        _cache, _stamp = result, stamp
        return result


TOPICS = {
    "camera": ("Camera", r"^(PixelGameKartCamera|PixelSDK_Camera)$"),
    "driving": ("Kart & driving", r"^(PixelKartPhysics|PixelKartController|Ant_Player)$"),
    "boost": ("Boost & items", r"^(Ant_BoostManager|PixelWeaponObject|Ant_WeaponsManager)$"),
    "health": ("Health", r"^(HpBarController|PlayerGlobalStats)$"),
    "interface": ("Interface & audio", r"^(Ant_.*(?:Audio|Sound|Hud|HUD|UI).*|.*(?:AudioManager|SoundManager|Wwise|HUDController).*)$"),
    "race": ("Race & input", r"^(Ant_CurrentGameConfiguration|Ant_Race.*|.*(?:RaceManager|InputManager|InputMapping).*)$"),
}


def presentation(record):
    signature = record["signature"]
    match = re.search(r'(\S+)\s*\((.*)\)', signature)
    name = match[1] if match else signature
    return {**record, "name": name, "label": re.sub(r'(?<=[a-z0-9])(?=[A-Z])', ' ', name).replace('_', ' '),
            "topic": next((title for title, pattern in TOPICS.values() if re.match(pattern, record["type"])), "Other game functions"),
            "access": "private" if signature.startswith("private ") else "internal" if signature.startswith("internal ") else "public" if signature.startswith("public ") else "unspecified"}


def search(query="", offset=0, recovered=False, topic="all"):
    if topic not in (*TOPICS, "important", "all"): raise ValueError("Choose a listed API category")
    entries = methods()
    tokens = query.lower().split()
    def included(m):
        if topic == "all": return True
        if '<' in m["type"] or m["signature"].startswith("static ") and ".cctor" in m["signature"]: return False
        patterns = [TOPICS[topic][1]] if topic in TOPICS else [pattern for _, pattern in TOPICS.values()]
        return any(re.match(pattern, m["type"]) for pattern in patterns)
    selected = [m for m in entries if included(m) and (not recovered or m["source"]) and all(t in (m["type"] + " " + m["namespace"] + " " + m["signature"]).lower() for t in tokens)]
    if topic != "all": selected.sort(key=lambda m: (not bool(m["source"]), m["type"], m["signature"]))
    offset = max(0, offset)
    return {"total": len(selected), "indexed": len(entries), "reconstructed": sum(bool(m["source"]) for m in entries),
        "methods": [presentation(m) for m in selected[offset:offset + 80]], "offset": offset, "next": offset + 80 if offset + 80 < len(selected) else None,
        "message": "No local Il2CppDumper declarations indexed. Player controls still work; authors can import a dump with the analysis tools." if not entries else ""}


def detail(identity):
    record = next((m for m in methods() if m["id"] == identity), None)
    if record is None: raise ValueError("Choose an indexed function")
    return {**presentation(record), "declaration": record['signature'],
            "status": "Editable reconstructed C# available" if record["source"] else "Declaration indexed; native implementation not reconstructed"}
