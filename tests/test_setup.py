import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from studio import core, pack, setup, symbols, webapp


class SetupTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(); self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        p = patch.object(core, "ROOT", self.root); p.start(); self.addCleanup(p.stop)
        self.game = self.root / "Game"
        self.game.mkdir()
        for file in ("TheKarters2.exe", "GameAssembly.dll"): (self.game / file).write_bytes(b"fixture")

    def test_discovers_secondary_library_and_manifest_directory(self):
        steam = self.root / "Steam"; secondary = self.root / "OtherLibrary"
        (steam / "steamapps").mkdir(parents=True)
        (steam / "steamapps/libraryfolders.vdf").write_text('"libraryfolders" { "1" { "path" "' + str(secondary).replace('\\','\\\\') + '" } }')
        game = secondary / "steamapps/common/RenamedGame"; game.mkdir(parents=True)
        for file in ("TheKarters2.exe", "GameAssembly.dll"): (game / file).write_bytes(b"fixture")
        (secondary / "steamapps/appmanifest_2269950.acf").write_text('"installdir" "RenamedGame"')
        with patch.object(setup, "steam_roots", return_value=[steam]), patch.object(core, "DEFAULT_GAME", self.root / "missing"):
            self.assertEqual(setup.discover(), [game.resolve()])

    def test_setup_distinguishes_loader_files_from_initialized_loader(self):
        self.assertEqual(setup.readiness(self.game)["stage"], "install-loader")
        for relative in setup.CRITICAL:
            p = self.game / relative; p.parent.mkdir(parents=True, exist_ok=True); p.write_bytes(b"fixture")
        (self.game / "doorstop_config.ini").write_text('[General]\nenabled=true\ntarget_assembly=BepInEx\\core\\BepInEx.Unity.IL2CPP.dll\n')
        self.assertEqual(setup.readiness(self.game)["stage"], "initialize-loader")
        interop = self.game / "BepInEx/interop/Assembly-CSharp.dll"; interop.parent.mkdir(); interop.write_bytes(b"fixture")
        (self.game / "BepInEx/LogOutput.log").write_text(
            f"[Message: Preloader] BepInEx {setup.SUPPORTED_BEPINEX_VERSION} - TheKarters2\n"
            f"[Message: Preloader] Built from commit {setup.SUPPORTED_BEPINEX_COMMIT}\n"
            "Chainloader initialized\n[Error: Test] Failure\n")
        with patch.object(core, "is_managed_dll", return_value=True):
            result = setup.readiness(self.game)
        self.assertTrue(result["ready"])
        self.assertTrue(result["loaderCompatible"])
        self.assertEqual(result["loaderVersion"], setup.SUPPORTED_BEPINEX_VERSION)
        self.assertFalse(result["checks"][-1]["ok"])
        self.assertIn('Failure', result["runtimeErrors"][0])

    def test_rejects_unverified_older_loader_build(self):
        for relative in setup.CRITICAL:
            p = self.game / relative; p.parent.mkdir(parents=True, exist_ok=True); p.write_bytes(b"fixture")
        (self.game / "doorstop_config.ini").write_text('[General]\nenabled=true\ntarget_assembly=BepInEx\\core\\BepInEx.Unity.IL2CPP.dll\n')
        interop = self.game / "BepInEx/interop/Assembly-CSharp.dll"; interop.parent.mkdir(); interop.write_bytes(b"fixture")
        (self.game / "BepInEx/LogOutput.log").write_text(
            "[Message: Preloader] BepInEx 6.0.0-be.777 - TheKarters2\n"
            "[Message: Preloader] Built from commit 1111111\nChainloader initialized\n")
        with patch.object(core, "is_managed_dll", return_value=True): result = setup.readiness(self.game)
        self.assertFalse(result["ready"])
        self.assertEqual(result["stage"], "install-loader")
        self.assertFalse(result["loaderCompatible"])
        self.assertIn(setup.SUPPORTED_BEPINEX_VERSION, result["message"])

    def test_install_loader_preserves_existing_mods_and_configs(self):
        source = self.root / "vendor/BepInEx"
        for relative in (*setup.CRITICAL, "BepInEx/plugins/old.dll", "BepInEx/config/old.cfg"):
            p = source / relative; p.parent.mkdir(parents=True, exist_ok=True); p.write_bytes(b"loader")
        with patch.object(core, "require_game_stopped"):
            setup.install_loader(self.game)
        self.assertTrue((self.game / "winhttp.dll").exists())
        self.assertFalse((self.game / "BepInEx/plugins/old.dll").exists())
        self.assertFalse((self.game / "BepInEx/config/old.cfg").exists())
        self.assertEqual(len(list((self.root / "local/backups").glob('*/receipt.json'))), len(setup.CRITICAL))

    def test_first_run_without_game_still_opens_setup_state(self):
        with patch.object(setup, "discover", return_value=[]): app = webapp.Application()
        with patch.object(pack, "source_files", return_value=[]): state = app.state()
        self.assertEqual(state["setup"]["stage"], "choose-game")
        self.assertFalse(state["installed"])
        self.assertEqual(state["game"], "")

    def test_player_install_needs_no_compiler_and_supports_new_directory(self):
        artifact = self.root / "artifacts/TK2.Customization"; artifact.mkdir(parents=True)
        dll = artifact / "TK2.Customization.dll"; dll.write_bytes(b"built")
        for relative in ("BepInEx/core/BepInEx.Unity.IL2CPP.dll", "BepInEx/core/Il2CppInterop.Runtime.dll"):
            p = self.game / relative; p.parent.mkdir(parents=True, exist_ok=True); p.write_bytes(b"same")
        core.write_json(artifact / "build.json", {"dll": dll.name, "dllSha256": core.sha256(dll), "fingerprint": {"game": "A different PC", "files": {r: core.sha256(self.game / r) for r in ("GameAssembly.dll", "BepInEx/core/BepInEx.Unity.IL2CPP.dll", "BepInEx/core/Il2CppInterop.Runtime.dll")}}})
        with patch.object(core, "require_game_stopped"), patch.object(setup, "readiness", return_value={"ready":True}):
            setup.install_prebuilt(self.game)
        self.assertEqual((self.game / "BepInEx/plugins/TK2-Mod-Studio/TK2.Customization.dll").read_bytes(), b"built")
        dll.write_bytes(b"tampered")
        with patch.object(core, "require_game_stopped"), patch.object(setup, "readiness", return_value={"ready":True}), self.assertRaises(ValueError): setup.install_prebuilt(self.game)

    def test_prebuilt_compatibility_names_each_mismatched_runtime_file(self):
        artifact = self.root / "artifacts/TK2.Customization"; artifact.mkdir(parents=True)
        for relative in ("GameAssembly.dll", "BepInEx/core/BepInEx.Unity.IL2CPP.dll", "BepInEx/core/Il2CppInterop.Runtime.dll"):
            path = self.game / relative; path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(b"current")
        core.write_json(artifact / "build.json", {"fingerprint":{"files":{"GameAssembly.dll":"old","BepInEx/core/BepInEx.Unity.IL2CPP.dll":"old","BepInEx/core/Il2CppInterop.Runtime.dll":"old"}}})
        result = setup.prebuilt_compatibility(self.game)
        self.assertFalse(result["compatible"])
        self.assertEqual(len(result["mismatches"]), 3)
        self.assertIn("BepInEx/core/BepInEx.Unity.IL2CPP.dll", result["reason"])

    def test_function_browser_uses_real_va_and_matches_recovered_name(self):
        (self.root / "local").mkdir()
        dump = self.root / "dump.cs"
        dump.write_text('// Image 0: Assembly-CSharp.dll - 0\n// Namespace: \npublic class Kart // TypeDefIndex: 0\n{\n\t// RVA: 0x10 Offset: 0x10 VA: 0x180000010\n\tpublic void Jump(bool pressed) { }\n}\n')
        core.index_dump(dump, self.root / "local/catalog.json")
        (self.root / "src/Reconstructed").mkdir(parents=True)
        core.write_json(self.root / "src/Reconstructed/provenance.json", {"methods":[{"method":"Kart.Jump","address":"180000010","source":"src/Reconstructed/Kart.cs","confidence":"reviewed"}]})
        result = symbols.search('Jump')
        self.assertEqual(result["methods"][0]["address"], '180000010')
        self.assertEqual(result["reconstructed"], 1)
        self.assertIn('public void Jump(bool pressed);', symbols.detail('0:0')["declaration"])

    def test_runtime_check_tracks_plugin_version_instead_of_legacy_constant(self):
        source = self.root / 'plugins/TK2.Customization/Plugin.cs'
        source.parent.mkdir(parents=True)
        source.write_text('[BepInPlugin("local.tk2.customization", "TK2 Mod Toolkit Pack", "0.6.0")]')
        log = self.game / 'BepInEx/LogOutput.log'
        log.parent.mkdir(parents=True, exist_ok=True)
        for version, expected in [('0.4.1', False), ('0.6.0', True)]:
            log.write_text(f'[Info: BepInEx] Loading [TK2 Mod Toolkit Pack {version}]\n'
                           f'[Info: TK2 Mod Toolkit Pack] TK2 Mod Toolkit {version}: startup complete.\n')
            result = setup.readiness(self.game)
            self.assertEqual(next(c for c in result['checks'] if c['name'] == 'Current pack loaded successfully')['ok'], expected)
            self.assertNotEqual(result['loaderSource'], str(source))

    def test_chainloader_loading_line_is_not_treated_as_success_after_plugin_error(self):
        for relative in setup.CRITICAL:
            p = self.game / relative; p.parent.mkdir(parents=True, exist_ok=True); p.write_bytes(b"fixture")
        (self.game / "doorstop_config.ini").write_text('[General]\nenabled=true\ntarget_assembly=BepInEx\\core\\BepInEx.Unity.IL2CPP.dll\n')
        interop = self.game / "BepInEx/interop/Assembly-CSharp.dll"; interop.parent.mkdir(parents=True); interop.write_bytes(b"fixture")
        source = self.root / 'plugins/TK2.Customization/Plugin.cs'; source.parent.mkdir(parents=True)
        source.write_text('[BepInPlugin("local.tk2.customization", "TK2 Mod Toolkit Pack", "0.6.19")]')
        log = self.game / "BepInEx/LogOutput.log"
        log.parent.mkdir(parents=True, exist_ok=True)
        log.write_text(f"[Message: Preloader] BepInEx {setup.SUPPORTED_BEPINEX_VERSION}\n"
                       f"[Message: Preloader] Built from commit {setup.SUPPORTED_BEPINEX_COMMIT}\n"
                       "Chainloader initialized\n[Info: BepInEx] Loading [TK2 Mod Toolkit Pack 0.6.19]\n"
                       "[Error: BepInEx] Error loading [TK2 Mod Toolkit Pack 0.6.19]: MissingMethodException\n")
        with patch.object(core, "is_managed_dll", return_value=True): result = setup.readiness(self.game)
        self.assertTrue(result["ready"], "compatible loader remains repairable")
        self.assertFalse(result["checks"][-1]["ok"], "chainloader text must not imply plugin success")
        self.assertIn("MissingMethodException", result["pluginLoadError"])
        self.assertIn("failed", result["message"])

    def test_old_plugin_failure_is_not_reported_as_current_after_repair_install(self):
        for relative in setup.CRITICAL:
            p = self.game / relative; p.parent.mkdir(parents=True, exist_ok=True); p.write_bytes(b"fixture")
        (self.game / "doorstop_config.ini").write_text('[General]\nenabled=true\ntarget_assembly=BepInEx\\core\\BepInEx.Unity.IL2CPP.dll\n')
        interop = self.game / "BepInEx/interop/Assembly-CSharp.dll"; interop.parent.mkdir(parents=True); interop.write_bytes(b"fixture")
        source = self.root / 'plugins/TK2.Customization/Plugin.cs'; source.parent.mkdir(parents=True)
        source.write_text('[BepInPlugin("local.tk2.customization", "TK2 Mod Toolkit Pack", "0.6.20")]')
        log = self.game / "BepInEx/LogOutput.log"; log.parent.mkdir(parents=True, exist_ok=True)
        log.write_text(f"[Message: Preloader] BepInEx {setup.SUPPORTED_BEPINEX_VERSION}\n"
                       f"[Message: Preloader] Built from commit {setup.SUPPORTED_BEPINEX_COMMIT}\n"
                       "Chainloader initialized\n[Error: BepInEx] Error loading [TK2 Mod Toolkit Pack 0.6.19]: MissingMethodException\n")
        with patch.object(core, "is_managed_dll", return_value=True): result = setup.readiness(self.game)
        self.assertTrue(result["ready"])
        self.assertFalse(result["checks"][-1]["ok"])
        self.assertIsNone(result["pluginLoadError"])
        self.assertEqual(len(result["previousPluginLoadErrors"]), 1)
        self.assertIn("check again", result["message"])


if __name__ == '__main__': unittest.main()
