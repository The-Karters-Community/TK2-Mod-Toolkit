import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from studio import core


class CoreTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.game = self.root / "game"
        self.game.mkdir()
        (self.game / "TheKarters2.exe").write_bytes(b"test executable")
        (self.game / "GameAssembly.dll").write_bytes(b"test native binary")
        self.backups = self.root / "backups"

    def test_containment_rejects_escape_and_root(self):
        for target in (self.game, self.game / "../outside", self.root / "external"):
            with self.assertRaises(ValueError): core.contained(self.game, target)
        self.assertEqual(core.contained(self.game, self.game / "mods/a.dll"), self.game / "mods/a.dll")

    def test_backup_restores_original_and_checks_integrity(self):
        path = self.game / "BepInEx/config/a.cfg"
        path.parent.mkdir(parents=True)
        path.write_bytes(b"old\r\n# comment")
        transaction = core.backup_write(self.game, path, b"new", self.backups)
        with patch.object(core, "game_running", return_value=False):
            core.restore_backup(self.game, transaction, self.backups)
        self.assertEqual(path.read_bytes(), b"old\r\n# comment")

    def test_restore_rejects_newer_edit(self):
        path = self.game / "BepInEx/plugins/a.dll"
        transaction = core.backup_write(self.game, path, b"new", self.backups)
        path.write_bytes(b"user edit")
        with patch.object(core, "game_running", return_value=False), self.assertRaises(ValueError):
            core.restore_backup(self.game, transaction, self.backups)
        self.assertEqual(path.read_bytes(), b"user edit")

    def test_restore_of_new_file_removes_only_that_file(self):
        path = self.game / "BepInEx/plugins/new.dll"
        transaction = core.backup_write(self.game, path, b"new", self.backups)
        sentinel = path.parent / "untouched.dll"
        sentinel.write_bytes(b"sentinel")
        with patch.object(core, "game_running", return_value=False):
            core.restore_backup(self.game, transaction, self.backups)
        self.assertFalse(path.exists())
        self.assertEqual(sentinel.read_bytes(), b"sentinel")

    def test_tampered_receipt_cannot_escape_game(self):
        path = self.game / "BepInEx/plugins/a.dll"
        transaction = core.backup_write(self.game, path, b"new", self.backups)
        receipt = json.loads((transaction / "receipt.json").read_text())
        receipt["relative"] = "../outside"
        (transaction / "receipt.json").write_text(json.dumps(receipt))
        with patch.object(core, "game_running", return_value=False), self.assertRaises(ValueError):
            core.restore_backup(self.game, transaction, self.backups)

    def test_restore_rejects_corrupt_backup(self):
        path = self.game / "BepInEx/config/a.cfg"
        path.parent.mkdir(parents=True)
        path.write_bytes(b"old")
        transaction = core.backup_write(self.game, path, b"new", self.backups)
        (transaction / "previous").write_bytes(b"corrupt")
        with patch.object(core, "game_running", return_value=False), self.assertRaises(ValueError):
            core.restore_backup(self.game, transaction, self.backups)

    def test_config_roundtrip_preserves_comments_unknown_and_newlines(self):
        source = "# header\r\n[Audio]\r\n# help\r\nEnabled = false\r\nUnknown = Keep Me\r\n"
        result = core.update_cfg(source, {("Audio", "Enabled"): "true", ("UI", "HudScale"): "1.5"})
        self.assertEqual(result, source.replace("Enabled = false", "Enabled = true") + "\r\n[UI]\r\nHudScale = 1.5\r\n")
        self.assertEqual(core.update_cfg(result, {("Audio", "Enabled"): "true", ("UI", "HudScale"): "1.5"}), result)

    def test_config_adds_key_to_correct_section(self):
        result = core.update_cfg("[Audio]\nEnabled = true\n[Camera]\nEnabled = false\n", {("Audio", "MasterVolume"): "0.5"})
        self.assertLess(result.index("MasterVolume"), result.index("[Camera]"))

    def test_values_reject_nan_infinity_out_of_range_and_multiline(self):
        for value in ("nan", "inf", "-1", "5"):
            with self.assertRaises(ValueError): core.validate_settings({("Audio", "MasterVolume"): value})
        with self.assertRaises(ValueError): core.validate_settings({("UI", "CanvasNameFilter"): "HUD\n[Injected]"})
        with self.assertRaises(ValueError): core.validate_settings({("Physics", "Enabled"): "maybe"})

    def test_plugin_toggle_roundtrip_and_collision(self):
        path = self.game / "BepInEx/plugins/test.dll"
        path.parent.mkdir(parents=True)
        path.write_bytes(b"dll")
        with patch.object(core, "game_running", return_value=False):
            disabled = core.toggle_plugin(self.game, path)
            self.assertEqual(disabled.name, "test.dll.disabled")
            self.assertEqual(core.toggle_plugin(self.game, disabled), path)
            disabled.write_bytes(b"duplicate")
            with self.assertRaises(ValueError): core.toggle_plugin(self.game, path)

    def test_plugin_change_rejected_while_game_running(self):
        path = self.game / "BepInEx/plugins/test.dll"
        path.parent.mkdir(parents=True)
        path.write_bytes(b"dll")
        with patch.object(core, "game_running", return_value=True), self.assertRaises(ValueError):
            core.toggle_plugin(self.game, path)
        self.assertTrue(path.exists())

    def test_deployment_and_rollback_with_receipt(self):
        artifact = self.root / "artifact"
        artifact.mkdir()
        dll = artifact / "Example.dll"
        dll.write_bytes(b"mod")
        receipt = {"fingerprint": core.fingerprint(self.game), "references": {}, "dll": dll.name,
                   "dllSha256": core.sha256(dll)}
        core.write_json(artifact / "build.json", receipt)
        with patch.object(core, "game_running", return_value=False), patch.object(core, "ROOT", self.root):
            transaction = core.deploy_plugin(self.game, artifact)
            deployed = self.game / "BepInEx/plugins/TK2-Mod-Studio/Example.dll"
            self.assertEqual(deployed.read_bytes(), b"mod")
            core.restore_backup(self.game, transaction)
            self.assertFalse(deployed.exists())

    def test_deploy_rejects_updated_game_and_tampered_artifact(self):
        artifact = self.root / "artifact"
        artifact.mkdir()
        dll = artifact / "Example.dll"
        dll.write_bytes(b"mod")
        receipt = {"fingerprint": core.fingerprint(self.game), "references": {}, "dll": dll.name, "dllSha256": core.sha256(dll)}
        core.write_json(artifact / "build.json", receipt)
        (self.game / "GameAssembly.dll").write_bytes(b"updated")
        with patch.object(core, "game_running", return_value=False), self.assertRaises(ValueError):
            core.deploy_plugin(self.game, artifact)
        receipt["fingerprint"] = core.fingerprint(self.game)
        core.write_json(artifact / "build.json", receipt)
        dll.write_bytes(b"tampered")
        with patch.object(core, "game_running", return_value=False), self.assertRaises(ValueError):
            core.deploy_plugin(self.game, artifact)

    def test_index_handles_generics_and_last_type_byte_spans(self):
        source = "// Namespace: Test\npublic class Map<TKey, TValue> : Object // TypeDefIndex: 0\n{\n\tpublic int x; // 0x20\n}\n// Namespace: \npublic enum Mode // TypeDefIndex: 1\n{\n\tpublic const Mode A = 0;\n}\n"
        path = self.root / "dump.cs"
        path.write_text(source, encoding="utf-8")
        catalog = core.index_dump(path, self.root / "catalog.json")
        self.assertEqual(catalog["typeCount"], 2)
        self.assertEqual(catalog["types"][0]["name"], "Map<TKey, TValue>")
        self.assertNotIn("Mode", core.read_declaration(catalog, catalog["types"][0]))
        self.assertIn("const Mode A", core.read_declaration(catalog, catalog["types"][1]))

    def test_template_name_validation_and_readable_source(self):
        for name in ("../escape", "x", "1Test", "a-b"):
            with self.assertRaises(ValueError): core.create_mod(self.root, name)
        project = core.create_mod(self.root, "MyTestMod")
        self.assertTrue(project.exists())
        source = (project.parent / "Plugin.cs").read_text()
        self.assertIn('"local.tk2.mytestmod"', source)
        self.assertNotIn("HelloMod", source)
        with self.assertRaises(ValueError): core.create_mod(self.root, "MyTestMod")

    def test_catalog_assigns_assemblies_by_typedef_ranges(self):
        source = "// Image 0: Assembly-CSharp.dll - 0\n// Image 1: System.dll - 2\n// Namespace: \npublic class Foo // TypeDefIndex: 1\n{\n}\n// Namespace: System\npublic class Bar // TypeDefIndex: 2\n{\n}\n"
        path = self.root / "dump.cs"
        path.write_text(source)
        catalog = core.index_dump(path, self.root / "catalog.json")
        self.assertEqual([e["assembly"] for e in catalog["types"]], ["Assembly-CSharp.dll", "System.dll"])

    def test_native_index_keeps_overload_addresses_and_honors_limit(self):
        directory = self.root / "local/ghidra"
        directory.mkdir(parents=True)
        entries = [{"name": "HpBarController$$Hit", "address": address, "signature": "void Hit(int damage)"}
                   for address in ("180000010", "180000020")]
        (directory / "functions.jsonl").write_text("\n".join(json.dumps(e) for e in entries))
        with patch.object(core, "ROOT", self.root):
            count, matches = core.search_native_functions("hpbarcontroller", 1)
            self.assertEqual(count, 2)
            self.assertEqual(len(matches), 1)
            self.assertIn("Body not exported", core.native_description(matches[0]))

    def test_build_for_other_installation_is_rejected(self):
        artifact = self.root / "artifact"
        artifact.mkdir()
        receipt = {"fingerprint": core.fingerprint(self.game)}
        receipt["fingerprint"]["game"] = str(self.root / "other")
        core.write_json(artifact / "build.json", receipt)
        with patch.object(core, "game_running", return_value=False), self.assertRaises(ValueError):
            core.deploy_plugin(self.game, artifact)

    def test_cfg_controls_parse_bepinex_comments_and_validate_ranges(self):
        text = "[Physics]\n## Acceleration strength.\n# Setting type: Single\n# Default value: 100\n# Acceptable value range: From 0 to 500\nAcceleration = 100\n\n[General]\n# Setting type: Boolean\nEnabled = false\n"
        entries = core.parse_cfg_settings(text)
        self.assertEqual(len(entries), 2)
        self.assertEqual(entries[0]["description"], "Acceleration strength.")
        self.assertEqual(core.validate_cfg_value(entries[0], "42.5"), "42.5")
        with self.assertRaises(ValueError): core.validate_cfg_value(entries[0], "501")
        self.assertEqual(core.validate_cfg_value(entries[1], "TRUE"), "true")

    def test_cfg_controls_validate_enum_and_integer_bounds(self):
        entries = core.parse_cfg_settings("[Input]\n# Setting type: KeyCode\n# Acceptable values: None, A, B\nKey = None\n# Setting type: Int32\nCount = 3\n")
        with self.assertRaises(ValueError): core.validate_cfg_value(entries[0], "C")
        with self.assertRaises(ValueError): core.validate_cfg_value(entries[1], str(2**32))
        self.assertEqual(core.validate_cfg_value(entries[0], "A"), "A")


if __name__ == "__main__": unittest.main()
