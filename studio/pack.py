"""The player-facing catalogue and settings for the single pack."""
from pathlib import Path
import json
from . import core, recipe_catalog
from .shortcut_choices import add_shortcut_choices

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
    {"id": "PerformanceDiagnostics", "name": "Performance diagnostics", "category": "Graphics", "description": "Record game, engine, rendering and memory measurements during offline races. Available counters depend on the shipped player; capture stops automatically.", "origin": "New", "settings": [("CaptureSeconds", "Capture seconds", "int", 60, 10, 180, "Capture per race after a 5-second warmup; results are saved in BepInEx/diagnostics."), ("CaptureMemorySnapshot", "Capture memory snapshot", "bool", False, None, None, "One-time main-menu census of loaded textures, meshes, thumbnails and cameras. No image or simulation changes; resets after capture.")]},
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
    {"id": "GamepadOverlay", "name": "Gamepad viewer", "category": "Interface", "description": "Show live local racing controls or the action values recorded on a playing leaderboard ghost. Uses the game's Rewired mappings, so supported controllers and keyboard input share the same action-aware display.", "origin": "New", "settings": [
        ("StartVisible", "Show automatically", "bool", True, None, None, "Show while local race or replay input is available."),
        ("ShowReplayInputs", "Show leaderboard ghost inputs", "bool", True, None, None, "Display recorded actions during leaderboard ghost playback."),
        ("ToggleKey", "Toggle hotkey", "text", "F9", None, None, "Toggle the in-game input card. Choose None to disable the hotkey.", ["F" + str(i) for i in range(1,13)] + ["None"]),
        ("Corner", "Screen position", "text", "TopCenter", None, None, "Choose a screen preset or Custom to place the overlay with the percentage sliders.", ["TopLeft", "TopCenter", "TopRight", "MiddleLeft", "MiddleCenter", "MiddleRight", "BottomLeft", "BottomCenter", "BottomRight", "Custom"]),
        ("PositionX", "Custom horizontal position", "float", 50, 0, 100, "Used with Custom. 0 is the left edge; 100 is the right edge."),
        ("PositionY", "Custom vertical position", "float", 0, 0, 100, "Used with Custom. 0 is the top edge; 100 is the bottom edge."),
        ("Scale", "Overlay scale", "float", 1, .25, 2),
        ("Opacity", "Background opacity", "float", .92, .45, 1),
    ]},
    {"id": "Physics", "name": "Fast fall", "category": "Driving", "description": "Press Down Arrow or the mapped controller button after the minimum air time. Includes the legacy press, directional input and optional dodge modes.", "origin": "Community legacy port", "gameplay": True, "settings": [("FastFallAcceleration", "Downward acceleration", "float", 100, 0, 500), ("MinimumAirTime", "Air time before activation", "float", .4, 0, 3), ("UseSinglePressInput", "Press once per jump", "bool", True, None, None), ("ControllerAction", "Controller button", "text", "MenuTriangle", None, None), ("KeyboardKey", "Keyboard key", "text", "DownArrow", None, None), ("ShouldDodgeOnPress", "Dodge on fast fall", "bool", False, None, None), ("DodgeDurationAfterPress", "Dodge duration", "float", .5, .1, 1), ("MinimumJoystickInputBeforeFastFall", "Directional deadzone", "float", .1, 0, 1)]},
    {"id": "Antigravity", "name": "Antigravity (experimental)", "category": "Driving", "description": "Offline surface driving. Enable Drive on ground and walls to follow solid track contacts across ground/wall collider layers. Respawn volumes, triggers and racers remain excluded. Surface camera is temporarily disabled after a reported crash. In-game validation pending.", "origin": "New", "gameplay": True, "settings": [("AllowWallSurfaces", "Drive on ground and walls", "bool", False, None, None, "Follow solid ground and wall contacts without the curved-road arming gate. Offline only."), ("DisableSplineTeleports", "Disable spline teleports (testing)", "bool", False, None, None, "Temporary offline loop test. Blocks only new local Bezier spline movement while Antigravity is enabled; respawns and instant teleports are unchanged."), ("ShowStatus", "Show status", "bool", True, None, None, "Show current surface angle and collider layer.")]},
    {"id": "Laps", "name": "Custom lap count", "category": "Race rules", "description": "Choose how many laps an offline race requires. Port of the legacy custom-laps idea.", "origin": "Legacy port", "gameplay": True, "settings": [("Count", "Laps", "int", 3, 1, 99)]},
    {"id": "SimpleDriving", "name": "Driving challenge", "category": "Driving", "description": "Disable local jump and drift inputs. Fresh port of the legacy BoringMode idea.", "origin": "Legacy port", "gameplay": True, "settings": [("DisableJump", "Disable jumping", "bool", True, None, None), ("DisableDrift", "Disable drifting", "bool", True, None, None)]},
    {"id": "AutoBoost", "name": "Automatic drift boost", "category": "Driving", "description": "Automatically fire valid boosts; includes the legacy drift-release and early-press protection hooks.", "origin": "Legacy port", "gameplay": True, "settings": [("ThresholdPercent", "Auto-boost threshold", "float", 100, 1, 100), ("BoostOnDriftStop", "Boost when drift ends", "bool", True, None, None), ("SuppressEarlyBoost", "Ignore premature presses", "bool", True, None, None)]},
    {"id": "Tuning", "name": "Kart tuning", "category": "Driving", "description": "Scale local forward speed and jump strength from captured originals.", "origin": "Legacy port", "gameplay": True, "settings": [("SpeedMultiplier", "Speed multiplier", "float", 1, .25, 3), ("JumpMultiplier", "Jump multiplier", "float", 1, .25, 3)]},
]

FEATURES.extend([
    {"id": "MineTraps", "name": "Mine Traps", "category": "Items", "description": "Use the dormant MINA_09 as a large local trap. Choose a forward physics throw or a surface placement behind the kart; mines can also replace item-box rewards. In-game validation pending.", "origin": "New", "gameplay": True, "settings": [
        ("ItemBoxRewards", "Include in item-box rewards", "bool", True, None, None),
        ("ItemBoxChancePercent", "Item-box chance (%)", "float", 20, 0, 100, "Chance to replace an item box's first reward with mines."),
        ("RewardAmmo", "Mines per reward", "int", 1, 1, None, "Suggested 1-3; no upper limit."),
        ("DropKey", "Extra test drop key", "text", "M", None, None, "Free test drop independent of inventory ammo. Select None to use item boxes only."),
        ("ControllerAction", "Extra test drop controller button", "text", "", None, None),
        ("PlacementMode", "Mine placement", "text", "Forward throw", None, None, "Throw forward with Rigidbody physics or place a stationary mine on the track behind you.", ["Forward throw", "Drop behind"]),
        ("TimedDetonationDelay", "Timed detonation (seconds)", "float", 45, 0, None, "The mine detonates after this delay. No upper limit."),
        ("ForwardThrowSpeed", "Forward throw speed (m/s)", "float", 30, 0, None, "Used by Forward throw mode. Suggested 30 m/s; no upper limit."),
        ("RearDropDistance", "Distance behind kart", "float", 4, 0, None, "Used by Drop behind mode. Suggested 3-6 metres; no upper limit."),
        ("MineSizeMultiplier", "Mine size multiplier", "float", 5, 0.1, None, "Scales the whole visible mine model; the native trigger sphere is kept at its original size. No upper limit."),
        ("Damage", "Mine damage", "int", 80, 0, None, "Damage dealt when a mine explodes. No upper limit."),
        ("TriggerRadius", "Mine trigger radius", "float", 4, 0.1, None, "Kart-to-mine distance for the offline proximity fallback. Suggested 4 metres; no upper limit."),
        ("TimedBlastRadius", "Timed blast radius (m)", "float", 10, 0.1, None, "At timer expiry, spawn the mine explosion at its location and apply native HP damage to every kart within this radius. No remote-owner fallback."),
    ], "settingGroups": {
        "ItemBoxRewards": "Item boxes", "ItemBoxChancePercent": "Item boxes", "RewardAmmo": "Item boxes",
        "DropKey": "Placement controls", "ControllerAction": "Placement controls", "PlacementMode": "Placement controls",
        "ForwardThrowSpeed": "Placement controls", "RearDropDistance": "Placement controls", "MineSizeMultiplier": "Placement controls",
        "TimedDetonationDelay": "Trap behavior", "TriggerRadius": "Trap behavior", "TimedBlastRadius": "Trap behavior", "Damage": "Trap behavior",
    }},
    {"id": "AirShift", "name": "Air Dash", "category": "Driving", "description": "A lateral velocity burst while airborne. It preserves kart heading, vertical velocity and total horizontal speed. One dash per jump by default; choose keys and gamepad buttons below.", "origin": "New", "gameplay": True, "settings": [
        ("LeftKey", "Dash left key", "text", "LeftBracket", None, None),
        ("RightKey", "Dash right key", "text", "RightBracket", None, None),
        ("LeftAction", "Controller left action", "text", "", None, None, "Optional existing Rewired action; blank disables this controller shortcut."),
        ("RightAction", "Controller right action", "text", "", None, None, "Choose actions that do not overlap your trick or Fast Fall controls."),
        ("SidewaysImpulse", "Lateral speed impulse (m/s)", "float", 65, 0.1, None, "Suggested 65 m/s; total horizontal speed is conserved. No upper limit."),
        ("ShiftDuration", "Burst duration (seconds)", "float", .25, 0.01, None, "Suggested 0.25 seconds; no upper limit."),
        ("OncePerJump", "Once per jump", "bool", True, None, None, "Turn off to allow repeated shifts before landing."),
        ("ShowStatus", "Show Air Dash status", "bool", True, None, None),
    ]},
    {"id": "PracticeRewind", "name": "Rewind / Practice Scrubbing", "category": "Training", "description": "Experimental solo training: Backspace rewinds 3 seconds; hold to scrub farther, release to resume. Restores driving, reserves and boost state within the current checkpoint segment. History clears on checkpoints, recovery and teleports. Race clock, items and other racers are not rewound. In-game validation pending.", "origin": "New", "gameplay": True, "settings": [
        ("RewindKey", "Rewind key", "text", "Backspace", None, None),
        ("ControllerAction", "Controller action", "text", "", None, None, "Optional existing Rewired action; blank uses keyboard only."),
        ("RewindSeconds", "Initial rewind seconds", "float", 3, .5, 10),
        ("HistorySeconds", "History seconds", "float", 10, 3, 20, "Recorded at 20 samples per second; automatically enlarged to cover the initial rewind interval."),
        ("ShowStatus", "Show practice status", "bool", True, None, None),
    ]},
])

# Static catalogs are generated from the actual typed C# bindings, so settings
# are available before the player's first game launch.
_CATALOG_ROOT = Path(__file__).resolve().parent
_MK = json.loads((_CATALOG_ROOT / "mk_catalog.json").read_text(encoding="utf-8"))["features"]
for feature in _MK:
    feature["settings"] = [tuple(s.get(k) for k in ("key", "label", "kind", "default", "low", "high", "description", "choices", "requires", "allowCustom")) for s in feature["settings"]]
_COMMUNITY = json.loads((_CATALOG_ROOT / "community_catalog.json").read_text(encoding="utf-8"))
FEATURES.extend(_MK + _COMMUNITY)
add_shortcut_choices(FEATURES)
for feature in FEATURES:
    feature["name"] = {"UI": "HUD size", "HudOpacity": "HUD transparency", "Rendering": "Graphics", "AirShift": "Air Dash"}.get(feature["id"], feature["name"])


_CAMERA = next(f for f in FEATURES if f["id"] == "Camera")
_CAMERA["description"] = "Tune your racing view. Press F8 in a race to adjust it while you drive."
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
    ("PanelEnabled", "Enable in-race panel", "bool", True, None, None, "Available during a local race while Camera Setup is enabled."),
    ("PanelHotkey", "Panel hotkey", "text", "F8", None, None, "Press during a local race. Escape also closes the panel.",
     ["F" + str(i) for i in range(1,13)] + list("ABCDEFGHIJKLMNOPQRSTUVWXYZ") + ["Insert", "Home", "End", "BackQuote", "None"]),
    ("PanelScale", "In-race panel scale", "float", 1, .7, 1.6, "Automatically fitted to the screen."),
]
_CAMERA["settingGroups"] = {"AimAtKart":"Aim & rotation", "TargetHeight":"Aim & rotation", "PitchOffset":"Aim & rotation", "YawOffset":"Aim & rotation", "RollOffset":"Aim & rotation", "SmoothingSeconds":"Transitions", "PanelEnabled":"In-race panel", "PanelHotkey":"In-race panel", "PanelScale":"In-race panel"}


RETIRED_MODULES = frozenset({'MirrorMode', 'RicochetItems'} | {'Recipe.' + name for name in (
    'AirGlider','DriftCapacitor','EchoRewind','GravitySurf','LandingCombo',
    'RepulsorPulse','SlipstreamSling','CosmeticModel','TrackInspector')})
RETIRED_SETTINGS = frozenset({('AirShift', key) for key in (
    'AllowGroundTesting', 'DashDistance', 'DashDuration', 'LimitHorizontalSpeed', 'SpeedRetention'
)} | {('MineTraps', key) for key in ('DropDistance', 'OwnerCanTrigger', 'ArmDelay', 'Lifetime', 'ThrowForceMultiplier')})
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
            if not math.isfinite(number) or (low is not None and number < low) or (high is not None and number > high) or (kind == "int" and not number.is_integer()):
                raise ValueError(f"{compound}: expected {kind} from {low} to {high}")
            result[(section, key)] = format(number, "g")
        else:
            if not isinstance(value, str) or any(c in value for c in "\r\n") or len(value) > 4096:
                raise ValueError("Expected a single-line setting")
            definition = options[(section,key)]
            if len(definition) > 7 and definition[7] and not (len(definition) > 9 and definition[9]) and value not in definition[7]:
                raise ValueError(f"{compound}: choose one of the listed values")
            result[(section, key)] = value
    return result


def defaults():
    return {f"{s}/{k}": default for (s, k), (_, default, _, _) in schema().items()}


PACKS = [
    {"id": "garage", "name": "Toolkit Essentials", "description": "Online protection, camera, interface, audio, graphics and race performance controls.", "features": ["OnlineProtection", "UI", "HudOpacity", "DisableVignette", "GamepadOverlay", "Audio", "Camera", "Rendering", "Performance", "PerformanceDiagnostics", "TrackBoundaries"]},
    {"id": "community", "name": "Community Mods", "description": "Race rules, driving challenges, practice, advanced tuning and community commands.", "features": ["Laps", "SimpleDriving", "AutoBoost", "Tuning", "Physics", "Antigravity", "AirShift", "PracticeRewind", "MineTraps"] + [f["id"] for f in _MK] + [f["id"] for f in _COMMUNITY]},
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
        "PerformanceDiagnostics": ["PerformanceDiagnostics.cs", "PerformanceSamples.cs", "PerformanceEngineSamples.cs", "PerformanceFeature.cs", "MemoryCensus.cs"],
        "TrackBoundaries": ["TrackBoundaries.cs", "BoundarySelection.cs", "BoundaryGeometry.cs", "StudioBehaviour.cs"],
        "GamepadOverlay": ["GamepadOverlay.cs", "InputOverlayModel.cs", "StudioBehaviour.cs", "Plugin.cs"],
        "Audio": ["AudioFeature.cs", "Plugin.cs"],
        "Camera": ["CameraFeature.cs", "CameraSettings.cs", "CameraPanel.cs"],
        "Physics": ["FastFallFeature.cs", "ShortcutInput.cs", "Plugin.cs"],
        "Antigravity": ["AntigravityFeature.cs", "AntigravitySurfaceMode.cs", "AntigravitySplineTeleport.cs", "AntigravityPolicy.cs", "AntigravityCamera.cs", "AntigravityCameraMath.cs", "CameraFeature.cs", "FeatureHook.cs", "AirShiftFeature.cs", "AirDashMath.cs", "Plugin.cs"],
        "AirShift": ["FeatureHook.cs", "AirShiftFeature.cs", "AirDashMath.cs", "ShortcutInput.cs", "Plugin.cs"],
        "PracticeRewind": ["FeatureHook.cs", "PracticeRewindFeature.cs", "PracticeHistory.cs", "DrivingSnapshot.cs", "AirShiftFeature.cs", "AirDashMath.cs", "FastFallFeature.cs", "ShortcutInput.cs", "StudioBehaviour.cs", "Plugin.cs"],
        "MineTraps": ["FeatureHook.cs", "MineTrapFeature.cs", "ShortcutInput.cs", "AirShiftFeature.cs", "AirDashMath.cs", "Plugin.cs", "StudioBehaviour.cs"],
        "CommunityCommands": ["CommunityMods.cs", "CommunityCommandParser.cs"],
        "NightmareAI": ["NightmareAI.cs"],
        "CNKBoostMeter": ["LegacyMK.Meter.cs", "LegacyMK.cs"],
        "KartParameters": ["LegacyMK.Parameters.cs", "LegacyMK.cs"],
        "BoostParameters": ["LegacyMK.Parameters.cs", "LegacyMK.cs"],
        "SaveStates": ["FeatureHook.cs", "LegacyMK.Practice.cs", "LegacyMK.cs", "DrivingSnapshot.cs", "AirShiftFeature.cs", "AirDashMath.cs", "FastFallFeature.cs", "ShortcutInput.cs"],
        "ReverseRace": ["LegacyMK.Practice.cs", "LegacyMK.cs"],
    }
    for name in ("Laps", "SimpleDriving", "AutoBoost", "Tuning"):
        ownership[name] = ["PackModules.cs", "Plugin.cs"]
    available = set(source_files())
    result = {feature["id"]: ["plugins/TK2.Customization/" + file for file in
              ownership.get(feature["id"], ["LegacyMK.cs"]) if "plugins/TK2.Customization/" + file in available]
              for feature in FEATURES}
    version_source = "plugins/TK2.Customization/ToolkitVersion.cs"
    for files in result.values():
        if "plugins/TK2.Customization/Plugin.cs" in files and version_source in available:
            files.append(version_source)
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
