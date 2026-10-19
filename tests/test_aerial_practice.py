import unittest
from studio import pack, module_packages


class AerialPracticeIntegrationTests(unittest.TestCase):
    def test_modules_are_opt_in_and_in_exactly_one_pack(self):
        defaults = pack.defaults()
        for identity in ('AirShift', 'PracticeRewind', 'RicochetItems'):
            self.assertIs(defaults[identity + '/Enabled'], False)
            self.assertEqual(sum(p['features'].count(identity) for p in pack.catalog_packs()), 1)
            feature = next(f for f in pack.catalog_features() if f['id'] == identity)
            self.assertTrue(feature['gameplay'])
            self.assertIn('validation pending', feature['description'])

    def test_rewind_export_includes_shared_snapshot_and_movement_helpers(self):
        for identity in ('PracticeRewind', 'SaveStates'):
            feature = next(f for f in pack.catalog_features() if f['id'] == identity)
            sources = module_packages._source_dependencies([feature])
            for helper in ('DrivingSnapshot.cs', 'AirShiftFeature.cs', 'AirShiftMath.cs', 'FastFallFeature.cs'):
                self.assertIn('plugins/TK2.Customization/' + helper, sources)

    def test_controls_survive_config_roundtrip(self):
        from studio import settings
        values = settings.read(pack.initial_config().encode())['settings']
        self.assertEqual(values['AirShift/LeftKey'], 'LeftBracket')
        self.assertEqual(values['AirShift/RightKey'], 'RightBracket')
        self.assertEqual(values['PracticeRewind/RewindSeconds'], 3)
        self.assertEqual(values['PracticeRewind/RewindKey'], 'Backspace')
        self.assertEqual(values['AirShift/RightAction'], '')


if __name__ == '__main__':
    unittest.main()
