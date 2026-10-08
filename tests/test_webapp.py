import hashlib
from http.client import HTTPConnection
import json
from pathlib import Path
import tempfile
import threading
import unittest
from unittest.mock import patch
from studio import core, pack, webapp


class GarageTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.game = self.root / "game"
        self.game.mkdir()
        for name in ("TheKarters2.exe", "GameAssembly.dll"): (self.game / name).write_bytes(b"fixture")
        self.sources = self.root / "plugins/TK2.Customization"
        self.sources.mkdir(parents=True)
        (self.sources / "Example.cs").write_text("// original source")
        (self.root / "src/Reconstructed").mkdir(parents=True)
        (self.root / "templates").mkdir()
        (self.root / "templates/PackRecipe.cs.txt").write_text("public class RecipeName {}")
        for target, value in (("ROOT", self.root),):
            p = patch.object(core, target, value); p.start(); self.addCleanup(p.stop)
        p = patch.object(pack, "SOURCE_ROOT", self.sources); p.start(); self.addCleanup(p.stop)
        self.app = webapp.Application(self.game)

    def test_pack_defaults_allow_requested_offline_test_toggles(self):
        self.assertGreaterEqual(len(pack.FEATURES), 25)
        values = pack.defaults()
        self.assertTrue(all(not v for k, v in values.items() if k.endswith("/Enabled")))
        for feature in pack.FEATURES:
            if feature.get("gameplay"):
                self.assertEqual(pack.validate({feature["id"] + "/Enabled": True})[(feature["id"],"Enabled")], "true")
        for value in (float('nan'), float('inf'), -1, 3):
            with self.assertRaises(ValueError): pack.validate({"Audio/MasterVolume": value})
        with self.assertRaises(ValueError): pack.validate({"Laps/Count": 1.5})

    def test_config_save_preserves_unknown_and_detects_external_edit(self):
        path = self.app.config_path
        path.parent.mkdir(parents=True)
        path.write_bytes(b"# preserve\r\n[Audio]\r\nEnabled = false\r\nUnknown = original\r\n")
        digest = self.app.config_data()["configHash"]
        self.app.save_settings({"hash": digest, "values": {"Audio/Enabled": True}})
        self.assertIn(b"Unknown = original\r\n", path.read_bytes())
        self.assertIn(b"Enabled = true", path.read_bytes())
        with self.assertRaises(ValueError): self.app.save_settings({"hash": digest, "values": {"Audio/Enabled": False}})
        self.assertEqual(len(list((self.root / "local/backups").glob("*/receipt.json"))), 1)

    def test_live_setting_save_never_builds_deploys_or_requires_closed_game(self):
        plugin = self.game / "BepInEx/plugins/TK2-Mod-Studio/TK2.Customization.dll"
        plugin.parent.mkdir(parents=True)
        plugin.write_bytes(b"running-plugin-unchanged")
        digest = core.sha256(plugin)
        self.app.config_path.parent.mkdir(parents=True)
        self.app.config_path.write_text("[Camera]\nEnabled = false\nFieldOfView = 65\n", encoding="utf-8")
        with patch.object(core, "build_plugin") as build, patch.object(core, "deploy_plugin") as deploy, patch.object(core, "require_game_stopped", side_effect=AssertionError("settings must work with a running game")) as stopped:
            for edits in ({"Camera/Enabled": True}, {"Camera/FieldOfView": 80}, {"Camera/Enabled": False}):
                state = self.app.config_data()
                self.app.action("settings", {"hash": state["configHash"], "values": edits, "baseValues": {key:state["settings"][key] for key in edits}})
        build.assert_not_called(); deploy.assert_not_called(); stopped.assert_not_called()
        self.assertEqual(core.sha256(plugin), digest)
        self.assertEqual(self.app.config_data()["settings"]["Camera/FieldOfView"], 80)

    def test_source_edit_conflict_and_backup(self):
        name = "plugins/TK2.Customization/Example.cs"
        p = self.sources / "Example.cs"
        original = core.sha256(p)
        self.app.action("save-source", {"file": name, "hash": original, "content": "// new source"})
        backup = next((self.root / "local/source-backups").glob("*"))
        self.assertEqual(backup.read_text(), "// original source")
        p.write_text("// external edit")
        with self.assertRaises(ValueError): self.app.action("save-source", {"file": name, "hash": original, "content": "// overwrite"})
        self.assertEqual(p.read_text(), "// external edit")
        with self.assertRaises(ValueError): pack.source_path("../../external.cs")

    def test_recipe_is_a_source_in_same_pack(self):
        name = pack.create_recipe("MyCamera")
        self.assertIn(name, pack.source_files())
        self.assertEqual(pack.source_path(name).read_text(), "public class MyCamera {}")
        with self.assertRaises(ValueError): pack.create_recipe("../outside")
        with self.assertRaises(ValueError): pack.create_recipe("MyCamera")

    def test_module_navigation_exposes_only_existing_editable_files(self):
        (self.sources / 'CameraFeature.cs').write_text('// camera behavior')
        (self.sources / 'CameraSettings.cs').write_text('// camera settings')
        files = pack.module_sources()['Camera']
        self.assertEqual(files, ['plugins/TK2.Customization/CameraFeature.cs', 'plugins/TK2.Customization/CameraSettings.cs'])
        self.assertTrue(all(pack.source_path(file).is_file() for file in files))
        self.assertEqual(pack.module_sources()['Audio'], [])

    def test_open_source_folder_validates_path_and_never_opens_a_binary(self):
        with patch.object(webapp.os, 'startfile') as opened:
            self.app.action('open-source-folder', {'file': 'plugins/TK2.Customization/Example.cs'})
            opened.assert_called_once_with(self.sources)
            for file in ('../../outside.cs', 'plugins/TK2.Customization/Example.dll'):
                with self.assertRaises(ValueError): self.app.action('open-source-folder', {'file': file})

    def test_failed_author_build_keeps_installed_plugin(self):
        with patch.object(core, 'require_game_stopped'), patch.object(core, 'build_plugin', side_effect=RuntimeError('Build failed')), patch.object(core, 'deploy_plugin') as deploy:
            with self.assertRaises(RuntimeError): self.app.action('build-install', {})
            deploy.assert_not_called()

    def test_install_builds_before_deploy_and_keeps_defaults_off(self):
        with patch.object(core, "require_game_stopped"), patch.object(core, "build_plugin", return_value={"artifact": str(self.root / "artifact")}) as build, patch.object(core, "deploy_plugin", return_value=self.root / "backup") as deploy:
            result = self.app.action("build-install", {})
        build.assert_called_once(); deploy.assert_called_once()
        self.assertIn("One pack", result["message"])
        self.assertTrue(all(not value for key, value in self.app.config_data()["settings"].items() if key.endswith("/Enabled")))

    def test_only_one_garage_server_can_own_a_port(self):
        server = webapp.Server(self.app, 0)
        self.addCleanup(server.server_close)
        with self.assertRaises(OSError):
            duplicate = webapp.Server(self.app, server.server_port)
            duplicate.server_close()

    def test_http_api_rejects_cross_origin_missing_token_and_wrong_host(self):
        server = webapp.Server(self.app, 0)
        thread = threading.Thread(target=server.serve_forever, daemon=True); thread.start()
        self.addCleanup(server.server_close)
        self.addCleanup(server.shutdown)
        def request(method, path, headers=None, body=None):
            connection = HTTPConnection("127.0.0.1", server.server_port)
            connection.request(method, path, body, headers or {})
            response = connection.getresponse(); result = response.status, response.read(); connection.close(); return result
        self.assertEqual(request("GET", "/api/state")[0], 403)
        auth = {"X-TK2-Token": self.app.token}
        self.assertEqual(request("GET", "/api/state", auth)[0], 200)
        self.assertEqual(request("GET", "/api/state", {**auth, "Origin": "https://example.com"})[0], 403)
        self.assertEqual(request("GET", "/", {"Host": "example.com"})[0], 403)
        status, html = request("GET", "/")
        self.assertEqual(status, 200); self.assertIn(self.app.token.encode(), html); self.assertNotIn(b"__SESSION_TOKEN__", html)
        bad = json.dumps({"values": {"Audio/Enabled": True}, "hash": "stale"})
        self.assertEqual(request("POST", "/api/settings", {**auth, "Content-Type": "application/json"}, bad)[0], 409)
        self.assertFalse(self.app.config_path.exists())
        self.assertEqual(request("GET", "/api/source?file=../../outside.cs", auth)[0], 400)
        self.assertEqual(request("GET", "/api/source?file=plugins/TK2.Customization/Example.cs", auth)[0], 200)


if __name__ == "__main__": unittest.main()
