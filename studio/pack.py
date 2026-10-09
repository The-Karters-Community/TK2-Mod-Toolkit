"""The player-facing catalogue and settings for the single pack."""
from pathlib import Path
import json
from . import core, recipe_catalog

PROJECT = core.ROOT / "plugins/TK2.Customization/TK2.Customization.csproj"
ARTIFACT = core.ROOT / "artifacts/TK2.Customization"
SOURCE_ROOT = PROJECT.parent

FEATURES = [
    {"id": "OnlineProtection", "name": "Online protection", "category": "Safety", "description": "Mandatory protection. Blocks leaderboard uploads and online-room actions while unapproved mods are active. Cosmetic and local presentation modules are reviewed individually.", "origin": "New", "defaultEnabled": True, "locked": True, "settings": [("BlockLeaderboardUploads", "Block leaderboard uploads", "bool", True, None, None, "Mandatory. Record uploads are blocked while any non-allowlisted module or plugin is active."), ("BlockOnlineLobbyJoins", "Block online rooms", "bool", True, None, None, "Mandatory. Online rooms are blocked while any non-allowlisted module or plugin is active.")]},
    {"id": "UI", "name": "HUD scale", "category": "Interface", "description": "Make matching HUD canvases easier to read.", "origin": "New", "settings": [("HudScale", "Scale", "float", 1, .5, 2), ("CanvasNameFilter", "Canvas name contains", "text", "HUD", None, None)]},
    {"id": "HudOpacity", "name": "HUD opacity", "category": "Interface", "description": "Soften the HUD on matching canvases with an existing CanvasGroup. Uses the HUD name filter.", "origin": "New", "settings": [("Opacity", "Opacity", "float", 1, .1, 1)]},
    {"id": "DisableVignette", "name": "Disable vignette", "category": "Interface", "description": "Remove the game's edge-darkening vignette effect without changing its saved post-processing settings.", "origin": "New", "settings": []},
    {"id": "Audio", "name": "Audio mixer", "category": "Audio", "description": "Multiply the game's Wwise volume settings, then restore them when disabled.", "origin": "New", "settings": [("MasterVolume", "Master volume", "float", 1, 0, 1), ("MusicVolume", "Music", "float", 1, 0, 1), ("SfxVolume", "Sound effects", "float", 1, 0, 1), ("VoiceVolume", "Voices", "float", 1, 0, 1), ("UiVolume", "Interface sounds", "float", 1, 0, 1)]},
    {"id": "Camera", "name": "Camera setup", "category": "Camera", "description": "Adjust the local racing camera while keeping your kart comfortably framed.", "origin": "New", "settings": [("FieldOfView", "Field of view", "float", 65, 35, 110), ("PreserveKartFraming", "Keep kart size", "bool", True, None, None), ("DistanceMultiplier", "Camera distance", "float", 1, .5, 2.5), ("HeightOffset", "Camera height", "float", 0, -2, 4)]},
    {"id": "Rendering", "name": "Shadow distance", "category": "Graphics", "description": "Set Unity's shadow draw distance. Rendering behavior depends on the game's pipeline.", "origin": "New", "settings": [("ShadowDistance", "Distance", "float", 100, 0, 500)]},
    {"id": "Performance", "name": "Race performance", "category": "Graphics", "description": "Cache draw-distance settings and allow garbage collection during offline races. Optional lower AI physics changes movement accuracy.", "origin": "New", "gameplay": True, "settings": [
        ("CacheDrawDistance", "Cache draw distance", "bool", True, None, None, "Reuse camera distance settings to reduce repeated array allocations."),
        ("RaceGarbageCollection", "Allow race garbage collection", "bool", True, None, None, "Keep managed collection enabled during offline races; collections can cause brief pauses."),
        ("LowerAIPhysics", "Lower AI physics", "bool", False, None, None, "Use the game's existing lower-accuracy AI physics path. May affect movement and collisions; offline only."),
        ("AIPhysicsInterval", "AI physics interval", "int", 2, 1, 4, "Physics ticks between selected AI motor updates. 1 updates every tick; higher values lower accuracy.", None, "LowerAIPhysics"),
    ]},
    {"id": "PerformanceDiagnostics", "name": "Performance diagnostics", "category": "Graphics", "description": "Record game, engine, rendering and memory measurements during offline races. Available counters depend on the shipped player; capture stops automatically.", "origin": "New", "settings": [("CaptureSeconds", "Capture seconds", "int", 60, 10, 180, "Capture per race after a 5-second warmup; results are saved in BepInEx/diagnostics.")]},
    {"id": "TrackBoundaries", "name": "Track boundaries", "category": "Graphics", "description": "Inspect nearby invisible walls and kart-mask respawn colliders with optional translucent surfaces and pin-to-inspect details. Press F10 to open or close the in-race inspector; offline only.", "origin": "New", "settings": [
        ("ShowWalls", "Invisible walls", "bool", True, None, None),
        ("ShowRespawn", "Respawn colliders", "bool", True, None, None),
        ("ShowSurfaces", "Translucent surfaces", "bool", False, None, None, "Draw collider surfaces beneath their outlines."),
        ("SurfaceOpacity", "Surface opacity", "float", .12, .03, .35, "Fill alpha, adjustable while inspecting a track."),
        ("DrawDistance", "View distance", "float", 200, 25, 1000, "Distance from a local kart in metres."),
        ("MaxVisibleColliders", "Maximum outlines", "int", 64, 16, 192, "Draw only this many nearest matching colliders to limit overlay cost."),
        ("ToggleKey", "Visibility key", "text", "F10", None, None, "Toggle while this module is enabled.", ["F" + str(i) for i in range(1,13)] + ["None"]),
        ("InspectKey", "Pin collider key", "text", "F8", None, None, "Select a displayed boundary under the screen center and show its details. This does not affect physics.", ["F" + str(i) for i in range(1,13)] + ["None"]),
    ]},
    {"id": "Physics", "name": "Fast fall", "category": "Driving", "description": "Press Down Arrow or the mapped controller button after the minimum air time. Includes the legacy press, directional input and optional dodge modes.", "origin": "Community legacy port", "gameplay": True, "settings": [("FastFallAcceleration", "Downward acceleration", "float", 100, 0, 500), ("MinimumAirTime", "Air time before activation", "float", .4, 0, 3), ("UseSinglePressInput", "Press once per jump", "bool", True, None, None), ("ControllerAction", "Controller action", "text", "MenuTriangle", None, None), ("ShouldDodgeOnPress", "Dodge on fast fall", "bool", False, None, None), ("DodgeDurationAfterPress", "Dodge duration", "float", .5, .1, 1), ("MinimumJoystickInputBeforeFastFall", "Directional deadzone", "float", .1, 0, 1)]},
    {"id": "Laps", "name": "Custom lap count", "category": "Race rules", "description": "Choose how many laps an offline race requires. Port of the legacy custom-laps idea.", "origin": "Legacy port", "gameplay": True, "settings": [("Count", "Laps", "int", 3, 1, 99)]},
    {"id": "SimpleDriving", "name": "Driving challenge", "category": "Driving", "description": "Disable local jump and drift inputs. Fresh port of the legacy BoringMode idea.", "origin": "Legacy port", "gameplay": True, "settings": [("DisableJump", "Disable jumping", "bool", True, None, None), ("DisableDrift", "Disable drifting", "bool", True, None, None)]},
    {"id": "AutoBoost", "name": "Automatic drift boost", "category": "Driving", "description": "Automatically fire valid boosts; includes the legacy drift-release and early-press protection hooks.", "origin": "Legacy port", "gameplay": True, "settings": [("ThresholdPercent", "Auto-boost threshold", "float", 100, 1, 100), ("BoostOnDriftStop", "Boost when drift ends", "bool", True, None, None), ("SuppressEarlyBoost", "Ignore premature presses", "bool", True, None, None)]},
    {"id": "Tuning", "name": "Kart tuning", "category": "Driving", "description": "Scale local forward speed and jump strength from captured originals.", "origin": "Legacy port", "gameplay": True, "settings": [("SpeedMultiplier", "Speed multiplier", "float", 1, .25, 3), ("JumpMultiplier", "Jump multiplier", "float", 1, .25, 3)]},
]

# Static catalogs are generated from the actual typed C# bindings, so settings
# are available before the player's first game launch.
_CATALOG_ROOT = Path(__file__).resolve().parent
_MK = json.loads((_CATALOG_ROOT / "mk_catalog.json").read_text(encoding="utf-8"))["features"]
for feature in _MK:
    feature["settings"] = [tuple(s.get(k) for k in ("key", "label", "kind", "default", "low", "high", "description", "choices", "requires", "allowCustom")) for s in feature["settings"]]
_COMMUNITY = json.loads((_CATALOG_ROOT / "community_catalog.json").read_text(encoding="utf-8"))
FEATURES.extend(_MK + _COMMUNITY)
for feature in FEATURES:
    feature["name"] = {"UI": "HUD size", "HudOpacity": "HUD transparency", "Rendering": "Graphics"}.get(feature["id"], feature["name"])


_CAMERA = next(f for f in FEATURES if f["id"] == "Camera")
_CAMERA["description"] = "Live camera framing, aim and rotation. Enable Camera setup to use its in-race panel; press F8 in a local race or choose another key below."
_CAMERA["settings"] = [
    ("FieldOfView", "Field of view", "float", 65, 35, 110),
    ("PreserveKartFraming", "Keep kart size", "bool", True, None, None),
    ("DistanceMultiplier", "Camera distance", "float", 1, .25, 4),
    ("HeightOffset", "Camera height", "float", 0, -5, 8),
    ("LateralOffset", "Side offset", "float", 0, -4, 4),
    ("AimAtKart", "Aim toward kart", "bool", False, None, None),
    ("TargetHeight", "Look-at target height", "float", .5, -1, 3, "Used when Aim toward kart is on."),
    ("PitchOffset", "Pitch offset", "float", 0, -45, 45),
    ("YawOffset", "Yaw offset", "float", 0, -90, 90),
    ("RollOffset", "Roll offset", "float", 0, -30, 30),
    ("SmoothingSeconds", "Smoothing seconds", "float", .12, 0, 2, "Zero applies adjustments instantly."),
    ("PanelEnabled", "Enable in-race panel", "bool", True, None, None, "Available only while the Camera setup module is enabled."),
    ("PanelHotkey", "Panel hotkey", "text", "F8", None, None, "Press during a local race with Camera setup enabled. Escape also closes the panel.",
     ["F" + str(i) for i in range(1,13)] + list("ABCDEFGHIJKLMNOPQRSTUVWXYZ") + ["Insert", "Home", "End", "BackQuote", "None"]),
    ("PanelScale", "In-race panel scale", "float", 1, .7, 1.6, "Automatically fitted to the screen."),
]
_CAMERA["settingGroups"] = {"AimAtKart":"Aim & rotation", "TargetHeight":"Aim & rotation", "PitchOffset":"Aim & rotation", "YawOffset":"Aim & rotation", "RollOffset":"Aim & rotation", "SmoothingSeconds":"Transitions", "PanelEnabled":"In-race panel", "PanelHotkey":"In-race panel", "PanelScale":"In-race panel"}


RETIRED_MODULES = frozenset({'MirrorMode'} | {'Recipe.' + name for name in (
    'AirGlider','DriftCapacitor','EchoRewind','GravitySurf','LandingCombo',
    'RepulsorPulse','SlipstreamSling','CosmeticModel','TrackInspector')})
RETIRED_SETTINGS = frozenset()
FORCED_TRUE_SETTINGS = frozenset({
    ("OnlineProtection", "Enabled"),
    ("OnlineProtection", "BlockLeaderboardUploads"),
    ("OnlineProtection", "BlockOnlineLobbyJoins"),
})


def schema(features=None):
    result = {}
    for feature in features if features is not None else catalog_features():
        result[(feature["id"], "Enabled")] = ("bool", bool(feature.get("defaultEnabled", False)), None, None)
        for setting in feature["settings"]:
            key, _, kind, default, low, high = setting[:6]
            result[(feature["id"], key)] = (kind, default, low, high)
    return result


def validate(values):
    import math
    features = catalog_features()
    definitions = schema(features)
    options = {(f['id'],s[0]):s for f in features for s in f['settings']}
    result = {}
    for compound, value in values.items():
        section, separator, key = compound.partition("/")
        if not separator or (section, key) not in definitions:
            raise ValueError("Unknown pack setting: " + compound)
        kind, _, low, high = definitions[(section, key)]
        if kind == "bool":
            if not isinstance(value, bool): raise ValueError("Expected a boolean")
            result[(section, key)] = str(value).lower()
        elif kind in ("float", "int"):
            if isinstance(value, bool): raise ValueError("Expected a number, not a switch value")
            number = float(value)
            if not math.isfinite(number) or not low <= number <= high or (kind == "int" and not number.is_integer()):
                raise ValueError(f"{compound}: expected {kind} from {low} to {high}")
            result[(section, key)] = format(number, "g")
        else:
            if not isinstance(value, str) or any(c in value for c in "\r\n") or len(value) > 4096:
                raise ValueError("Expected a single-line setting")
            definition = options[(section,key)]
            if len(definition) > 7 and definition[7] and value not in definition[7]:
                raise ValueError(f"{compound}: choose one of the listed values")
            result[(section, key)] = value
    return result


def defaults():
    return {f"{s}/{k}": default for (s, k), (_, default, _, _) in schema().items()}


PACKS = [
    {"id": "garage", "name": "Toolkit Essentials", "description": "Online protection, camera, interface, audio, graphics and race performance controls.", "features": ["OnlineProtection", "UI", "HudOpacity", "DisableVignette", "Audio", "Camera", "Rendering", "Performance", "PerformanceDiagnostics", "TrackBoundaries"]},
    {"id": "community", "name": "Community Mods", "description": "Race rules, driving challenges, practice, advanced tuning and community commands.", "features": ["Laps", "SimpleDriving", "AutoBoost", "Tuning", "Physics"] + [f["id"] for f in _MK] + [f["id"] for f in _COMMUNITY]},
]


def catalog_features():
    return FEATURES + recipe_catalog.features()


def catalog_packs():
    recipes = recipe_catalog.features()
    packs = [{**p, 'features': p['features'] + [f['id'] for f in recipes if f['pack'] == p['id']]} for p in PACKS]
    members = [f['id'] for f in recipes if f['pack'] not in {p['id'] for p in PACKS}]
    extra = [{'id':'recipes','name':'Your recipes','description':'Create, edit and share your own C# modules.','features':members}] if members else []
    return packs + extra


def initial_config():
    values = validate(defaults())
    text = core.update_cfg("# TK2 Mod Toolkit Pack\n", values)
    return text


def seed_config(text):
    """Add missing definitions and enforce the mandatory online-safety settings."""
    present = {(e["section"], e["key"]) for e in core.parse_cfg_settings(text)}
    missing = {pair: value for pair, value in validate(defaults()).items() if pair not in present}
    missing.update({pair: "true" for pair in FORCED_TRUE_SETTINGS})
    return core.update_cfg(text or "# TK2 Mod Toolkit Pack\n", missing) if missing else text


def source_files():
    roots = (SOURCE_ROOT, core.ROOT / "src/Reconstructed")
    return sorted(str(p.relative_to(core.ROOT)).replace("\\", "/") for root in roots for p in root.rglob("*.cs")
                  if not any(part in ("bin", "obj") for part in p.relative_to(root).parts))


def source_path(name):
    if name not in source_files(): raise ValueError("Choose a listed C# source file")
    return core.contained(core.ROOT, core.ROOT / name)


def module_sources():
    """Logical modules share one project; expose their real source ownership."""
    ownership = {
        "UI": ["StudioBehaviour.cs", "Plugin.cs"],
        "OnlineProtection": ["OnlineProtectionFeature.cs", "OnlineProtectionPolicy.cs", "Plugin.cs"],
        "HudOpacity": ["StudioBehaviour.cs", "PackModules.cs"],
        "DisableVignette": ["PackModules.cs", "Plugin.cs"],
        "Rendering": ["StudioBehaviour.cs", "PackModules.cs"],
        "Performance": ["PerformanceFeature.cs"],
        "PerformanceDiagnostics": ["PerformanceDiagnostics.cs", "PerformanceSamples.cs", "PerformanceEngineSamples.cs", "PerformanceFeature.cs"],
        "TrackBoundaries": ["TrackBoundaries.cs", "BoundarySelection.cs", "BoundaryGeometry.cs", "StudioBehaviour.cs"],
        "Audio": ["AudioFeature.cs", "Plugin.cs"],
        "Camera": ["CameraFeature.cs", "CameraSettings.cs", "CameraPanel.cs"],
        "Physics": ["FastFallFeature.cs", "Plugin.cs"],
        "CommunityCommands": ["CommunityMods.cs", "CommunityCommandParser.cs"],
        "NightmareAI": ["NightmareAI.cs"],
        "CNKBoostMeter": ["LegacyMK.Meter.cs", "LegacyMK.cs"],
        "KartParameters": ["LegacyMK.Parameters.cs", "LegacyMK.cs"],
        "BoostParameters": ["LegacyMK.Parameters.cs", "LegacyMK.cs"],
        "SaveStates": ["LegacyMK.Practice.cs", "LegacyMK.cs"],
        "ReverseRace": ["LegacyMK.Practice.cs", "LegacyMK.cs"],
    }
    for name in ("Laps", "SimpleDriving", "AutoBoost", "Tuning"):
        ownership[name] = ["PackModules.cs", "Plugin.cs"]
    available = set(source_files())
    result = {feature["id"]: ["plugins/TK2.Customization/" + file for file in
              ownership.get(feature["id"], ["LegacyMK.cs"]) if "plugins/TK2.Customization/" + file in available]
              for feature in FEATURES}
    import re
    for file in available:
        if "/Recipes/" not in file: continue
        text = source_path(file).read_text(encoding="utf-8")
        match = re.search(r'public\s+(?:override\s+)?string\s+Name\s*=>\s*"([^"\r\n]+)"', text)
        if not match: continue
        name = match[1]
        helper = "plugins/TK2.Customization/RecipeHost.cs"
        dependencies = [helper]
        # Built-in recipes expose their supporting code in Edit and exports.
        dependencies += sorted(dep for dep in available if dep.startswith('plugins/TK2.Customization/Recipes/' + name) and dep != file)
        result["Recipe." + name] = [file] + list(dict.fromkeys(dep for dep in dependencies if dep in available))
    return result


def create_recipe(name):
    import re
    if not re.fullmatch(r"[A-Za-z][A-Za-z0-9]{2,39}", name):
        raise ValueError("Use 3 to 40 letters and digits, starting with a letter")
    path = SOURCE_ROOT / "Recipes" / (name + ".cs")
    if path.exists(): raise ValueError("That recipe already exists")
    source = (core.ROOT / "templates/PackRecipe.cs.txt").read_text(encoding="utf-8").replace("RecipeName", name)
    core.atomic_write(path, source.encode("utf-8"))
    return str(path.relative_to(core.ROOT)).replace("\\", "/")
