"""Discover Steam installs and prepare the loader without launching the game."""
import json
import os
from pathlib import Path
import re
from . import core

PACKAGE = "BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788+5b766a3"
CRITICAL = ("winhttp.dll", "doorstop_config.ini", "BepInEx/core/BepInEx.Unity.IL2CPP.dll",
            "BepInEx/core/Il2CppInterop.Runtime.dll", "dotnet/coreclr.dll")


def steam_roots():
    roots = []
    if os.name == "nt":
        import winreg
        for hive, key, value in ((winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam", "SteamPath"),
                                  (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath")):
            try:
                with winreg.OpenKey(hive, key) as handle: roots.append(Path(winreg.QueryValueEx(handle, value)[0]))
            except OSError: pass
    roots.append(Path(os.environ.get("PROGRAMFILES(X86)", "C:/Program Files (x86)")) / "Steam")
    return list(dict.fromkeys(roots))


def discover():
    libraries = []
    for steam in steam_roots():
        libraries.append(steam)
        vdf = steam / "steamapps/libraryfolders.vdf"
        if vdf.is_file():
            libraries.extend(Path(p.replace("\\\\", "\\")) for p in re.findall(r'"path"\s*"([^"]+)"', vdf.read_text(encoding="utf-8", errors="replace")))
    candidates = []
    saved = core.ROOT / "local/preferences.json"
    if saved.is_file():
        try: candidates.append(Path(json.loads(saved.read_text())["game"]))
        except (ValueError, KeyError): pass
    for library in dict.fromkeys(libraries):
        manifest = library / "steamapps/appmanifest_2269950.acf"
        directory = "The Karters 2 Turbo Charged"
        if manifest.is_file():
            match = re.search(r'"installdir"\s*"([^"]+)"', manifest.read_text(encoding="utf-8", errors="replace"))
            if match and Path(match[1]).name == match[1]: directory = match[1]
        candidates.append(library / "steamapps/common" / directory)
    candidates.append(core.DEFAULT_GAME)
    valid = []
    for candidate in candidates:
        try:
            game = core.validate_game(candidate)
            if game not in valid: valid.append(game)
        except (ValueError, OSError): pass
    return valid


def loader_source():
    return next((p for p in (core.ROOT / "vendor/BepInEx", core.ROOT.parent / PACKAGE)
                 if all((p / name).is_file() for name in CRITICAL)), None)


def loader_files(source):
    return [p for p in source.rglob("*") if p.is_file() and not any(part in ("plugins", "config", "interop", "cache") for part in p.relative_to(source).parts)]


def readiness(game):
    if game is None:
        return {"stage": "choose-game", "ready": False, "message": "Choose your game folder or scan Steam libraries.", "checks": []}
    source = loader_source()
    required = {Path(name) for name in CRITICAL}
    if source:
        required.update(p.relative_to(source) for p in loader_files(source) if p.suffix.lower() in (".dll", ".json") or p.name == "winhttp.dll")
    missing = [str(name) for name in sorted(required) if not (game / name).is_file()]
    config = (game / "doorstop_config.ini").read_text(encoding="utf-8-sig", errors="replace") if (game / "doorstop_config.ini").is_file() else ""
    enabled = bool(re.search(r'^\s*enabled\s*=\s*true\s*$', config, re.M | re.I))
    target_ok = bool(re.search(r'^\s*target_assembly\s*=.*BepInEx[\\/]core[\\/]BepInEx\.Unity\.IL2CPP\.dll\s*$', config, re.M | re.I))
    interop = game / "BepInEx/interop/Assembly-CSharp.dll"
    log = next((game / "BepInEx" / name for name in ("LogOutput.log", "LogOutput.txt") if (game / "BepInEx" / name).is_file()), None)
    text = log.read_text(encoding="utf-8", errors="replace") if log else ""
    initialized = "Chainloader initialized" in text and interop.is_file() and core.is_managed_dll(interop)
    loaded = "Loading [TK2 Mod Garage Pack 0.3.0]" in text or "TK2 Mod Garage 0.3.0:" in text
    warnings = [line for line in text.splitlines() if "[Warning" in line][-15:]
    errors = [line for line in text.splitlines() if any(word in line.lower() for word in ("[error", "exception", "error loading", "unavailable", "disabled after", "stopped:"))][-35:]
    stage = "install-loader" if missing or not enabled or not target_ok else "initialize-loader" if not initialized else "ready"
    message = {"install-loader": "Install or repair BepInEx first.", "initialize-loader": "Start the game once, wait for its menu, close it, then check again.", "ready": "BepInEx is initialized. The mod pack can be installed."}[stage]
    return {"stage": stage, "ready": stage == "ready", "message": message, "loaderSource": str(source) if source else None,
            "missingFiles": missing, "checks": [{"name": "Loader files", "ok": not missing}, {"name": "Doorstop enabled and configured", "ok": enabled and target_ok},
                {"name": "Game started with BepInEx", "ok": initialized}, {"name": "Current pack appeared in game log", "ok": loaded}],
            "logPath": str(log) if log else None, "logModified": log.stat().st_mtime if log else None, "runtimeErrors": errors, "runtimeWarnings": warnings}


def install_loader(game):
    core.require_game_stopped()
    source = loader_source()
    if source is None: raise ValueError("Loader bundle missing. Put the BepInEx Unity IL2CPP x64 distribution in vendor/BepInEx, then retry.")
    files = loader_files(source)
    # Existing mod/config directories are excluded. Every replaced loader file has
    # a normal reversible SDK backup receipt.
    installed = 0
    for path in files:
        relative = path.relative_to(source)
        target = core.contained(game, game / relative)
        data = path.read_bytes()
        if not target.is_file() or target.read_bytes() != data:
            core.backup_write(game, target, data); installed += 1
    return {"message": f"BepInEx prepared ({installed} files). Start the game once and wait for its menu, then close it and check again.", "setup": readiness(game)}


def install_prebuilt(game):
    """Player installation uses the bundled DLL; author builds are separate."""
    core.require_game_stopped()
    if not readiness(game)["ready"]: raise ValueError(readiness(game)["message"])
    artifact = core.ROOT / "artifacts/TK2.Customization"
    receipt = json.loads((artifact / "build.json").read_text()) if (artifact / "build.json").is_file() else None
    if not receipt: raise ValueError("No built plugin is bundled. Use Workshop → Build pack first.")
    dll = core.contained(artifact, artifact / receipt["dll"])
    if core.sha256(dll) != receipt["dllSha256"]: raise ValueError("Bundled plugin changed after building; rebuild first.")
    for relative in ("GameAssembly.dll", "BepInEx/core/BepInEx.Unity.IL2CPP.dll", "BepInEx/core/Il2CppInterop.Runtime.dll"):
        expected = receipt["fingerprint"]["files"].get(relative)
        if not expected or core.sha256(game / relative) != expected: raise ValueError("This game/loader build differs from the bundled plugin. Build against your installation in Workshop; gameplay ports also need their version check updated.")
    target = core.contained(game, game / "BepInEx/plugins/TK2-Mod-Studio/TK2.Customization.dll")
    backup = core.backup_write(game, target, dll.read_bytes())
    return backup
