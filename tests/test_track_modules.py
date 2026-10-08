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

    def test_inspector_hotkey_saves_as_typed_config(self):
        edits = {'Recipe.TrackInspector/Enabled': True, 'Recipe.TrackInspector/ToggleKey': 'C'}
        values = pack.validate(edits)
        self.assertEqual(values[('Recipe.TrackInspector', 'Enabled')], 'true')
        self.assertEqual(values[('Recipe.TrackInspector', 'ToggleKey')], 'C')

    def test_inspector_uses_native_trigger_debug_path(self):
        source = (pack.PROJECT.parent / 'Recipes/TrackInspector.cs').read_text(encoding='utf-8')
        self.assertIn('bForceDebugShowTriggerCollisionMeshes', source)
        self.assertIn('mapPhysicsTriggerColliderList', source)
        self.assertNotIn('FindObjectsOfType<Collider>', source)
        self.assertNotIn('Graphics.DrawMeshNow', source)
        self.assertNotIn('ClassInjector', source)

    def test_mirror_flips_clip_space_and_reverses_local_steering(self):
        source = (pack.PROJECT.parent / 'Recipes/MirrorRace.cs').read_text(encoding='utf-8')
        self.assertIn('camera.projectionMatrix = flipX * state.Projection;', source)
        self.assertNotIn('state.Projection * flipX', source)
        self.assertIn('BeforeSteerInput', source)
        self.assertIn('E_HUMAN_LOCAL', source)
        self.assertIn('Mirror Race applied to local camera', source)

    def test_export_includes_only_active_recipe_helpers(self):
        features = {f['id']: f for f in pack.catalog_features()}
        for name, helpers in [('MirrorRace', []), ('TrackInspector', [])]:
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
