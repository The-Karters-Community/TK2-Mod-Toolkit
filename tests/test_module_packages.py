import hashlib
import json
from pathlib import Path
import stat
import tempfile
import unittest
from unittest.mock import patch
import zipfile

from studio import core, module_packages as packages, pack


RECIPE = '''using BepInEx.Configuration;
using UnityEngine;
namespace TK2.Customization;
public sealed class TestHop : IModRecipe {
 public string Name => "TestHop";
 public bool ChangesGameplay => true;
 public void Configure(ConfigFile config) {
  config.Bind("Recipe." + Name, "Strength", 6f, new ConfigDescription("Hop strength.", new AcceptableValueRange<float>(0, 30)));
  config.Bind("Recipe." + Name, "Shortcut", KeyCode.F9, "Shortcut.");
 }
 public void Tick() {} public void Restore() {}
}'''


class ModulePackagesTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.sources = self.root / "plugins/TK2.Customization"
        self.sources.joinpath("Recipes").mkdir(parents=True)
        self.file = self.sources / "Recipes/TestHop.cs"
        self.file.write_text(RECIPE, encoding="utf-8")
        self.patchers = [patch.object(core, "ROOT", self.root), patch.object(pack, "SOURCE_ROOT", self.sources)]
        for patcher in self.patchers: patcher.start()
        self.addCleanup(self.temporary.cleanup)
        for patcher in self.patchers: self.addCleanup(patcher.stop)

    def exported(self, values=None):
        result = packages.export_package(["Recipe.TestHop"], "My hop", values or {})
        return packages.download_path(result["id"])

    def rewrite(self, path, mutate=None, extra=None):
        with zipfile.ZipFile(path) as archive: data = {entry.filename: archive.read(entry) for entry in archive.infolist()}
        manifest = json.loads(data["manifest.json"])
        if mutate: mutate(manifest, data)
        data["manifest.json"] = json.dumps(manifest).encode()
        with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as archive:
            for name, value in data.items(): archive.writestr(name, value)
            if extra: archive.writestr(*extra)

    def test_selected_sources_settings_and_safe_disabled_import(self):
        path = self.exported({"Recipe.TestHop/Strength": 12, "Recipe.TestHop/Enabled": True, "Audio/MasterVolume": .2})
        with zipfile.ZipFile(path) as archive:
            manifest = json.loads(archive.read("manifest.json"))
            self.assertEqual([entry["id"] for entry in manifest["modules"]], ["Recipe.TestHop"])
            self.assertNotIn("Audio/MasterVolume", manifest["settings"])
            self.assertEqual(archive.namelist(), ["manifest.json", "plugins/TK2.Customization/Recipes/TestHop.cs"])
        preview = packages.preview_import(path)
        self.assertEqual(preview["conflicts"], [])
        result = packages.import_package(path, preview["hash"])
        self.assertEqual(result["settings"]["Recipe.TestHop/Strength"], 12)
        self.assertFalse(result["settings"]["Recipe.TestHop/Enabled"])
        self.assertFalse(result["requiresBuild"])
        self.assertFalse((self.root / "BepInEx/config").exists())

    def test_conflict_requires_explicit_replace_and_backs_up(self):
        path = self.exported()
        edited = RECIPE + "\n// my later local edit"
        self.file.write_text(edited)
        preview = packages.preview_import(path)
        self.assertEqual(preview["conflicts"], ["plugins/TK2.Customization/Recipes/TestHop.cs"])
        with self.assertRaisesRegex(ValueError, "explicitly allow replacement"):
            packages.import_package(path, preview["hash"])
        self.assertEqual(self.file.read_text(), edited)
        result = packages.import_package(path, preview["hash"], replace=True)
        self.assertEqual(self.file.read_text(), RECIPE)
        self.assertEqual((Path(result["backup"]) / "plugins/TK2.Customization/Recipes/TestHop.cs").read_text(), edited)
        self.assertTrue(result["requiresBuild"])

    def test_new_source_import(self):
        path = self.exported(); self.file.unlink()
        preview = packages.preview_import(path)
        result = packages.import_package(path, preview["hash"])
        self.assertEqual(self.file.read_text(), RECIPE)
        self.assertTrue(result["requiresBuild"])

    def test_package_change_after_preview_is_rejected(self):
        path = self.exported(); preview = packages.preview_import(path)
        self.rewrite(path, lambda manifest, data: manifest.update(name="Changed"))
        with self.assertRaisesRegex(ValueError, "changed after preview"):
            packages.import_package(path, preview["hash"])

    def test_tampered_checksum_rejected(self):
        path = self.exported()
        self.rewrite(path, lambda manifest, data: data.update({"plugins/TK2.Customization/Recipes/TestHop.cs": b"tampered"}))
        with self.assertRaisesRegex(ValueError, "checksum mismatch"): packages.preview_import(path)

    def test_unselected_settings_rejected(self):
        path = self.exported()
        self.rewrite(path, lambda manifest, data: manifest["settings"].update({"Audio/MasterVolume": .2}))
        with self.assertRaisesRegex(ValueError, "unselected"): packages.preview_import(path)

    def test_recipe_ranges_come_from_source_not_manifest(self):
        path = self.exported()
        def mutate(manifest, data):
            manifest["modules"][0]["schema"]["Strength"]["high"] = 1000
            manifest["settings"]["Recipe.TestHop/Strength"] = 1000
        self.rewrite(path, mutate)
        with self.assertRaisesRegex(ValueError, "allowed range"): packages.preview_import(path)

    def test_traversal_absolute_ads_reserved_and_unsafe_extensions(self):
        bad = ["../evil.cs", "C:/evil.cs", "/evil.cs", "plugins/TK2.Customization/Recipes/A.cs:payload", "plugins/TK2.Customization/Recipes/CON.cs", "plugins/TK2.Customization/evil.dll", "plugins\\TK2.Customization\\evil.cs", "plugins/TK2.Customization/Recipes/trailing .cs."]
        for name in bad:
            with self.subTest(name=name):
                path = self.exported(); self.rewrite(path, extra=(name, b"bad"))
                with self.assertRaises(ValueError): packages.preview_import(path)

    def test_case_collision_and_duplicate_paths_rejected(self):
        for extra in ["plugins/TK2.Customization/Recipes/testhop.cs", "plugins/TK2.Customization/Recipes/TestHop.cs", "plugins/tk2.customization/Recipes/Other.cs"]:
            with self.subTest(extra=extra):
                path = self.exported()
                import warnings
                with warnings.catch_warnings(): warnings.simplefilter("ignore"); self.rewrite(path, extra=(extra, b"bad"))
                with self.assertRaises(ValueError): packages.preview_import(path)

    def test_zip_symlink_rejected(self):
        path = self.exported()
        entry = zipfile.ZipInfo("plugins/TK2.Customization/Recipes/Link.cs")
        entry.create_system = 3; entry.external_attr = (stat.S_IFLNK | 0o777) << 16
        with zipfile.ZipFile(path, "a") as archive: archive.writestr(entry, "/etc/passwd")
        with self.assertRaisesRegex(ValueError, "Links"): packages.preview_import(path)

    def test_zipbomb_ratio_rejected(self):
        path = self.exported(); self.rewrite(path, extra=("plugins/TK2.Customization/Recipes/Bomb.cs", b"A" * (2 * 1024 * 1024)))
        with self.assertRaisesRegex(ValueError, "expands"): packages.preview_import(path)

    def test_download_id_does_not_allow_arbitrary_paths(self):
        for name in ("../README.md", "C:/Windows/win.ini", "ABC", "0" * 32):
            with self.subTest(name=name), self.assertRaises(ValueError): packages.download_path(name)

    def test_invalid_config_value_is_rejected_on_export(self):
        for value in (float("nan"), float("inf"), 31, True, "6"):
            with self.subTest(value=value), self.assertRaises(ValueError): self.exported({"Recipe.TestHop/Strength": value})

    def test_removed_modules_and_assets_are_rejected(self):
        for name in ('models/kart/model.obj', 'plugins/TK2.Customization/Models/CosmeticModel.cs', 'plugins/TK2.Customization/Recipes/AirGlider.cs'):
            path = self.exported(); self.rewrite(path, extra=(name,b'fixture'))
            with self.subTest(name=name), self.assertRaises(ValueError): packages.preview_import(path)
        with self.assertRaises(ValueError): packages.export_package(['Recipe.AirGlider'],'Removed',{})

    def test_shared_local_helper_export_and_rollback_on_write_failure(self):
        helper = self.sources / "Recipes/Helper.cs"
        helper.write_text("internal static class Helper { internal static void Act() {} }")
        self.file.write_text(RECIPE.replace("public void Tick() {}", "public void Tick() { Helper.Act(); }"))
        path = self.exported()
        with zipfile.ZipFile(path) as archive: self.assertIn("plugins/TK2.Customization/Recipes/Helper.cs", archive.namelist())
        self.file.write_text("// local recipe edit"); helper.write_text("// local helper edit")
        preview = packages.preview_import(path)
        original_atomic_write = core.atomic_write
        def failing_write(target, data):
            if target == self.file and data != b"// local recipe edit": raise OSError("simulated source write failure")
            original_atomic_write(target, data)
        with patch.object(core, "atomic_write", side_effect=failing_write), self.assertRaises(OSError):
            packages.import_package(path, preview["hash"], replace=True)
        self.assertEqual(helper.read_text(), "// local helper edit")
        self.assertEqual(self.file.read_text(), "// local recipe edit")

    def test_package_recipe_name_must_match_source(self):
        path = self.exported()
        def mutate(manifest, data):
            manifest["modules"][0]["id"] = "Recipe.Fake"
        self.rewrite(path, mutate)
        with self.assertRaisesRegex(ValueError, "does not match any packaged source"): packages.preview_import(path)

    def test_invalid_model_dependency_rejected_before_source_write(self):
        path = self.exported()
        bad = b'mtllib ../outside.mtl\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3'
        name = 'models/kart/model.obj'
        def mutate(manifest, data):
            data[name] = bad
            manifest['files'].append({'path':name,'kind':'model','size':len(bad),'sha256':hashlib.sha256(bad).hexdigest()})
        self.rewrite(path,mutate)
        self.file.write_text('// local change')
        with self.assertRaises(ValueError): packages.preview_import(path)
        self.assertEqual(self.file.read_text(),'// local change')
        self.assertFalse((self.root / 'local/module-assets').exists())


if __name__ == "__main__": unittest.main()
