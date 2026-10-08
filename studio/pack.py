"""The player-facing catalogue and settings for the single pack."""
from pathlib import Path
from . import core

PROJECT = core.ROOT / "plugins/TK2.Customization/TK2.Customization.csproj"
ARTIFACT = core.ROOT / "artifacts/TK2.Customization"
SOURCE_ROOT = PROJECT.parent

FEATURES = [
    {"id": "UI", "name": "HUD scale", "category": "Interface", "description": "Make matching HUD canvases easier to read.", "origin": "New", "settings": [("HudScale", "Scale", "float", 1, .5, 2), ("CanvasNameFilter", "Canvas name contains", "text", "HUD", None, None)]},
    {"id": "HudOpacity", "name": "HUD opacity", "category": "Interface", "description": "Soften the HUD on matching canvases with an existing CanvasGroup. Uses the HUD name filter.", "origin": "New", "settings": [("Opacity", "Opacity", "float", 1, .1, 1)]},
    {"id": "Audio", "name": "Audio mixer", "category": "Audio", "description": "Multiply the game's Wwise volume settings, then restore them when disabled.", "origin": "New", "settings": [("MasterVolume", "Volume multiplier", "float", 1, 0, 1)]},
    {"id": "Camera", "name": "Camera field of view", "category": "Camera", "description": "Choose a wider or tighter view for the main perspective camera.", "origin": "New", "settings": [("FieldOfView", "Vertical FOV", "float", 65, 35, 110)]},
    {"id": "Rendering", "name": "Shadow distance", "category": "Graphics", "description": "Set Unity's shadow draw distance. Rendering behavior depends on the game's pipeline.", "origin": "New", "settings": [("ShadowDistance", "Distance", "float", 100, 0, 500)]},
    {"id": "Physics", "name": "Fast fall", "category": "Driving", "description": "Hold Down Arrow while airborne to fall faster. Uses editable reconstructed velocity logic.", "origin": "Adapted idea", "gameplay": True, "settings": [("FastFallAcceleration", "Downward acceleration", "float", 100, 0, 500), ("MinimumAirTime", "Air time before activation", "float", .4, 0, 3)]},
    {"id": "Laps", "name": "Custom lap count", "category": "Race rules", "description": "Choose how many laps an offline race requires. Port of the legacy custom-laps idea.", "origin": "Legacy port", "gameplay": True, "settings": [("Count", "Laps", "int", 3, 1, 99)]},
    {"id": "SimpleDriving", "name": "Driving challenge", "category": "Driving", "description": "Disable local jump and drift inputs. Fresh port of the legacy BoringMode idea.", "origin": "Legacy port", "gameplay": True, "settings": []},
    {"id": "AutoBoost", "name": "Automatic drift boost", "category": "Driving", "description": "Fire a filled boost near the perfect window. Partial port: no old bad-boost suppression.", "origin": "Legacy port", "gameplay": True, "settings": []},
    {"id": "Tuning", "name": "Kart tuning", "category": "Driving", "description": "Scale local forward speed and jump strength from captured originals.", "origin": "Legacy port", "gameplay": True, "settings": [("SpeedMultiplier", "Speed multiplier", "float", 1, .25, 3), ("JumpMultiplier", "Jump multiplier", "float", 1, .25, 3)]},
]


def schema():
    result = {}
    for feature in FEATURES:
        result[(feature["id"], "Enabled")] = ("bool", False, None, None)
        for key, _, kind, default, low, high in feature["settings"]:
            result[(feature["id"], key)] = (kind, default, low, high)
    return result


def validate(values):
    import math
    result = {}
    for compound, value in values.items():
        section, separator, key = compound.partition("/")
        if not separator or (section, key) not in schema():
            raise ValueError("Unknown pack setting: " + compound)
        kind, _, low, high = schema()[(section, key)]
        if kind == "bool":
            if not isinstance(value, bool): raise ValueError("Expected a boolean")
            if value and key == "Enabled" and next(f for f in FEATURES if f["id"] == section).get("gameplay"):
                raise ValueError("Gameplay modules remain locked pending runtime leaderboard validation")
            result[(section, key)] = str(value).lower()
        elif kind in ("float", "int"):
            number = float(value)
            if not math.isfinite(number) or not low <= number <= high or (kind == "int" and not number.is_integer()):
                raise ValueError(f"{compound}: expected {kind} from {low} to {high}")
            result[(section, key)] = format(number, "g")
        else:
            if not isinstance(value, str) or not value.strip() or any(c in value for c in "\r\n"):
                raise ValueError("Expected a nonempty single-line filter")
            result[(section, key)] = value
    return result


def defaults():
    return {f"{s}/{k}": default for (s, k), (_, default, _, _) in schema().items()}


def initial_config():
    values = validate(defaults())
    text = core.update_cfg("# TK2 Mod Garage Pack\n", values)
    return text + """
[Recipe.FrameLimiter]
## Target frame rate; VSync is disabled while this recipe is enabled.
# Setting type: Int32
# Default value: 120
# Acceptable value range: From 30 to 360
FramesPerSecond = 120

## Enable the frame limiter recipe.
# Setting type: Boolean
# Default value: false
Enabled = false
"""


def source_files():
    roots = (SOURCE_ROOT, core.ROOT / "src/Reconstructed")
    return sorted(str(p.relative_to(core.ROOT)).replace("\\", "/") for root in roots for p in root.rglob("*.cs")
                  if not any(part in ("bin", "obj") for part in p.relative_to(root).parts))


def source_path(name):
    if name not in source_files(): raise ValueError("Choose a listed C# source file")
    return core.contained(core.ROOT, core.ROOT / name)


def create_recipe(name):
    import re
    if not re.fullmatch(r"[A-Za-z][A-Za-z0-9]{2,39}", name):
        raise ValueError("Use 3 to 40 letters and digits, starting with a letter")
    path = SOURCE_ROOT / "Recipes" / (name + ".cs")
    if path.exists(): raise ValueError("That recipe already exists")
    source = (core.ROOT / "templates/PackRecipe.cs.txt").read_text(encoding="utf-8").replace("RecipeName", name)
    core.atomic_write(path, source.encode("utf-8"))
    return str(path.relative_to(core.ROOT)).replace("\\", "/")
