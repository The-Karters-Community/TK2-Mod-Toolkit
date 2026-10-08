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
        (self.game / "BepInEx/LogOutput.log").write_text('Chainloader initialized\n[Error: Test] Failure\n')
        with patch.object(core, "is_managed_dll", return_value=True):
            result = setup.readiness(self.game)
        self.assertTrue(result["ready"])
        self.assertFalse(result["checks"][-1]["ok"])
        self.assertIn('Failure', result["runtimeErrors"][0])

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


if __name__ == '__main__': unittest.main()
