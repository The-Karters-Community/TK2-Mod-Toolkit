"""Built-in track tools must be configurable and export all their editable helpers."""
import unittest
from studio import pack, module_packages


class TrackModuleIntegrationTests(unittest.TestCase):
    def test_new_tools_start_disabled_and_have_one_pack_membership(self):
        definitions = pack.defaults()
        for name, group in [('MirrorRace', 'community'), ('TrackInspector', 'garage')]:
            identity = 'Recipe.' + name
            self.assertIs(definitions[identity + '/Enabled'], False)
            packs = pack.catalog_packs()
            self.assertEqual(sum(p['features'].count(identity) for p in packs), 1)
            self.assertIn(identity, next(p for p in packs if p['id'] == group)['features'])

    def test_inspector_options_and_hotkey_save_as_typed_config(self):
        edits = {'Recipe.TrackInspector/Enabled': True,
                 'Recipe.TrackInspector/ShowWalls': False,
                 'Recipe.TrackInspector/ShowKillTriggers': False,
                 'Recipe.TrackInspector/ToggleKey': 'C'}
        values = pack.validate(edits)
        self.assertEqual(values[('Recipe.TrackInspector', 'Enabled')], 'true')
        self.assertEqual(values[('Recipe.TrackInspector', 'ShowWalls')], 'false')
        self.assertEqual(values[('Recipe.TrackInspector', 'ShowKillTriggers')], 'false')
        self.assertEqual(values[('Recipe.TrackInspector', 'ToggleKey')], 'C')

    def test_export_includes_geometry_and_overlay_helpers(self):
        features = {f['id']: f for f in pack.catalog_features()}
        for name, helpers in [('MirrorRace', ['MirrorGeometry.cs', 'MirrorMesh.cs']),
                              ('TrackInspector', ['TrackInspectorRules.cs'])]:
            identity = 'Recipe.' + name
            paths = module_packages._source_dependencies([features[identity]])
            for helper in helpers:
                self.assertIn('plugins/TK2.Customization/Recipes/' + helper, paths)
                self.assertIn('plugins/TK2.Customization/Recipes/' + helper,
                              pack.module_sources()[identity])
            self.assertFalse(any('/Models/' in path for path in paths))
            self.assertNotIn('Enabled', [s[0] for s in features[identity]['settings']])


if __name__ == '__main__':
    unittest.main()
