"""Managed static model library, FBX conversion and asset-only deployment."""
from __future__ import annotations

import itertools
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
import uuid

from . import core

LIBRARY = core.ROOT / "local/imported-models"
TEXTURES = {".png", ".jpg", ".jpeg"}
BUNDLES = {".bundle", ".unity3d", ".assetbundle"}
LIMIT = 256 * 1024 * 1024


def discover_blender(custom: str = "") -> str:
    if custom:
        path = Path(custom.strip().strip('"')).expanduser()
        if not path.is_file() or path.name.lower() not in {"blender.exe", "blender"}:
            raise ValueError("Choose an existing blender.exe, not its installation folder or launcher.")
        return str(path.resolve())
    found = shutil.which("blender")
    if found:
        return str(Path(found).resolve())
    candidates = []
    for variable in ("ProgramFiles", "ProgramW6432", "ProgramFiles(x86)"):
        folder = Path(os.environ.get(variable, "C:/Program Files")) / "Blender Foundation"
        if folder.is_dir():
            candidates.extend(folder.glob("Blender */blender.exe"))
    if candidates:
        return str(sorted(set(candidates), key=lambda p: tuple(int(n) for n in re.findall(r"\d+", p.parent.name)), reverse=True)[0].resolve())
    return ""


def _inside(root: Path, path: Path) -> Path:
    root = root.resolve()
    resolved = path.resolve()
    if not resolved.is_relative_to(root) or resolved == root:
        raise ValueError("Model reference leaves its source folder: " + str(path))
    cursor = path.absolute()
    while cursor != root and root in cursor.parents:
        if cursor.is_symlink() or (hasattr(cursor, "is_junction") and cursor.is_junction()):
            raise ValueError("Model references must not use filesystem links or junctions.")
        cursor = cursor.parent
    if root.is_symlink() or (hasattr(root, "is_junction") and root.is_junction()):
        raise ValueError("Model folders must not be links or junctions.")
    return resolved


def _file(path: Path, maximum: int) -> Path:
    if not path.is_file() or path.stat().st_size < 1 or path.stat().st_size > maximum:
        raise ValueError("Missing, empty or oversized model asset: " + path.name)
    return path


def _record(model_id: str) -> tuple[Path, dict]:
    if not re.fullmatch(r"[a-z0-9][a-z0-9-]{0,79}", model_id or ""):
        raise ValueError("Choose a model from the imported model library.")
    folder = _inside(LIBRARY, LIBRARY / model_id)
    try:
        record = json.loads((folder / "model.json").read_text(encoding="utf-8"))
    except (OSError, ValueError) as error:
        raise ValueError("Imported model is missing or its manifest is invalid. Import it again.") from error
    if not isinstance(record, dict) or record.get("id") != model_id:
        raise ValueError("Imported model manifest does not match its folder.")
    files = record.get("files")
    if not isinstance(files, dict) or not files or len(files) > 130 or any(not isinstance(k, str) or not re.fullmatch(r"[a-f0-9]{64}", str(v)) for k, v in files.items()):
        raise ValueError("Imported model manifest has invalid file receipts. Import it again.")
    if record.get("modelPath") not in {"model.obj", "model.bundle"} or record["modelPath"] not in files:
        raise ValueError("Imported model manifest has no valid main model asset.")
    _inside(folder, folder / record["modelPath"])
    return folder, record


def state() -> dict:
    models = []
    if LIBRARY.is_dir():
        for folder in sorted(LIBRARY.iterdir()):
            if folder.is_dir() and not folder.name.startswith("."):
                try:
                    _, record = _record(folder.name)
                    models.append(record)
                except (ValueError, KeyError):
                    continue
    return {"models": models, "blender": discover_blender(), "supports": ["FBX", "OBJ", "AssetBundle"],
            "unityVersion": "6000.0.75f1"}


def _relative_file(root: Path, text: str) -> Path:
    # Windows backslashes are accepted in creator material references.
    value = text.strip().strip('"').replace("\\", "/")
    if not value or value.startswith("/") or any(char in value for char in '<>:"|?*\x00'):
        raise ValueError("Model dependencies need relative paths within their source folder: " + text)
    return _inside(root, root / value)


def _image_size(path: Path) -> tuple[int, int]:
    _file(path, 16 * 1024 * 1024)
    data = path.read_bytes()
    width = height = 0
    if len(data) >= 24 and data[:8] == b"\x89PNG\r\n\x1a\n":
        width, height = struct.unpack(">II", data[16:24])
    elif data.startswith(b"\xff\xd8"):
        offset = 2
        while offset + 3 < len(data):
            if data[offset] != 255:
                break
            offset += 1
            while offset < len(data) and data[offset] == 255:
                offset += 1
            if offset >= len(data):
                break
            marker = data[offset]
            offset += 1
            if marker in {0xD8, 0x01} or 0xD0 <= marker <= 0xD7:
                continue
            if marker in {0xD9, 0xDA} or offset + 1 >= len(data):
                break
            length = int.from_bytes(data[offset:offset + 2], "big")
            if length < 2 or offset + length > len(data):
                break
            if 0xC0 <= marker <= 0xCF and marker not in {0xC4, 0xC8, 0xCC} and length >= 7:
                height, width = struct.unpack(">HH", data[offset + 3:offset + 7])
                break
            offset += length
    if not (0 < width <= 8192 and 0 < height <= 8192 and width * height <= 16000000):
        raise ValueError("Texture is not a supported PNG/JPEG or exceeds 8192 pixels per side / 16 megapixels: " + path.name)
    return width, height


def _obj(path: Path, sample: bool = False) -> dict:
    _file(path, 32 * 1024 * 1024)
    vertices = []
    uv_count = normal_count = triangles = corners = 0
    preview_faces = []
    material_files = set()
    materials = set()
    with path.open(encoding="utf-8-sig", errors="strict") as stream:
        for line_number, source in enumerate(stream, 1):
            if len(source) > 65536:
                raise ValueError("OBJ line exceeds 64 KB.")
            line = source.partition("#")[0].strip()
            item = line.split()
            if not item:
                continue
            if item[0] in {"v", "vn", "vt"}:
                count = 2 if item[0] == "vt" else 3
                try:
                    values = [float(value) for value in item[1:1 + count]]
                except ValueError as error:
                    raise ValueError(f"OBJ line {line_number} contains invalid numeric values.") from error
                if len(values) != count or any(not math.isfinite(value) or abs(value) > 1000000 for value in values):
                    raise ValueError(f"OBJ line {line_number} contains invalid or unbounded coordinates.")
                if item[0] == "v":
                    vertices.append(values)
                    if len(vertices) > 250000:
                        raise ValueError("OBJ exceeds 250,000 source vertices.")
                elif item[0] == "vt":
                    uv_count += 1
                else:
                    normal_count += 1
                if uv_count > 500000 or normal_count > 500000:
                    raise ValueError("OBJ attribute count exceeds 500,000.")
            elif item[0] == "f":
                if len(item) < 4:
                    raise ValueError("OBJ polygon has fewer than three vertices.")
                indices = []
                for corner in item[1:]:
                    refs = corner.split("/")
                    if len(refs) > 3:
                        raise ValueError("OBJ corner has an invalid index format.")
                    indices.append(_index(refs[0], len(vertices)))
                    if len(refs) > 1 and refs[1]:
                        _index(refs[1], uv_count)
                    if len(refs) > 2 and refs[2]:
                        _index(refs[2], normal_count)
                triangles += len(indices) - 2
                corners += 3 * (len(indices) - 2)
                if triangles > 500000:
                    raise ValueError("OBJ exceeds 500,000 triangles.")
                if sample and len(preview_faces) < 2000:
                    for index in range(1, len(indices) - 1):
                        if len(preview_faces) >= 2000:
                            break
                        preview_faces.append([indices[0], indices[index], indices[index + 1]])
            elif item[0] == "mtllib":
                material_files.add(line[6:].strip())
            elif item[0] == "usemtl":
                materials.add(line[7:].strip())
                if len(materials) > 64:
                    raise ValueError("OBJ exceeds 64 material groups.")
    if not triangles:
        raise ValueError("OBJ contains no usable polygon faces.")
    result = {"vertices": len(vertices), "faces": triangles, "materials": sorted(materials), "mtl": sorted(material_files)}
    if sample:
        selected = sorted({index for face in preview_faces for index in face})
        remap = {value: index for index, value in enumerate(selected)}
        # Preview uses the same default handedness as runtime MirrorX=true.
        result["preview"] = {"vertices": [[-vertices[i][0], vertices[i][1], vertices[i][2]] for i in selected],
            "faces": [[remap[index] for index in reversed(face)] for face in preview_faces],
            "bounds": {"min": [-max(v[0] for v in vertices), min(v[1] for v in vertices), min(v[2] for v in vertices)],
                       "max": [-min(v[0] for v in vertices), max(v[1] for v in vertices), max(v[2] for v in vertices)]},
            "sampled": triangles > len(preview_faces), "sourceFaces": triangles}
    return result


def _index(text: str, count: int) -> int:
    try:
        value = int(text)
    except ValueError as error:
        raise ValueError("OBJ corner has an invalid integer index.") from error
    index = value - 1 if value > 0 else count + value
    if value == 0 or index < 0 or index >= count:
        raise ValueError("OBJ corner references an absent vertex, UV or normal.")
    return index


def _copy_obj(source: Path, output: Path) -> tuple[dict, list[str]]:
    details = _obj(source)
    root = source.parent.resolve()
    warnings = []
    companions = {}
    pixels = 0
    surfaces = 0
    for name in details["mtl"]:
        material = _relative_file(root, name)
        _file(material, 1024 * 1024)
        text = material.read_text(encoding="utf-8-sig")
        for source_line in text.splitlines():
            if len(source_line) > 65536:
                raise ValueError("MTL line exceeds 64 KB.")
            line = source_line.partition("#")[0].strip()
            item = line.split()
            if not item:
                continue
            if item[0] == "newmtl":
                surfaces += 1
                if surfaces > 64:
                    raise ValueError("MTL exceeds 64 materials.")
            elif item[0] == "Kd":
                try:
                    values = [float(value) for value in item[1:4]]
                except ValueError as error:
                    raise ValueError("MTL diffuse colour contains invalid numbers.") from error
                if len(values) != 3 or any(not math.isfinite(value) or abs(value) > 1000000 for value in values):
                    raise ValueError("MTL diffuse colour is incomplete or unbounded.")
            elif item[0] == "map_Kd":
                relative = line[6:].strip().strip('"')
                if relative.startswith("-"):
                    warnings.append("Diffuse texture mapping options are unsupported: " + relative)
                    continue
                texture = _relative_file(material.parent, relative)
                # A nested MTL must keep references below its own directory, as runtime requires.
                if texture.suffix.lower() not in TEXTURES:
                    warnings.append("Unsupported diffuse image format: " + texture.name)
                    continue
                width, height = _image_size(texture)
                if texture not in companions:
                    pixels += width * height
                    if pixels > 32000000:
                        raise ValueError("OBJ diffuse textures exceed 32 megapixels combined.")
                companions[texture] = texture.relative_to(root)
            elif item[0].startswith("map_") or item[0] in {"bump", "norm"}:
                warnings.append("Only diffuse image maps are rendered; ignored " + item[0] + ".")
        companions[material] = material.relative_to(root)
    total = source.stat().st_size + sum(file.stat().st_size for file in companions)
    if total > LIMIT:
        raise ValueError("Imported model assets exceed 256 MB.")
    output.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, output / "model.obj")
    for file, relative in companions.items():
        target = _inside(output, output / relative)
        if target.name.lower() in {"model.obj", "model.json", "conversion.json"}:
            raise ValueError("Model dependency collides with the import manifest.")
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(file, target)
    # Renaming the OBJ does not change MTL relative references.
    return details, sorted(set(warnings))


def _stage_fbx(source: Path, folder: Path):
    folder.mkdir(parents=True)
    shutil.copy2(source, folder / "source.fbx")
    candidates = list(itertools.islice(source.parent.glob("*"), 1001))
    companion_folder = source.parent / (source.stem + ".fbm")
    if companion_folder.is_dir():
        _inside(source.parent, companion_folder)
        candidates += list(itertools.islice(companion_folder.rglob("*"), 1001))
    if len(candidates) > 1000:
        raise ValueError("Too many files beside FBX. Place it and its textures in a dedicated folder.")
    total = source.stat().st_size
    names = {}
    for candidate in candidates:
        if candidate.suffix.lower() not in TEXTURES or not candidate.is_file():
            continue
        candidate = _inside(source.parent, candidate)
        _image_size(candidate)
        key = candidate.name.casefold()
        digest = core.sha256(candidate)
        if key in names:
            if names[key] == digest:
                continue
            raise ValueError("FBX texture filenames collide. Give its images unique filenames.")
        names[key] = digest
        total += candidate.stat().st_size
        if total > LIMIT:
            raise ValueError("FBX and companion textures exceed 256 MB.")
        shutil.copy2(candidate, folder / candidate.name)


def import_model(path: str | Path, blender: str = "") -> dict:
    source = Path(path).expanduser().resolve()
    _file(source, LIMIT)
    extension = source.suffix.lower()
    if extension not in {".fbx", ".obj"} | BUNDLES:
        raise ValueError("Choose an FBX, OBJ or Unity AssetBundle model file.")
    LIBRARY.mkdir(parents=True, exist_ok=True)
    digest = core.sha256(source)
    slug = re.sub(r"[^a-z0-9]+", "-", source.stem.casefold()).strip("-")[:40] or "model"
    model_id = slug + "-" + digest[:8] + "-" + uuid.uuid4().hex[:6]
    final = _inside(LIBRARY, LIBRARY / model_id)
    # Work is contained in a unique local temporary directory; failed conversion never
    # appears as an imported library entry and never touches the installation.
    with tempfile.TemporaryDirectory(prefix=".model-import-", dir=LIBRARY) as temporary:
        work = _inside(LIBRARY, Path(temporary))
        output = work / "output"
        warnings = []
        conversion = {}
        if extension == ".fbx":
            executable = discover_blender(blender)
            if not executable:
                raise ValueError("FBX import needs installed Blender. Install Blender 3.5 or newer, or choose blender.exe in the Toolkit.")
            input_folder = work / "input"
            _stage_fbx(source, input_folder)
            output.mkdir()
            script = core.ROOT / "templates/ConvertFbx.py"
            if not script.is_file():
                raise ValueError("Toolkit FBX converter script is missing. Rebuild or reinstall the Toolkit.")
            command = [executable, "--background", "--factory-startup", "--disable-autoexec", "--python-exit-code", "7",
                "--python", str(script), "--", str(input_folder / "source.fbx"), str(output / "model.obj")]
            try:
                result = subprocess.run(command, capture_output=True, text=True, encoding="utf-8", errors="replace",
                    creationflags=core.CREATE_NO_WINDOW, timeout=180)
            except subprocess.TimeoutExpired as error:
                raise ValueError("FBX conversion exceeded three minutes. Simplify the mesh/textures and retry.") from error
            except OSError as error:
                raise ValueError("Could not start selected Blender: " + str(error)) from error
            log = result.stdout + result.stderr
            (core.ROOT / "local").mkdir(exist_ok=True)
            core.atomic_write(core.ROOT / "local/model-conversion.log", log.encode("utf-8"))
            if result.returncode or not (output / "model.obj").is_file():
                meaningful = [line for line in log.splitlines() if any(word in line for word in ("Error", "RuntimeError", "Exception", "Traceback"))]
                raise ValueError("Blender FBX conversion failed: " + " ".join(meaningful[-4:] or log.splitlines()[-4:])[:1200] + " See local/model-conversion.log.")
            conversion = json.loads((output / "conversion.json").read_text(encoding="utf-8"))
            validated = work / "validated"
            details, warnings = _copy_obj(output / "model.obj", validated)
            warnings += conversion.get("warnings", [])
            output = validated
        elif extension == ".obj":
            details, warnings = _copy_obj(source, output)
        else:
            with source.open("rb") as stream:
                if stream.read(8).split(b"\0")[0] not in {b"UnityFS", b"UnityRaw", b"UnityWeb"}:
                    raise ValueError("Selected file is not a recognized Unity AssetBundle.")
            output.mkdir()
            shutil.copy2(source, output / "model.bundle")
            details = {"vertices": None, "faces": None, "materials": []}
            warnings = ["Bundle must be built for Windows x64 in Unity 6000.0.75f1. Runtime copies static meshes only; preview is available for FBX/OBJ."]
        record = {"id": model_id, "name": source.stem, "format": "FBX" if extension == ".fbx" else "OBJ" if extension == ".obj" else "AssetBundle",
            "modelPath": "model.bundle" if extension in BUNDLES else "model.obj", "vertices": details["vertices"], "faces": details["faces"],
            "warnings": sorted(set(warnings)), "importedAt": time.time(), "sourceSha256": digest,
            "blenderVersion": conversion.get("blenderVersion"), "files": {}}
        for file in output.rglob("*"):
            if file.is_file():
                record["files"][file.relative_to(output).as_posix()] = core.sha256(file)
        core.write_json(output / "model.json", record)
        output.rename(final)
    return {**record, "libraryPath": str(final)}


def preview(model_id: str) -> dict:
    folder, record = _record(model_id)
    if record["format"] == "AssetBundle":
        return {"unsupported": "AssetBundle preview requires the Unity runtime; use FBX/OBJ for Toolkit wireframe preview.", "vertices": [], "faces": []}
    return _obj(_inside(folder, folder / record["modelPath"]), sample=True)["preview"]


def deploy(game: Path, model_id: str) -> dict:
    game = core.validate_game(Path(game))
    folder, record = _record(model_id)
    target_root = game / "BepInEx/models" / model_id
    backups = []
    total = 0
    for relative, digest in record["files"].items():
        source = _relative_file(folder, relative)
        _file(source, LIMIT)
        total += source.stat().st_size
        if total > LIMIT or core.sha256(source) != digest:
            raise ValueError("Imported model assets changed or exceed the allowed size. Import them again before deployment.")
    # Validate every asset before the first write, and never touch the plugin or config.
    for relative in record["files"]:
        source = _relative_file(folder, relative)
        target = _inside(target_root, target_root / relative)
        backups.append(str(core.backup_write(game, target, source.read_bytes())))
    relative = model_id + "/" + record["modelPath"]
    return {"id": model_id, "modelPath": relative, "ModelPath": relative, "files": len(backups), "backups": backups}
