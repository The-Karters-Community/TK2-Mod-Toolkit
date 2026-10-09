"""Selective, reviewable source/settings packages. Import never builds or runs C#."""
from __future__ import annotations

import hashlib
import json
import math
import os
from pathlib import Path, PurePosixPath, PureWindowsPath
import re
import stat
import tempfile
import time
import uuid
import zipfile

from . import core, pack, recipe_catalog

FORMAT = "tk2mod"
VERSION = 1
MAX_FILES = 512
MAX_TOTAL = 384 * 1024 * 1024
MAX_ARCHIVE = 512 * 1024 * 1024
SOURCE_PREFIXES = ("plugins/TK2.Customization/", "src/Reconstructed/")
WARNINGS = ["Imported C# is editable source, not sandboxed code. Review it before building.",
            "Built-in modules share source files. Replacing shared code can affect other modules.",
            "Imported modules are disabled. Apply settings explicitly and build changed C# with the game closed."]


def _digest(data):
    return hashlib.sha256(data).hexdigest()


def _name(value):
    if not isinstance(value, str) or not value or "\\" in value or "\x00" in value:
        raise ValueError("Invalid package file path")
    path = PurePosixPath(value)
    if path.is_absolute() or PureWindowsPath(value).drive or str(path) != value:
        raise ValueError("Absolute or non-canonical package path: " + value)
    for part in path.parts:
        if part in (".", "..") or not part or part[-1:] in (".", " ") or any(ord(c) < 32 or c in ':<>"|?*' for c in part):
            raise ValueError("Unsafe package path: " + value)
        stem = part.split(".", 1)[0].upper()
        if stem in {"CON", "PRN", "AUX", "NUL"} or re.fullmatch(r"(?:COM|LPT)[1-9]", stem):
            raise ValueError("Reserved Windows filename: " + value)
    return value


def _kind(path):
    name = _name(path)
    if name.startswith(SOURCE_PREFIXES) and name.endswith(".cs"):
        if '/Models/' in name or PurePosixPath(name).stem in {identity.removeprefix('Recipe.') for identity in pack.RETIRED_MODULES}:
            raise ValueError("This package contains a removed module")
        if any(p.lower() in ("bin", "obj", ".git") for p in PurePosixPath(name).parts):
            raise ValueError("Generated or repository files cannot be imported")
        return "source"
    raise ValueError("Unsupported package file: " + name)


def _limit(name, kind):
    if kind == "source": return 2 * 1024 * 1024
    raise ValueError("Only C# source packages are supported")


def _managed(path, root):
    """Containment plus link/junction rejection, including existing ancestors."""
    root = Path(root).absolute()
    path = Path(path).absolute()
    if not path.is_relative_to(root) or path == root:
        raise ValueError("Path is outside the managed package directory")
    workspace = Path(core.ROOT).absolute()
    if root.is_relative_to(workspace):
        for ancestor in (root, *root.parents):
            if not ancestor.is_relative_to(workspace): break
            if ancestor.exists() and (ancestor.is_symlink() or getattr(ancestor, "is_junction", lambda: False)()):
                raise ValueError("Linked workspace package directories are not supported")
    for item in (root, *path.relative_to(root).parents):
        check = item if item == root else root / item
        if check.exists() and (check.is_symlink() or getattr(check, "is_junction", lambda: False)()):
            raise ValueError("Linked package directories are not supported")
    if path.exists() and (path.is_symlink() or getattr(path, "is_junction", lambda: False)()):
        raise ValueError("Linked package files are not supported")
    return core.contained(root, path)


def _schema(feature):
    result = {"Enabled": {"kind": "bool", "default": False}}
    for setting in feature.get("settings", []):
        key, _, kind, default, low, high = setting[:6]
        result[key] = {"kind": kind, "default": default, "low": low, "high": high,
                       "choices": setting[7] if len(setting) > 7 else None,
                       "allowCustom": setting[9] if len(setting) > 9 else False}
    return result


def _setting(value, definition):
    kind = definition.get("kind")
    if kind == "bool":
        if not isinstance(value, bool): raise ValueError("Expected a boolean package setting")
    elif kind in ("int", "float"):
        if isinstance(value, bool) or not isinstance(value, (float, int)) or not math.isfinite(value):
            raise ValueError("Expected a finite numeric package setting")
        low, high = definition.get("low"), definition.get("high")
        if not isinstance(low, (int, float)) or not isinstance(high, (int, float)) or not low <= value <= high:
            raise ValueError("Package setting is outside its allowed range")
        if kind == "int" and int(value) != value: raise ValueError("Expected an integer package setting")
    elif kind == "text":
        if not isinstance(value, str) or len(value) > 4096 or any(c in value for c in "\r\n\x00"):
            raise ValueError("Expected a single-line package setting")
        choices = definition.get("choices")
        if choices and not definition.get("allowCustom") and value not in choices:
            raise ValueError("Package setting is not an allowed choice")
    else: raise ValueError("Unsupported package setting type")
    return value


def _selected(ids):
    if not isinstance(ids, (list, tuple)) or not ids or len(ids) > 100 or any(not isinstance(i, str) for i in ids):
        raise ValueError("Select at least one module")
    if len(set(ids)) != len(ids): raise ValueError("Duplicate selected modules")
    available = {f["id"]: f for f in pack.catalog_features()}
    if any(i not in available for i in ids): raise ValueError("Unknown selected module")
    return [available[i] for i in ids]


def _source_dependencies(features):
    sources = set()
    owners = pack.module_sources()
    for feature in features:
        sources.update(source for source in owners.get(feature["id"], [])
                       if not (feature["id"].startswith("Recipe.") and source.endswith("/RecipeHost.cs")))
    if any(f["id"] == "Physics" for f in features):
        sources.add("plugins/TK2.Customization/ReadableGame.cs")
        sources.update(s for s in pack.source_files() if s.startswith("src/Reconstructed/"))
    # Local custom recipes may use additional helper source files. Resolve declared
    # local type names transitively; never follow paths requested by imported code.
    available = {s: pack.source_path(s).read_text(encoding="utf-8-sig") for s in pack.source_files()}
    declarations = {}
    framework = {"Plugin", "RecipeHost", "IModRecipe"}
    for path, text in available.items():
        # Nested State/Entry names are common across independent modules and do
        # not represent cross-file dependencies. SDK host types are prerequisites.
        for name in re.findall(r"^(?:public|internal)\s+(?:(?:sealed|static|abstract|partial|readonly)\s+)*(?:class|struct|interface|enum)\s+(\w+)", text, re.MULTILINE):
            if name not in framework: declarations.setdefault(name, set()).add(path)
    queue = list(sources)
    while queue:
        source = queue.pop()
        if source not in available: raise ValueError("Missing module dependency: " + source)
        for token in set(re.findall(r"\b[A-Za-z_]\w*\b", available[source])):
            for dependency in declarations.get(token, ()):
                if dependency not in sources:
                    sources.add(dependency); queue.append(dependency)
    return sorted(sources)


def export_package(ids, name, values, game=None):
    features = _selected(ids)
    if not isinstance(name, str) or not 1 <= len(name.strip()) <= 80 or any(ord(c) < 32 for c in name):
        raise ValueError("Use a package name from 1 to 80 characters")
    if not isinstance(values, dict): raise ValueError("Expected selected setting values")
    selected_values, modules, content = {}, [], {}
    for feature in features:
        definitions = _schema(feature)
        for key, definition in definitions.items():
            compound = feature["id"] + "/" + key
            selected_values[compound] = _setting(values.get(compound, definition["default"]), definition)
        modules.append({"id": feature["id"], "name": feature["name"], "gameplay": bool(feature.get("gameplay")), "schema": definitions})
    for source in _source_dependencies(features):
        _kind(source)
        content[source] = _managed(core.ROOT / source, core.ROOT).read_bytes()
    if len(content) + 1 > MAX_FILES or sum(map(len, content.values())) > MAX_TOTAL:
        raise ValueError("Selected package exceeds file or total size limits")
    entries = []
    for path, data in sorted(content.items()):
        kind = _kind(path)
        if len(data) > _limit(path, kind): raise ValueError("Package source exceeds size limits")
        entries.append({"path": path, "kind": kind, "sha256": _digest(data), "size": len(data)})
    identity = uuid.uuid4().hex
    manifest = {"format": FORMAT, "version": VERSION, "name": name.strip(), "modules": modules,
                "settings": selected_values, "files": entries,
                "requires": {"toolkit": ">=0.6.3", "runtime": "TK2.Customization", "runtimeVersion": ">=0.5.1", "sourceProject": "TK2-Mod-SDK", "unity": "IL2CPP", "buildAfterSourceImport": True}}
    directory = core.ROOT / "local/module-exports"
    directory.mkdir(parents=True, exist_ok=True)
    destination = _managed(directory / (identity + ".tk2mod"), directory)
    with tempfile.NamedTemporaryFile(dir=directory, delete=False) as temporary: temporary_path = Path(temporary.name)
    try:
        with zipfile.ZipFile(temporary_path, "w", zipfile.ZIP_DEFLATED) as archive:
            archive.writestr("manifest.json", json.dumps(manifest, indent=2, allow_nan=False))
            for path, data in content.items(): archive.writestr(path, data)
        os.replace(temporary_path, destination)
    finally:
        temporary_path.unlink(missing_ok=True)
    slug = re.sub(r"[^A-Za-z0-9._-]+", "-", name.strip()).strip(".-") or "modules"
    return {"id": identity, "filename": slug + ".tk2mod", "modules": [m["id"] for m in modules],
            "files": entries, "sha256": core.sha256(destination), "warnings": WARNINGS}


def download_path(id):
    if not isinstance(id, str) or not re.fullmatch(r"[0-9a-f]{32}", id): raise ValueError("Invalid package download")
    directory = core.ROOT / "local/module-exports"
    path = _managed(directory / (id + ".tk2mod"), directory)
    if not path.is_file(): raise ValueError("Package download is no longer available")
    return path


def _json(data):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result: raise ValueError("Duplicate manifest key: " + key)
            result[key] = value
        return result
    try: return json.loads(data, object_pairs_hook=unique, parse_constant=lambda _: (_ for _ in ()).throw(ValueError("Non-finite manifest value")))
    except (UnicodeDecodeError, json.JSONDecodeError) as error: raise ValueError("Invalid package manifest") from error


def _read(path):
    path = Path(path).expanduser().resolve()
    if not path.is_file() or path.suffix.lower() != ".tk2mod" or path.stat().st_size > MAX_ARCHIVE:
        raise ValueError("Choose a .tk2mod package within the supported size limit")
    # Bind preview to the exact bytes later imported; read once to avoid a changed
    # file between manifest validation and extraction.
    data = path.read_bytes()
    import io
    try:
        archive = zipfile.ZipFile(io.BytesIO(data))
    except zipfile.BadZipFile as error: raise ValueError("Invalid mod package ZIP") from error
    content = {}
    with archive:
        entries = archive.infolist()
        if not entries or len(entries) > MAX_FILES: raise ValueError("Package has too many files")
        prefixes, total = {}, 0
        for entry in entries:
            name = _name(entry.filename)
            if entry.is_dir(): raise ValueError("Package uses unsupported directory entries")
            mode = entry.external_attr >> 16
            if stat.S_IFMT(mode) not in (0, stat.S_IFREG) or entry.flag_bits & 1:
                raise ValueError("Links, special files and encrypted package entries are unsupported")
            for index in range(1, len(PurePosixPath(name).parts) + 1):
                prefix = "/".join(PurePosixPath(name).parts[:index])
                folded = prefix.casefold()
                if folded in prefixes and prefixes[folded] != prefix: raise ValueError("Package has case-colliding paths")
                prefixes[folded] = prefix
            if name in content: raise ValueError("Duplicate package file: " + name)
            kind = "manifest" if name == "manifest.json" else _kind(name)
            limit = 1024 * 1024 if kind == "manifest" else _limit(name, kind)
            total += entry.file_size
            if entry.file_size > limit or total > MAX_TOTAL or entry.file_size > max(1024 * 1024, entry.compress_size * 200):
                raise ValueError("Package expands beyond supported size limits")
            try: content[name] = archive.read(entry)
            except (zipfile.BadZipFile, RuntimeError, NotImplementedError) as error: raise ValueError("Unreadable package entry") from error
        if "manifest.json" not in content: raise ValueError("Package manifest is missing")
    manifest = _json(content.pop("manifest.json"))
    if not isinstance(manifest, dict) or manifest.get("format") != FORMAT or manifest.get("version") != VERSION:
        raise ValueError("Unsupported mod package format/version")
    modules, files, settings = manifest.get("modules"), manifest.get("files"), manifest.get("settings")
    if not isinstance(modules, list) or not 1 <= len(modules) <= 100 or not isinstance(files, list) or not isinstance(settings, dict):
        raise ValueError("Invalid package modules, files or settings")
    identities = []
    known = {f["id"]: f for f in pack.catalog_features()}
    for module in modules:
        if not isinstance(module, dict) or not isinstance(module.get("id"), str) or not re.fullmatch(r"[A-Za-z][A-Za-z0-9_.]{1,79}", module["id"]):
            raise ValueError("Invalid package module identity")
        identity = module["id"]
        if identity in identities: raise ValueError("Duplicate package module")
        if identity not in known and not identity.startswith("Recipe."): raise ValueError("Unknown built-in package module")
        identities.append(identity)
        if not isinstance(module.get("schema"), dict): raise ValueError("Missing module settings schema")
    declared = set()
    for entry in files:
        if not isinstance(entry, dict) or not isinstance(entry.get("path"), str): raise ValueError("Invalid manifest file")
        name = _name(entry["path"])
        if name in declared or name not in content or entry.get("kind") != _kind(name): raise ValueError("Manifest file list mismatch")
        if entry.get("size") != len(content[name]) or entry.get("sha256") != _digest(content[name]): raise ValueError("Package file checksum mismatch")
        declared.add(name)
    if declared != set(content): raise ValueError("Undeclared files in mod package")
    for name, source in content.items():
        if _kind(name) == "source":
            try:
                if "\x00" in source.decode("utf-8-sig"): raise ValueError("C# source contains binary data")
            except UnicodeDecodeError as error: raise ValueError("C# source must use UTF-8") from error
    # New recipe settings are verified against literal bindings in their source,
    # not solely against a schema supplied by an untrusted manifest.
    with tempfile.TemporaryDirectory(prefix="tk2mod-review-") as temporary:
        stage = Path(temporary)
        for name, source in content.items():
            target = stage / name; target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(source)
        parsed = {f["id"]: f for f in recipe_catalog.source_features(stage / "plugins/TK2.Customization")}
    definitions = {}
    for module in modules:
        identity = module["id"]
        if identity.startswith("Recipe."):
            if identity not in parsed: raise ValueError("Recipe module does not match any packaged source")
            definition = _schema(parsed[identity])
        else: definition = _schema(known[identity])
        # The independent current catalog (or parsed recipe) defines allowed keys,
        # defaults and bounds. Manifest metadata is descriptive only.
        for key, value in definition.items(): definitions[identity + "/" + key] = value
    for compound, value in settings.items():
        if compound not in definitions: raise ValueError("Package contains unselected or unsupported settings: " + compound)
        _setting(value, definitions[compound])
    imported = {compound: settings.get(compound, definition["default"]) for compound, definition in definitions.items()}
    for identity in identities: imported[identity + "/Enabled"] = False
    return path, _digest(data), manifest, content, imported


def _destination(name, identity):
    return _managed(core.ROOT / name, core.ROOT)


def preview_import(path):
    path, digest, manifest, content, settings = _read(path)
    files, conflicts = [], []
    for name, data in sorted(content.items()):
        target = _destination(name, digest[:32])
        changed = target.is_file() and core.sha256(target) != _digest(data)
        if target.exists() and not target.is_file(): raise ValueError("Import target is not a file")
        if changed: conflicts.append(name)
        files.append({"path": name, "kind": _kind(name), "size": len(data), "sha256": _digest(data),
                      "status": "replace" if changed else "unchanged" if target.is_file() else "new"})
    return {"path": str(path), "hash": digest, "name": manifest.get("name", "Imported modules"),
            "modules": manifest["modules"], "files": files, "conflicts": conflicts,
            "settings": settings, "warnings": WARNINGS, "requiresBuild": any(f["kind"] == "source" and f["status"] != "unchanged" for f in files)}


def import_package(path, hash, replace=False):
    path, digest, manifest, content, settings = _read(path)
    if not isinstance(hash, str) or digest != hash: raise ValueError("Package changed after preview; preview it again")
    if not isinstance(replace, bool): raise ValueError("Expected an explicit replace option")
    targets = [(name, data, _destination(name, digest[:32])) for name, data in sorted(content.items())]
    conflicts = [name for name, data, target in targets if target.is_file() and core.sha256(target) != _digest(data)]
    if conflicts and not replace: raise ValueError("Existing code differs; review and explicitly allow replacement: " + ", ".join(conflicts))
    for _, _, target in targets:
        if target.exists() and not target.is_file(): raise ValueError("Import target is not a file")
    backup = core.ROOT / "local/source-backups" / (str(time.time_ns()) + "-package")
    originals, written = {}, []
    try:
        # Capture all originals before any source mutation; rollback on an I/O
        # failure leaves a recoverable backup and restores previous source bytes.
        for name, data, target in targets:
            if target.is_file() and core.sha256(target) == _digest(data): continue
            originals[name] = target.read_bytes() if target.is_file() else None
            if originals[name] is not None:
                saved = _managed(backup / name, backup); core.atomic_write(saved, originals[name])
        for name, data, target in targets:
            if name not in originals: continue
            core.atomic_write(target, data); written.append((name, target))
    except Exception:
        for name, target in reversed(written):
            if originals[name] is None: target.unlink(missing_ok=True)
            else: core.atomic_write(target, originals[name])
        raise
    return {"settings": settings, "modules": [m["id"] for m in manifest["modules"]],
            "files": [name for name, _, _ in targets],
            "backup": str(backup) if conflicts else None, "warnings": WARNINGS,
            "requiresBuild": any(_kind(name) == "source" for name, _ in written),
            "message": "Imported selected source and settings. Modules remain disabled; review code, apply settings, and build changed C# explicitly."}
