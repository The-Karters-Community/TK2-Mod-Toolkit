"""Antigravity must remain opt-in, protected gameplay functionality."""
import unittest

from studio import module_packages, pack


class AntigravityIntegrationTests(unittest.TestCase):
    def test_module_is_opt_in_gameplay_and_has_one_pack_membership(self):
        identity = "Antigravity"
        defaults = pack.defaults()
        self.assertIs(defaults[identity + "/Enabled"], False)
        self.assertIs(defaults[identity + "/CameraFollowsSurface"], False)

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
        lifecycle = (pack.PROJECT.parent / "AntigravityLifecycle.cs").read_text(encoding="utf-8")
        callback = lifecycle.split("private static void AfterCharacterUpdate(", 1)[1].split(
            "private static void BeforeCharacterUpdate(", 1
        )[0]
        self.assertLess(callback.index("IsLocalRacer(__instance)"), callback.index("GetInstanceID()"))
        self.assertLess(callback.index("AntigravityEnabled.Value"), callback.index("States.TryGetValue"))
        self.assertLess(callback.index("RacerRunning(__instance)"), callback.index("States.TryGetValue"))
        self.assertIn("Release(id, true)", callback)
        self.assertIn("internal static FeatureHook[] Hooks()", source)
        self.assertIn("FeatureHook.Install(plugin.Harmony, typeof(AntigravityFeature), Hooks())", source)
        self.assertNotIn("AccessTools.FieldRef", source)
        self.assertNotIn("AccessTools.Field(", source)
        surface = (pack.PROJECT.parent / "AntigravitySurfaceMode.cs").read_text(encoding="utf-8")
        self.assertIn("SessionModified = true;", surface)

    def test_velocity_hooks_use_native_movement_boundaries(self):
        source = (pack.PROJECT.parent / "AntigravityFeature.cs").read_text(encoding="utf-8")
        hooks = source.split("internal static FeatureHook[] Hooks()", 1)[1].split("};", 1)[0]
        self.assertIn('typeof(PixelKartPhysics), "UpdateVelocity"', hooks)
        self.assertIn("KinematicCharacterController_ICharacterController_UpdateVelocity", hooks)
        self.assertNotIn('"UpdateGravityWhenNotGrounded"', hooks)
        self.assertIn('"UpdateSlopeVelocity"', hooks)
        self.assertIn("Prefix: nameof(SlopePrefix)", hooks)
        self.assertNotIn('"LimitVerticalVelocity"', hooks)
        self.assertIn("MainVelocityPrefix", source)
        self.assertIn("MainVelocityFinalizer", source)

    def test_catalog_describes_shared_local_surface_driving_and_exclusions(self):
        feature = next(feature for feature in pack.catalog_features() if feature["id"] == "Antigravity")
        description = feature["description"].lower()
        for phrase in ("offline", "human and ai", "static road and wall", "respawn", "triggers", "racer colliders"):
            self.assertIn(phrase, description)
        guards = (pack.PROJECT.parent / "AntigravityStateAccess.cs").read_text(encoding="utf-8")
        self.assertIn("E_HUMAN_LOCAL", guards)
        self.assertIn("E_AI_LOCAL", guards)
        running = guards.split("internal static bool RacerRunning(", 1)[1].split("private static void UpdateCameraEngagement", 1)[0]
        for condition in ("Plugin.OfflineLabAllowed", "Time.timeScale > 0f", "MenuManager.Instance == null",
                          "E_RACE_RUNNING", "IsPlayingReplayData()", "bIsPlayerRespawning", "IsTeleportingFromAtoB()", "bIsInTankForm"):
            self.assertIn(condition, running)

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
