"""Filesystem and build operations shared by the CLI and GUI. No game execution."""
from __future__ import annotations

import hashlib
import bisect
import json
import math
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess
import tempfile
import time
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_GAME = Path(r"C:\Program Files (x86)\Steam\steamapps\common\The Karters 2 Turbo Charged")
PLUGIN_NAME = "TK2.Customization"
CONFIG_NAME = "local.tk2.customization.cfg"
CREATE_NO_WINDOW = getattr(subprocess, "CREATE_NO_WINDOW", 0)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def contained(root: Path, target: Path) -> Path:
    base, child = root.resolve(), target.resolve()
    if child == base or not child.is_relative_to(base):
        raise ValueError(f"Path is outside the managed directory: {target}")
    # Resolving an existing junction/symlink also catches redirection outside root.
    return child


def atomic_write(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, name = tempfile.mkstemp(prefix=".tk2-", dir=path.parent)
    try:
        with os.fdopen(fd, "wb") as stream:
            stream.write(data)
        os.replace(name, path)
    finally:
        if os.path.exists(name):
            os.unlink(name)


def write_json(path: Path, data) -> None:
    atomic_write(path, json.dumps(data, indent=2, ensure_ascii=False).encode("utf-8"))


def validate_game(game: Path) -> Path:
    game = game.resolve()
    for name in ("TheKarters2.exe", "GameAssembly.dll"):
        if not (game / name).is_file():
            raise ValueError(f"Not a TK2 installation: missing {name}")
    return game


def game_running() -> bool:
    if os.name != "nt":
        return False
    result = subprocess.run(["tasklist", "/FI", "IMAGENAME eq TheKarters2.exe", "/FO", "CSV", "/NH"],
                            capture_output=True, text=True, creationflags=CREATE_NO_WINDOW, check=True)
    return '"thekarters2.exe"' in result.stdout.lower()


def require_game_stopped() -> None:
    if game_running():
        raise ValueError("Close The Karters 2 before changing DLLs or restoring files. Config settings can reload live.")


def fingerprint(game: Path) -> dict:
    game = validate_game(game)
    names = ["GameAssembly.dll", "UnityPlayer.dll", "TheKarters2_Data/il2cpp_data/Metadata/global-metadata.dat",
             "BepInEx/interop/Assembly-CSharp.dll", "BepInEx/core/BepInEx.Unity.IL2CPP.dll",
             "BepInEx/core/Il2CppInterop.Runtime.dll"]
    return {"game": str(game), "buildInfo": (game / "build-info.txt").read_text(encoding="utf-8")
            if (game / "build-info.txt").exists() else "unknown",
            "files": {name: sha256(game / name) for name in names if (game / name).is_file()}}


def diagnose(game: Path) -> dict:
    info = fingerprint(game)
    log = next((game / "BepInEx" / n for n in ("LogOutput.log", "LogOutput.txt")
                if (game / "BepInEx" / n).exists()), None)
    text = log.read_text(encoding="utf-8", errors="replace") if log else ""
    info.update({"loaderInstalled": (game / "winhttp.dll").is_file(),
                 "interopPresent": (game / "BepInEx/interop/Assembly-CSharp.dll").is_file(),
                 "logPath": str(log) if log else None,
                 "logModified": log.stat().st_mtime if log else None,
                 "observations": [line for line in text.splitlines() if any(
                     word in line for word in ("Error loading", "Ambiguous", "Loading [", "Unity ", "Runtime version", "exhausted"))]})
    return info


def index_dump(path: Path, output: Path) -> dict:
    """Index declaration byte spans; bodies in dump.cs are stubs, not implementations."""
    types, images, namespace, pending_start, current = [], [], "", None, None
    type_pattern = re.compile(r"^(?:public|private|internal|protected).*?\b(class|struct|enum|interface)\s+(.+?)\s*// TypeDefIndex: (\d+)")
    offset, line_number = 0, 0
    with path.open("rb") as stream:
        for raw in stream:
            line_number += 1
            line = raw.decode("utf-8", errors="replace").rstrip()
            image = re.match(r"// Image \d+: (.+) - (\d+)$", line)
            if image: images.append((int(image[2]), image[1]))
            if line.startswith("// Namespace:"):
                if current:
                    current["end"] = offset
                    types.append(current)
                    current = None
                namespace = line.partition(":")[2].strip()
                pending_start = offset
            match = type_pattern.match(line)
            if match:
                kind, name, index = match.groups()
                name = name.split(" : ", 1)[0].strip()
                current = {"name": name, "namespace": namespace, "kind": kind, "index": int(index),
                           "start": pending_start if pending_start is not None else offset,
                           "line": line_number, "declaration": line, "members": 0}
                current["assembly"] = images[bisect.bisect_right([i[0] for i in images], int(index)) - 1][1] if images else "unknown"
            elif current and line.startswith("\t") and (";" in line or " {" in line):
                current["members"] += 1
            offset += len(raw)
    if current:
        current["end"] = offset
        types.append(current)
    data = {"source": str(path.resolve()), "sha256": sha256(path), "types": types,
            "typeCount": len(types), "kind": "IL2CPP metadata declarations (no implementation bodies)"}
    write_json(output, data)
    return data


def read_declaration(catalog: dict, entry: dict) -> str:
    with Path(catalog["source"]).open("rb") as stream:
        stream.seek(entry["start"])
        return stream.read(entry["end"] - entry["start"]).decode("utf-8", errors="replace")


def plugins(game: Path) -> list[dict]:
    base = game / "BepInEx/plugins"
    if not base.exists():
        return []
    return [{"path": str(p), "relative": str(p.relative_to(base)), "enabled": p.suffix.lower() == ".dll",
             "bytes": p.stat().st_size} for p in sorted(base.rglob("*")) if p.is_file()
            and (p.suffix.lower() == ".dll" or p.name.lower().endswith(".dll.disabled"))]


def toggle_plugin(game: Path, path: Path) -> Path:
    validate_game(game)
    require_game_stopped()
    path = contained(game / "BepInEx/plugins", path)
    if path.name.endswith(".dll.disabled"):
        target = path.with_name(path.name[:-9])
    elif path.suffix.lower() == ".dll":
        target = path.with_name(path.name + ".disabled")
    else:
        raise ValueError("Select a DLL or .dll.disabled file")
    contained(game / "BepInEx/plugins", target)
    if target.exists():
        raise ValueError(f"Both enabled and disabled copies exist: {target}")
    path.rename(target)
    return target


def backup_write(game: Path, path: Path, content: bytes, backup_root: Path | None = None) -> Path:
    path = contained(validate_game(game), path)
    backup_root = backup_root or ROOT / "local/backups"
    transaction = backup_root / f"{time.time_ns()}"
    transaction.mkdir(parents=True)
    old = path.read_bytes() if path.exists() else None
    if old is not None:
        atomic_write(transaction / "previous", old)
    receipt = {"game": str(game.resolve()), "relative": str(path.relative_to(game.resolve())),
               "existed": old is not None, "before": hashlib.sha256(old).hexdigest() if old is not None else None,
               "after": hashlib.sha256(content).hexdigest()}
    write_json(transaction / "receipt.json", receipt)
    atomic_write(path, content)
    return transaction


def restore_backup(game: Path, transaction: Path, backup_root: Path | None = None) -> Path:
    require_game_stopped()
    transaction = contained(backup_root or ROOT / "local/backups", transaction)
    receipt = json.loads((transaction / "receipt.json").read_text())
    if str(validate_game(game)) != receipt["game"]:
        raise ValueError("Backup belongs to another game installation")
    target = contained(game, game / receipt["relative"])
    if not target.is_file() or sha256(target) != receipt["after"]:
        raise ValueError("File changed since this backup. Restore rejected to preserve newer edits.")
    if receipt["existed"]:
        previous = (transaction / "previous").read_bytes()
        if hashlib.sha256(previous).hexdigest() != receipt["before"]:
            raise ValueError("Backup content failed integrity check")
        atomic_write(target, previous)
    else:
        target.unlink()
    return target


def update_cfg(text: str, values: dict[tuple[str, str], str]) -> str:
    """Preserve unknown keys, comments, order and newline style; replace only selected settings."""
    newline = "\r\n" if "\r\n" in text else "\n"
    lines, seen, section = [], set(), ""
    remaining = dict(values)
    def append_missing(name):
        for (sect, key), value in values.items():
            if sect == name and (sect, key) in remaining:
                lines.append(f"{key} = {value}")
                remaining.pop((sect, key))
    for line in text.splitlines():
        match = re.match(r"^\s*\[([^]]+)\]\s*$", line)
        if match:
            append_missing(section)
            section = match[1]
            seen.add(section)
        setting = re.match(r"^(\s*)([^#;=]+?)(\s*=\s*)(.*)$", line)
        if setting and (section, setting[2].strip()) in values:
            key = (section, setting[2].strip())
            line = setting[1] + setting[2] + setting[3] + values[key]
            remaining.pop(key, None)
        lines.append(line)
    append_missing(section)
    for sect in dict.fromkeys(k[0] for k in remaining):
        lines.extend(["", f"[{sect}]"])
        append_missing(sect)
    return newline.join(lines) + newline


SETTING_SCHEMA = {
    ("UI", "Enabled"): ("bool", False, None, None),
    ("UI", "HudScale"): ("float", 1.0, 0.5, 2.0),
    ("UI", "CanvasNameFilter"): ("text", "HUD", None, None),
    ("Audio", "Enabled"): ("bool", False, None, None),
    ("Audio", "MasterVolume"): ("float", 1.0, 0.0, 1.0),
    ("Camera", "Enabled"): ("bool", False, None, None),
    ("Camera", "FieldOfView"): ("float", 65.0, 35.0, 110.0),
    ("Physics", "Enabled"): ("bool", False, None, None),
    ("Physics", "FastFallAcceleration"): ("float", 100.0, 0.0, 500.0),
    ("Physics", "MinimumAirTime"): ("float", 0.4, 0.0, 3.0),
}


def parse_cfg_settings(text: str) -> list[dict]:
    """Read BepInEx's self-describing comments without discarding raw config content."""
    entries, section, metadata, descriptions = [], "", {}, []
    for line in text.splitlines():
        header = re.match(r"^\s*\[([^]]+)\]\s*$", line)
        if header:
            section, metadata, descriptions = header[1], {}, []
            continue
        comment = re.match(r"^# (Setting type|Default value|Acceptable values|Acceptable value range): (.*)$", line)
        if comment: metadata[comment[1]] = comment[2]
        elif line.startswith("## "): descriptions.append(line[3:])
        else:
            setting = re.match(r"^\s*([^#;=]+?)\s*=\s*(.*)$", line)
            if setting and section:
                entries.append({"section": section, "key": setting[1].strip(), "value": setting[2],
                                "type": metadata.get("Setting type", "String"), "default": metadata.get("Default value", ""),
                                "choices": [v.strip() for v in metadata.get("Acceptable values", "").split(",") if v.strip()],
                                "range": metadata.get("Acceptable value range", ""), "description": "\n".join(descriptions)})
                metadata, descriptions = {}, []
    return entries


def validate_cfg_value(entry: dict, value: str) -> str:
    if any(ch in value for ch in "\r\n"):
        raise ValueError("Use a single-line setting value")
    kind = entry["type"]
    if kind == "Boolean":
        if value.lower() not in ("true", "false"): raise ValueError("Expected true or false")
        value = value.lower()
    elif kind in ("Single", "Double", "Decimal", "Int32", "Int64", "UInt32", "UInt64", "Int16", "UInt16", "Byte", "SByte"):
        number = int(value) if kind not in ("Single", "Double", "Decimal") else float(value)
        if not math.isfinite(number): raise ValueError("Expected a finite number")
        integer_limits = {"Int32": (-2**31, 2**31-1), "Int64": (-2**63, 2**63-1), "UInt32": (0, 2**32-1),
                          "UInt64": (0, 2**64-1), "Int16": (-2**15, 2**15-1), "UInt16": (0, 2**16-1), "Byte": (0, 255), "SByte": (-128, 127)}
        if kind in integer_limits and not integer_limits[kind][0] <= number <= integer_limits[kind][1]:
            raise ValueError(f"Value outside {kind} limits")
        bounds = re.match(r"^From (.+) to (.+)$", entry["range"])
        if bounds and not float(bounds[1]) <= number <= float(bounds[2]):
            raise ValueError("Expected " + entry["range"])
    if entry["choices"] and value not in entry["choices"]:
        raise ValueError("Choose one of: " + ", ".join(entry["choices"]))
    return value


def validate_settings(values: dict) -> dict:
    result = {}
    for key, value in values.items():
        kind, _, low, high = SETTING_SCHEMA[key]
        if kind == "bool":
            if str(value).lower() not in ("true", "false", "0", "1"):
                raise ValueError(f"{key}: expected true or false")
            result[key] = "true" if str(value).lower() in ("true", "1") else "false"
        elif kind == "float":
            number = float(value)
            if not math.isfinite(number) or not low <= number <= high:
                raise ValueError(f"{key}: expected {low} to {high}")
            result[key] = format(number, "g")
        else:
            if not str(value).strip() or any(ch in str(value) for ch in "\r\n"):
                raise ValueError(f"{key}: provide a non-empty, single-line filter")
            result[key] = str(value)
    return result


def is_managed_dll(path: Path) -> bool:
    try:
        with path.open("rb") as f:
            if f.read(2) != b"MZ":
                return False
            f.seek(0x3C)
            pe = struct.unpack("<I", f.read(4))[0]
            f.seek(pe)
            if f.read(4) != b"PE\0\0":
                return False
            f.seek(pe + 24)
            magic = struct.unpack("<H", f.read(2))[0]
            directory = pe + 24 + (112 if magic == 0x20B else 96)
            f.seek(directory + 14 * 8)
            return struct.unpack("<II", f.read(8))[0] != 0
    except (OSError, struct.error):
        return False


def build_plugin(game: Path, project: Path, log=lambda message: None) -> dict:
    """Compile against local managed runtime DLLs; avoid NuGet and accidental wrapper redistribution."""
    game = validate_game(game)
    if not (game / "BepInEx/interop/Assembly-CSharp.dll").exists():
        raise ValueError("Generate BepInEx interop by running the game first")
    core_names = ("BepInEx.Core", "BepInEx.Unity.IL2CPP", "BepInEx.Unity.Common", "0Harmony", "Il2CppInterop.Runtime")
    refs = [p for p in (game / "dotnet").glob("*.dll") if is_managed_dll(p)]
    refs += [game / f"BepInEx/core/{name}.dll" for name in core_names]
    refs += list((game / "BepInEx/interop").glob("*.dll"))
    for p in refs:
        if not p.is_file():
            raise ValueError(f"Missing build reference: {p}")
    stamp = fingerprint(game)
    build_root = ROOT / "local/build"
    build_root.mkdir(parents=True, exist_ok=True)
    tree = ET.Element("Project")
    group = ET.SubElement(tree, "ItemGroup")
    for p in refs:
        node = ET.SubElement(group, "Reference", {"Include": p.stem})
        ET.SubElement(node, "HintPath").text = str(p)
        ET.SubElement(node, "Private").text = "false"
    props = build_root / "References.props"
    ET.ElementTree(tree).write(props, encoding="utf-8", xml_declaration=True)
    # Changes to *any* build input invalidate deployment, not just Assembly-CSharp.
    ref_hashes = {str(p): sha256(p) for p in refs}
    project = project.resolve()
    command = ["dotnet", "build", str(project), "-c", "Release", f"-p:LocalReferences={props}",
               "-p:RestoreSources=", "-p:NuGetAudit=false", "--nologo"]
    log("Building against installed .NET 6 runtime and current interop (no packages).")
    result = subprocess.run(command, cwd=project.parent, capture_output=True, text=True,
                            creationflags=CREATE_NO_WINDOW, timeout=180)
    log(result.stdout + result.stderr)
    if result.returncode:
        raise RuntimeError("Build failed. See compiler output.")
    if fingerprint(game)["files"] != stamp["files"] or any(sha256(Path(p)) != digest for p, digest in ref_hashes.items()):
        raise ValueError("Game references changed during compilation. Rebuild against a stable installation.")
    output = project.parent / "bin/Release/net6.0" / (project.stem + ".dll")
    if not output.exists():
        raise RuntimeError(f"Expected artifact missing: {output}")
    artifact = ROOT / "artifacts" / project.stem
    artifact.mkdir(parents=True, exist_ok=True)
    shutil.copy2(output, artifact / output.name)
    receipt = {"fingerprint": stamp, "references": ref_hashes, "dll": output.name,
               "dllSha256": sha256(output), "project": str(project), "runtimeTested": False}
    write_json(artifact / "build.json", receipt)
    return {"artifact": str(artifact), "receipt": receipt}


def deploy_plugin(game: Path, artifact: Path) -> Path:
    require_game_stopped()
    game = validate_game(game)
    receipt = json.loads((artifact / "build.json").read_text())
    if receipt["fingerprint"]["game"] != str(game):
        raise ValueError("Build belongs to another installation. Rebuild for the selected game folder.")
    if fingerprint(game)["files"] != receipt["fingerprint"]["files"]:
        raise ValueError("Game or loader changed after build. Rebuild before deploying.")
    for name, digest in receipt["references"].items():
        if not Path(name).is_file() or sha256(Path(name)) != digest:
            raise ValueError(f"Reference changed after build: {name}")
    dll = contained(artifact, artifact / receipt["dll"])
    if sha256(dll) != receipt["dllSha256"]:
        raise ValueError("Artifact failed integrity check. Rebuild.")
    target = game / "BepInEx/plugins/TK2-Mod-Studio" / dll.name
    if target.with_name(target.name + ".disabled").exists():
        raise ValueError("A disabled copy exists. Enable it first or remove it manually before updating.")
    transaction = backup_write(game, target, dll.read_bytes())
    return transaction


def create_mod(destination: Path, name: str) -> Path:
    if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]{2,48}", name):
        raise ValueError("Use 3–49 letters/digits/underscores, starting with a letter")
    destination = destination / name
    if destination.exists():
        raise ValueError("Project already exists")
    destination.mkdir(parents=True)
    template = ROOT / "templates/HelloMod"
    for p in template.iterdir():
        text = p.read_text(encoding="utf-8").replace("HelloMod", name).replace("hellomod", name.lower())
        target = destination / (name + p.suffix if p.name.startswith("HelloMod.") else p.name)
        target.write_text(text, encoding="utf-8")
    return destination / f"{name}.csproj"


def export_ghidra(home: Path, project: Path, log=lambda message: None, selected_name: str | None = None,
                  selected_address: str | None = None) -> dict:
    launcher = home / "support/analyzeHeadless.bat"
    if not launcher.exists() or not project.is_file() or project.suffix != ".gpr":
        raise ValueError("Choose a Ghidra installation and an existing .gpr project")
    out = ROOT / "local/ghidra"
    out.mkdir(parents=True, exist_ok=True)
    targets = ROOT / "tools/ghidra/targets.txt"
    limit = "45"
    if selected_name:
        targets = ROOT / "local/selected-target.txt"
        atomic_write(targets, (re.escape(selected_name) + "$\n").encode("utf-8"))
        limit = "1"
    command = [str(launcher), str(project.parent), project.stem, "-process", "GameAssembly.dll",
               "-readOnly", "-noanalysis", "-scriptPath", str(ROOT / "tools/ghidra"),
               "-postScript", "ExportTk2.java", str(out), "@" + str(targets), limit]
    if selected_address: command.append(selected_address)
    command.extend(["-log", str(ROOT / "local/ghidra-targets.log")])
    env = os.environ.copy()
    env["GHIDRA_HEADLESS_MAXMEM"] = "4G"
    # Delete old success marker before starting; an export failure must not look successful.
    marker = out / "summary.json"
    if marker.exists(): marker.unlink()
    result = subprocess.run(command, capture_output=True, text=True, errors="replace", env=env,
                            creationflags=CREATE_NO_WINDOW, timeout=1200)
    log(result.stdout + result.stderr)
    if result.returncode or not marker.exists():
        raise RuntimeError("Ghidra export failed. See output; close an open project if it is locked.")
    return json.loads(marker.read_text())


def decompile_managed(executable: Path, dll: Path, game: Path, log=lambda message: None) -> Path:
    if not executable.is_file() or not dll.is_file() or not is_managed_dll(dll):
        raise ValueError("Choose dnSpy.Console.exe and a managed mod DLL (not GameAssembly.dll)")
    out = ROOT / "local/managed" / (dll.stem + "-" + sha256(dll)[:12])
    out.mkdir(parents=True, exist_ok=True)
    paths = ";".join(str(p) for p in (game / "BepInEx/core", game / "BepInEx/interop", dll.parent))
    result = subprocess.run([str(executable), "--no-color", "--no-resources", "--asm-path", paths,
                             "-o", str(out), str(dll)], capture_output=True, text=True,
                            errors="replace", creationflags=CREATE_NO_WINDOW, timeout=180)
    log(result.stdout + result.stderr)
    if result.returncode:
        raise RuntimeError("Managed decompilation failed; see output")
    return out


def search_native_functions(query: str, limit: int = 500) -> tuple[int, list[dict]]:
    path = ROOT / "local/ghidra/functions.jsonl"
    entries, count = [], 0
    if not path.exists(): return 0, []
    with path.open(encoding="utf-8") as stream:
        for line in stream:
            if query.lower() not in line.lower(): continue
            count += 1
            if len(entries) < limit:
                entry = json.loads(line)
                entry["native"] = True
                entries.append(entry)
    return count, entries


def native_description(entry: dict) -> str:
    name = re.sub(r"[^A-Za-z0-9_.-]", "_", entry["name"])
    stem = (entry["address"] + "_" + name)[:150]
    path = ROOT / "local/ghidra/pseudocode" / (stem + ".c")
    heading = f"{entry['name']} @ {entry['address']}\n{entry['signature']}\n\n"
    return heading + (path.read_text(encoding="utf-8") if path.exists() else
        "Body not exported yet. Use Export selected method to produce readable Ghidra pseudocode.\n"
        "The signature can be approximate, especially for aliased native functions.")
