"""Merge edited settings by value, preserving BepInEx metadata and unrelated edits."""
import hashlib
import math
import re
from . import core, pack


class SettingsConflict(ValueError):
    def __init__(self, keys):
        self.keys = keys
        super().__init__("These settings were also changed elsewhere: " + ", ".join(keys) + ". Reload those values before saving.")


def normalized(value, kind):
    if kind in ("bool", "Boolean"):
        return str(value).strip().lower() in ("true", "1")
    if kind in ("float", "int", "Single", "Double", "Decimal", "Int32", "Int64", "UInt32", "UInt64", "Int16", "UInt16", "Byte", "SByte"):
        try:
            number = float(value)
            return number if math.isfinite(number) else str(value)
        except (ValueError, TypeError): return str(value)
    return str(value)


def read(data):
    text = data.decode("utf-8-sig")
    entries = core.parse_cfg_settings(text)
    values = pack.defaults()
    known = pack.schema()
    for entry in entries:
        compound = entry["section"] + "/" + entry["key"]
        schema = known.get((entry["section"], entry["key"]))
        if schema:
            values[compound] = normalized(entry["value"], schema[0])
    for section, key in pack.FORCED_TRUE_SETTINGS:
        values[f"{section}/{key}"] = True
    return {"settings": values, "configHash": hashlib.sha256(data).hexdigest(),
            "recipes": [e for e in entries if e["section"].startswith("Recipe.") and e["section"] not in pack.RETIRED_MODULES and (e["section"], e["key"]) not in known and (e["section"], e["key"]) not in pack.RETIRED_SETTINGS],
            "extraSettings": [e for e in entries if (e["section"], e["key"]) not in known and not e["section"].startswith("Recipe.") and e["section"] not in pack.RETIRED_MODULES],
            "entries": entries}


def merge(data, body):
    current = read(data)
    dirty = body.get("values", {})
    recipe_dirty = body.get("recipes", {})
    extra_dirty = body.get("extraSettings", {})
    if not all(isinstance(v, dict) for v in (dirty, recipe_dirty, extra_dirty)):
        raise ValueError("Expected edited setting objects")
    # Older clients without value baselines may only save against identical bytes.
    if body.get("hash") != current["configHash"] and not any(k in body for k in ("baseValues", "baseRecipes", "baseExtraSettings")):
        raise SettingsConflict(list(dirty) + list(recipe_dirty) + list(extra_dirty))
    updates = pack.validate(dirty)
    for (section, key), value in updates.items():
        if (section, key) in pack.FORCED_TRUE_SETTINGS and normalized(value, "bool") is not True:
            raise ValueError(f"{section}/{key} is mandatory and cannot be disabled")
    definitions = {e["section"] + "/" + e["key"]: e for e in current["entries"]
                   if (e["section"], e["key"]) not in pack.RETIRED_SETTINGS}
    kinds = {s + "/" + k: definition[0] for (s, k), definition in pack.schema().items()}
    current_values = dict(current["settings"])
    for compound, entry in definitions.items():
        kinds.setdefault(compound, entry["type"])
        current_values.setdefault(compound, normalized(entry["value"], entry["type"]))
    for collection in (recipe_dirty, extra_dirty):
        for compound, value in collection.items():
            if compound.partition('/')[0] in pack.RETIRED_MODULES:
                raise ValueError('This module has been removed: ' + compound)
            if compound not in definitions or compound in current["settings"]:
                raise ValueError("Unknown additional setting: " + compound)
            entry = definitions[compound]
            updates[(entry["section"], entry["key"])] = core.validate_cfg_value(entry, str(value).lower() if isinstance(value, bool) else str(value))
    baselines = {**body.get("baseValues", {}), **body.get("baseRecipes", {}), **body.get("baseExtraSettings", {})}
    conflicts = []
    for (section, key), requested in updates.items():
        compound = section + "/" + key
        actual = normalized(current_values.get(compound), kinds[compound])
        desired = normalized(requested, kinds[compound])
        if compound in baselines:
            baseline = normalized(baselines[compound], kinds[compound])
            if actual != baseline and actual != desired: conflicts.append(compound)
        elif body.get("hash") != current["configHash"]:
            conflicts.append(compound)
    if conflicts: raise SettingsConflict(conflicts)
    # The toolkit always restores these settings even if a config file was edited
    # outside the app while it was open.
    updates.update({pair: "true" for pair in pack.FORCED_TRUE_SETTINGS})
    updated = core.update_cfg(data.decode("utf-8-sig") or "# TK2 Mod Toolkit Pack\n", updates)
    # Old module sections must not reappear in the game's config after a save.
    output, section = [], ""
    for line in updated.splitlines():
        header = re.match(r"^\s*\[([^]]+)\]\s*$", line)
        if header:
            section = header[1]
            if section in pack.RETIRED_MODULES:
                continue
        if section not in pack.RETIRED_MODULES:
            output.append(line)
    newline = "\r\n" if "\r\n" in updated else "\n"
    return (newline.join(output) + newline).encode("utf-8")
