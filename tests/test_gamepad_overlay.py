import unittest

from studio import pack


class GamepadOverlayCatalogTests(unittest.TestCase):
    def test_module_is_available_as_a_presentation_feature(self):
        feature = next(item for item in pack.catalog_features() if item["id"] == "GamepadOverlay")
        self.assertEqual(feature["category"], "Interface")
        self.assertIn("Rewired", feature["description"])
        self.assertFalse(pack.defaults()["GamepadOverlay/Enabled"])
        self.assertTrue(pack.defaults()["GamepadOverlay/StartVisible"])
        self.assertTrue(pack.defaults()["GamepadOverlay/ShowReplayInputs"])
        self.assertEqual(pack.defaults()["GamepadOverlay/ToggleKey"], "F9")
        self.assertEqual(pack.defaults()["GamepadOverlay/Corner"], "TopCenter")

    def test_catalog_exposes_safe_controls_before_first_plugin_launch(self):
        validated = pack.validate({
            "GamepadOverlay/ToggleKey": "F10",
            "GamepadOverlay/Corner": "BottomRight",
            "GamepadOverlay/Scale": 1.2,
            "GamepadOverlay/Opacity": .7,
        })
        self.assertEqual(validated[("GamepadOverlay", "ToggleKey")], "F10")
        self.assertEqual(validated[("GamepadOverlay", "Corner")], "BottomRight")
        with self.assertRaises(ValueError):
            pack.validate({"GamepadOverlay/Corner": "Unknown"})

    def test_module_package_includes_input_model_and_runtime_owner(self):
        self.assertEqual(set(pack.module_sources()["GamepadOverlay"]), {
            "plugins/TK2.Customization/GamepadOverlay.cs",
            "plugins/TK2.Customization/InputOverlayModel.cs",
            "plugins/TK2.Customization/StudioBehaviour.cs",
            "plugins/TK2.Customization/Plugin.cs",
        })


if __name__ == "__main__":
    unittest.main()
