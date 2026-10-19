import unittest
from studio import pack, module_packages


class AerialPracticeIntegrationTests(unittest.TestCase):
    def test_modules_are_opt_in_and_in_exactly_one_pack(self):
        defaults = pack.defaults()
        self.assertNotIn('RicochetItems/Enabled', defaults)
        self.assertIn('RicochetItems', pack.RETIRED_MODULES)
        self.assertIn(('AirShift', 'AllowGroundTesting'), pack.RETIRED_SETTINGS)
        self.assertIn(('AirShift', 'SidewaysImpulse'), pack.RETIRED_SETTINGS)
        for identity in ('AirShift', 'PracticeRewind', 'MineTraps'):
            self.assertIs(defaults[identity + '/Enabled'], False)
            self.assertEqual(sum(p['features'].count(identity) for p in pack.catalog_packs()), 1)
            feature = next(f for f in pack.catalog_features() if f['id'] == identity)
            self.assertTrue(feature['gameplay'])
            if identity != 'AirShift':
                self.assertIn('In-game validation pending', feature['description'])

    def test_rewind_export_includes_shared_snapshot_and_movement_helpers(self):
        for identity in ('PracticeRewind', 'SaveStates'):
            feature = next(f for f in pack.catalog_features() if f['id'] == identity)
            sources = module_packages._source_dependencies([feature])
            for helper in ('DrivingSnapshot.cs', 'AirShiftFeature.cs', 'AirDashMath.cs', 'FastFallFeature.cs'):
                self.assertIn('plugins/TK2.Customization/' + helper, sources)

    def test_controls_survive_config_roundtrip(self):
        from studio import settings
        values = settings.read(pack.initial_config().encode())['settings']
        self.assertEqual(values['AirShift/LeftKey'], 'LeftBracket')
        self.assertEqual(values['AirShift/RightKey'], 'RightBracket')
        self.assertEqual(values['PracticeRewind/RewindSeconds'], 3)
        self.assertEqual(values['PracticeRewind/RewindKey'], 'Backspace')
        self.assertEqual(values['AirShift/RightAction'], '')
        self.assertEqual(values['AirShift/DashDistance'], 3)
        self.assertEqual(values['AirShift/DashDuration'], .12)
        self.assertNotIn('AirShift/AllowGroundTesting', values)
        self.assertNotIn('AirShift/SidewaysImpulse', values)
        stale = b"[AirShift]\nAllowGroundTesting = true\nSidewaysImpulse = 65\n[MineTraps]\nTriggerRadius = 2.5\n"
        read = settings.read(stale)
        self.assertFalse(read['extraSettings'])

    def test_shortcuts_have_keyboard_and_physical_controller_menus(self):
        features = {f['id']: {s[0]: s for s in f['settings']} for f in pack.catalog_features()}
        for module, key in [('AirShift', 'LeftKey'), ('AirShift', 'RightKey'), ('SaveStates', 'SaveKey'),
                            ('SaveStates', 'LoadKey'), ('PracticeRewind', 'RewindKey'), ('Physics', 'KeyboardKey'), ('MineTraps', 'DropKey')]:
            self.assertIn('Space', features[module][key][7])
            self.assertIn('None', features[module][key][7])
        for module, key in [('AirShift', 'LeftAction'), ('AirShift', 'RightAction'), ('SaveStates', 'SaveAction'),
                            ('SaveStates', 'LoadAction'), ('PracticeRewind', 'ControllerAction'), ('MineTraps', 'ControllerAction')]:
            choices = features[module][key][7]
            self.assertIn('Gamepad: LB / L1', choices)
            self.assertIn('Gamepad: A / Cross', choices)
            self.assertIn('Raw button 31', choices)
            self.assertIn('', choices)

    def test_experimentation_values_have_no_former_upper_ceiling(self):
        values = pack.validate({'AirShift/DashDistance': 1000, 'AirShift/OncePerJump': False,
                                'MineTraps/RewardAmmo': 100, 'MineTraps/TriggerRadius': 1000})
        self.assertEqual(values[('AirShift', 'DashDistance')], '1000')
        feature = next(f for f in pack.catalog_features() if f['id'] == 'AirShift')
        distance = module_packages._schema(feature)['DashDistance']
        self.assertEqual(module_packages._setting(1000, distance), 1000)
        with self.assertRaises(ValueError):
            pack.validate({'AirShift/DashDistance': float('inf')})
        with self.assertRaises(ValueError):
            module_packages._setting(0, distance)

    def test_each_shortcut_module_exports_the_input_reader(self):
        for identity in ('AirShift', 'SaveStates', 'PracticeRewind', 'Physics', 'MineTraps'):
            feature = next(f for f in pack.catalog_features() if f['id'] == identity)
            sources = module_packages._source_dependencies([feature])
            self.assertIn('plugins/TK2.Customization/ShortcutInput.cs', sources)

    def test_aerial_exports_include_shared_hook_installer(self):
        for identity in ('AirShift', 'MineTraps', 'PracticeRewind', 'SaveStates'):
            feature = next(f for f in pack.catalog_features() if f['id'] == identity)
            self.assertIn('plugins/TK2.Customization/FeatureHook.cs', module_packages._source_dependencies([feature]))


if __name__ == '__main__':
    unittest.main()
