"""Antigravity must remain opt-in, protected gameplay functionality."""
import unittest

from studio import module_packages, pack


class AntigravityIntegrationTests(unittest.TestCase):
    def test_module_is_opt_in_gameplay_and_has_one_pack_membership(self):
        identity = "Antigravity"
        defaults = pack.defaults()
        self.assertIs(defaults[identity + "/Enabled"], False)

        feature = next(feature for feature in pack.catalog_features() if feature["id"] == identity)
        self.assertTrue(feature["gameplay"])
        self.assertEqual(
            sum(pack_item["features"].count(identity) for pack_item in pack.catalog_packs()),
            1,
        )

    def test_antigravity_export_contains_policy_and_runtime_helpers(self):
        feature = next(feature for feature in pack.catalog_features() if feature["id"] == "Antigravity")
        sources = module_packages._source_dependencies([feature])
        self.assertIn("plugins/TK2.Customization/AntigravityPolicy.cs", sources)
        self.assertIn("plugins/TK2.Customization/AntigravityFeature.cs", sources)

    def test_runtime_adapter_guards_before_state_lookup_and_resolves_named_hooks(self):
        feature_path = pack.PROJECT.parent / "AntigravityFeature.cs"
        source = feature_path.read_text(encoding="utf-8")
        callback = source.split("private static void AfterCharacterUpdate(", 1)[1].split(
            "private static bool TryContinuityProbe(", 1
        )[0]
        self.assertLess(callback.index("AntigravityEnabled.Value"), callback.index("GetInstanceID()"))
        self.assertLess(callback.index("AirShiftFeature.Running(__instance)"), callback.index("GetInstanceID()"))
        self.assertIn("internal static FeatureHook[] Hooks()", source)
        self.assertIn("FeatureHook.Install(plugin.Harmony, typeof(AntigravityFeature), Hooks())", source)
        self.assertNotIn("AccessTools.FieldRef", source)
        self.assertNotIn("AccessTools.Field(", source)
        self.assertIn("plugin.SessionModified = true;", source)

    def test_shared_runtime_guard_covers_non_race_and_recovery_states(self):
        source = (pack.PROJECT.parent / "AirShiftFeature.cs").read_text(encoding="utf-8")
        guard = source.split("private static string? BlockReason(", 1)[1].split("private static Dash State(", 1)[0]
        for condition in (
            "Plugin.OfflineLabAllowed", "E_HUMAN_LOCAL", "Time.timeScale <= 0",
            "MenuManager.Instance != null", "E_RACE_RUNNING", "IsPlayingReplayData()",
            "bIsPlayerRespawning", "IsTeleportingFromAtoB()",
        ):
            with self.subTest(condition=condition):
                self.assertIn(condition, guard)

        feature_source = (pack.PROJECT.parent / "AntigravityFeature.cs").read_text(encoding="utf-8")
        hooks = feature_source.split("internal static FeatureHook[] Hooks()", 1)[1].split("};", 1)[0]
        for forbidden in ("Checkpoint", "LapTime", "Replay", "Route", "RaceClock"):
            with self.subTest(forbidden=forbidden):
                self.assertNotIn(forbidden, hooks)


if __name__ == "__main__":
    unittest.main()
