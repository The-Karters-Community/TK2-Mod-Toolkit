import hashlib
import unittest
from studio import pack, settings


class SettingsMergeTests(unittest.TestCase):
    def save(self, data, values, baseline, **kwargs):
        return settings.merge(data, {"values": values, "baseValues": baseline,
                                    "hash": "old", **kwargs})

    def test_game_comment_rewrite_is_not_a_player_conflict(self):
        data = b"## Rewritten by BepInEx\r\n[Camera]\r\nFieldOfView = 65.000\r\nEnabled = true\r\n"
        changed = self.save(data, {"Camera/FieldOfView": 70}, {"Camera/FieldOfView": 65})
        self.assertIn(b"FieldOfView = 70\r\n", changed)
        self.assertIn(b"## Rewritten by BepInEx", changed)

    def test_two_windows_merge_disjoint_settings(self):
        data = b"[Audio]\nEnabled = true\n[Camera]\nFieldOfView = 75\n"
        changed = self.save(data, {"Audio/MasterVolume": .4}, {"Audio/MasterVolume": 1})
        self.assertEqual(settings.read(changed)["settings"]["Camera/FieldOfView"], 75)
        self.assertEqual(settings.read(changed)["settings"]["Audio/MasterVolume"], .4)

    def test_same_key_conflict_is_precise(self):
        data = b"[Camera]\nFieldOfView = 75\n"
        with self.assertRaises(settings.SettingsConflict) as caught:
            self.save(data, {"Camera/FieldOfView": 80, "Audio/Enabled": True},
                      {"Camera/FieldOfView": 65, "Audio/Enabled": False})
        self.assertEqual(caught.exception.keys, ["Camera/FieldOfView"])

    def test_game_already_has_requested_value_is_idempotent(self):
        data = b"[Camera]\nFieldOfView = 75\n"
        self.assertEqual(self.save(data, {"Camera/FieldOfView": 75}, {"Camera/FieldOfView": 65}), data)

    def test_dirty_only_validation_allows_untouched_bad_values(self):
        data = b"[Camera]\nFieldOfView = 20\n[Audio]\nEnabled = false\n"
        changed = self.save(data, {"Audio/Enabled": True}, {"Audio/Enabled": False})
        self.assertEqual(settings.read(changed)["settings"]["Camera/FieldOfView"], 20)

    def test_recipes_extra_settings_use_semantic_baselines(self):
        data = b"[Recipe.Example]\n# Setting type: Int32\nCount = 3\n[Other]\n# Setting type: Boolean\nEnabled = false\n"
        changed = settings.merge(data, {"hash": "old", "recipes": {"Recipe.Example/Count": 5},
            "baseRecipes": {"Recipe.Example/Count": "3"}, "extraSettings": {"Other/Enabled": True},
            "baseExtraSettings": {"Other/Enabled": False}})
        self.assertIn(b"Count = 5", changed)
        self.assertIn(b"Enabled = true", changed)

    def test_catalogs_seed_all_controls_without_resetting_player(self):
        original = "# player\n[Camera]\nFieldOfView = 36\nEnabled = true\n"
        seeded = pack.seed_config(original)
        values = settings.read(seeded.encode())["settings"]
        self.assertEqual(values["Camera/FieldOfView"], 36)
        self.assertTrue(values["Camera/Enabled"])
        self.assertTrue(values["Camera/PreserveKartFraming"])
        self.assertFalse(values["CommunityCommands/Enabled"])
        self.assertEqual(pack.seed_config(seeded), seeded)
        self.assertEqual(len({f["id"] for f in pack.FEATURES}), len(pack.FEATURES))
        self.assertEqual({f for p in pack.PACKS for f in p["features"]}, {f["id"] for f in pack.FEATURES})

    def test_nonfinite_external_config_does_not_break_json_state(self):
        import json
        data = settings.read(b"[Camera]\nFieldOfView = NaN\n")
        self.assertEqual(data["settings"]["Camera/FieldOfView"], "NaN")
        json.dumps(data, allow_nan=False)

    def test_empty_channel_fields_can_be_saved_and_invalid_bool_rejected(self):
        self.assertEqual(pack.validate({"CommunityCommands/Channel": ""}), {("CommunityCommands", "Channel"): ""})
        with self.assertRaises(ValueError): pack.validate({"Camera/PreserveKartFraming": "true"})

    def test_performance_controls_seed_disabled_and_validate_ai_interval(self):
        original = "[Performance]\nEnabled = true\nAIPhysicsInterval = 3\n"
        seeded = pack.seed_config(original)
        values = settings.read(seeded.encode())["settings"]
        self.assertTrue(values["Performance/Enabled"])
        self.assertEqual(values["Performance/AIPhysicsInterval"], 3)
        self.assertTrue(values["Performance/CacheDrawDistance"])
        self.assertTrue(values["Performance/RaceGarbageCollection"])
        self.assertFalse(values["Performance/LowerAIPhysics"])
        self.assertFalse(pack.defaults()["Performance/Enabled"])
        self.assertEqual(pack.seed_config(seeded), seeded)
        for invalid in (0, 5, 1.5, True):
            with self.subTest(interval=invalid), self.assertRaises(ValueError):
                pack.validate({"Performance/AIPhysicsInterval": invalid})
        for interval in (1, 4):
            self.assertEqual(pack.validate({"Performance/AIPhysicsInterval": interval}),
                             {("Performance", "AIPhysicsInterval"): str(interval)})

    def test_diagnostics_independent_defaults_and_bounds(self):
        data = settings.read(pack.seed_config("[Performance]\nEnabled = false\n").encode())["settings"]
        self.assertFalse(data["Performance/Enabled"])
        self.assertFalse(data["PerformanceDiagnostics/Enabled"])
        self.assertEqual(data["PerformanceDiagnostics/CaptureSeconds"], 60)
        for value in (9, 181, True, 10.5):
            with self.subTest(value=value), self.assertRaises(ValueError):
                pack.validate({"PerformanceDiagnostics/CaptureSeconds": value})
        self.assertEqual(pack.validate({"PerformanceDiagnostics/CaptureSeconds": 10}),
                         {("PerformanceDiagnostics", "CaptureSeconds"): "10"})

    def test_diagnostics_catalog_and_source_ownership(self):
        feature = next(f for f in pack.catalog_features() if f["id"] == "PerformanceDiagnostics")
        self.assertFalse(feature.get("gameplay", False))
        self.assertIn("PerformanceDiagnostics", next(p for p in pack.catalog_packs() if p["id"] == "garage")["features"])
        self.assertEqual(set(pack.module_sources()["PerformanceDiagnostics"]), {
            "plugins/TK2.Customization/PerformanceDiagnostics.cs", "plugins/TK2.Customization/PerformanceSamples.cs",
            "plugins/TK2.Customization/PerformanceFeature.cs", "plugins/TK2.Customization/PerformanceEngineSamples.cs"})

    def test_track_boundaries_settings_and_export_ownership(self):
        self.assertFalse(pack.defaults()["TrackBoundaries/Enabled"])
        self.assertTrue(pack.defaults()["TrackBoundaries/ShowWalls"])
        self.assertTrue(pack.defaults()["TrackBoundaries/ShowRespawn"])
        self.assertEqual(pack.defaults()["TrackBoundaries/ToggleKey"], "F10")
        self.assertEqual(sum(p["features"].count("TrackBoundaries") for p in pack.catalog_packs()), 1)
        self.assertEqual(set(pack.module_sources()["TrackBoundaries"]), {
            "plugins/TK2.Customization/TrackBoundaries.cs", "plugins/TK2.Customization/BoundarySelection.cs",
            "plugins/TK2.Customization/BoundaryGeometry.cs", "plugins/TK2.Customization/StudioBehaviour.cs"})
        self.assertEqual(pack.validate({"TrackBoundaries/DrawDistance": 1000}), {("TrackBoundaries", "DrawDistance"): "1000"})
        for value in (24, 1001, True):
            with self.subTest(value=value), self.assertRaises(ValueError):
                pack.validate({"TrackBoundaries/DrawDistance": value})
        with self.assertRaises(ValueError): pack.validate({"TrackBoundaries/ToggleKey": "Invalid"})

    def test_performance_module_has_pack_membership_and_source_ownership(self):
        from unittest.mock import patch
        feature = next(f for f in pack.catalog_features() if f["id"] == "Performance")
        self.assertEqual(feature["name"], "Race performance")
        self.assertTrue(feature["gameplay"])
        essentials = next(p for p in pack.catalog_packs() if p["id"] == "garage")
        self.assertIn("Performance", essentials["features"])
        source = "plugins/TK2.Customization/PerformanceFeature.cs"
        with patch.object(pack, "source_files", return_value=[source]):
            self.assertEqual(pack.module_sources()["Performance"], [source])


if __name__ == "__main__": unittest.main()
