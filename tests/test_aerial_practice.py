import unittest
from studio import pack, module_packages


class AerialPracticeIntegrationTests(unittest.TestCase):
    def test_modules_are_opt_in_and_in_exactly_one_pack(self):
        defaults = pack.defaults()
        self.assertNotIn('RicochetItems/Enabled', defaults)
        self.assertIn('RicochetItems', pack.RETIRED_MODULES)
        self.assertIn(('AirShift', 'AllowGroundTesting'), pack.RETIRED_SETTINGS)
        self.assertIn(('AirShift', 'DashDistance'), pack.RETIRED_SETTINGS)
        self.assertIn(('MineTraps', 'OwnerCanTrigger'), pack.RETIRED_SETTINGS)
        self.assertIn(('MineTraps', 'ArmDelay'), pack.RETIRED_SETTINGS)
        self.assertIn(('MineTraps', 'ThrowForceMultiplier'), pack.RETIRED_SETTINGS)
        for identity in ('AirShift', 'PracticeRewind', 'MineTraps', 'MidairDrift'):
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

    def test_midair_drift_settings_and_sources_roundtrip(self):
        from studio import settings
        values = settings.read(pack.initial_config().encode())['settings']
        self.assertIs(values['MidairDrift/Enabled'], False)
        self.assertIs(values['MidairDrift/ShowStatus'], True)
        feature = next(f for f in pack.catalog_features() if f['id'] == 'MidairDrift')
        sources = module_packages._source_dependencies([feature])
        for helper in ('MidairDriftFeature.cs', 'MidairDriftState.cs', 'FeatureHook.cs', 'AirShiftFeature.cs'):
            self.assertIn('plugins/TK2.Customization/' + helper, sources)
        self.assertEqual(pack.validate({'MidairDrift/Enabled': True})[('MidairDrift', 'Enabled')], 'true')

    def test_controls_survive_config_roundtrip(self):
        from studio import settings
        values = settings.read(pack.initial_config().encode())['settings']
        self.assertEqual(values['AirShift/LeftKey'], 'LeftBracket')
        self.assertEqual(values['AirShift/RightKey'], 'RightBracket')
        self.assertEqual(values['PracticeRewind/RewindSeconds'], 3)
        self.assertEqual(values['PracticeRewind/RewindKey'], 'Backspace')
        self.assertEqual(values['AirShift/RightAction'], '')
        self.assertEqual(values['AirShift/SidewaysImpulse'], 65)
        self.assertEqual(values['AirShift/ShiftDuration'], .25)
        self.assertEqual(values['MineTraps/Damage'], 80)
        self.assertEqual(values['MineTraps/TriggerRadius'], 4)
        self.assertEqual(values['MineTraps/TimedDetonationDelay'], 45)
        self.assertEqual(values['MineTraps/ForwardThrowSpeed'], 30)
        self.assertEqual(values['MineTraps/MineSizeMultiplier'], 5)
        self.assertEqual(values['MineTraps/TimedBlastRadius'], 10)
        self.assertEqual(values['MineTraps/PlacementMode'], 'Forward throw')
        self.assertNotIn('AirShift/AllowGroundTesting', values)
        self.assertNotIn('AirShift/DashDistance', values)
        self.assertNotIn('MineTraps/OwnerCanTrigger', values)
        stale = b"[AirShift]\nAllowGroundTesting = true\nDashDistance = 3\n[MineTraps]\nDropDistance = 4\nOwnerCanTrigger = false\nArmDelay = 0.6\nLifetime = 15\nThrowForceMultiplier = 30\n"
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
        mine_modes = features['MineTraps']['PlacementMode'][7]
        self.assertEqual(mine_modes, ['Forward throw', 'Drop behind'])
        self.assertEqual(features['MineTraps']['MineSizeMultiplier'][3], 5)
        self.assertNotIn('ArmDelay', features['MineTraps'])
        mine = next(f for f in pack.catalog_features() if f['id'] == 'MineTraps')
        self.assertEqual(mine['settingGroups']['PlacementMode'], 'Placement controls')

    def test_experimentation_values_have_no_former_upper_ceiling(self):
        values = pack.validate({'AirShift/SidewaysImpulse': 1000, 'AirShift/OncePerJump': False,
                                'MineTraps/RewardAmmo': 100, 'MineTraps/TriggerRadius': 1000})
        self.assertEqual(values[('AirShift', 'SidewaysImpulse')], '1000')
        feature = next(f for f in pack.catalog_features() if f['id'] == 'AirShift')
        impulse = module_packages._schema(feature)['SidewaysImpulse']
        self.assertEqual(module_packages._setting(1000, impulse), 1000)
        with self.assertRaises(ValueError):
            pack.validate({'AirShift/SidewaysImpulse': float('inf')})
        with self.assertRaises(ValueError):
            module_packages._setting(0, impulse)

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
