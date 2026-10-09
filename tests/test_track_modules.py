"""Built-in track tools must be configurable and export all their editable helpers."""
import unittest
from studio import pack, module_packages


class TrackModuleIntegrationTests(unittest.TestCase):
    def test_new_tools_start_disabled_and_have_one_pack_membership(self):
        definitions = pack.defaults()
        identity = 'Recipe.MirrorRace'
        self.assertIs(definitions[identity + '/Enabled'], False)
        packs = pack.catalog_packs()
        self.assertEqual(sum(p['features'].count(identity) for p in packs), 1)
        self.assertIn(identity, next(p for p in packs if p['id'] == 'community')['features'])

    def test_failed_track_inspector_is_removed_from_catalog_and_runtime_sources(self):
        identities = [feature['id'] for feature in pack.catalog_features()]
        self.assertNotIn('Recipe.TrackInspector', identities)
        self.assertIn('Recipe.TrackInspector', pack.RETIRED_MODULES)
        self.assertFalse((pack.PROJECT.parent / 'Recipes/TrackInspector.cs').exists())
        self.assertNotIn('TrackInspector.DrawStatus()',
                         (pack.PROJECT.parent / 'StudioBehaviour.cs').read_text(encoding='utf-8'))

    def test_mirror_flips_clip_space_and_reverses_local_steering(self):
        source = (pack.PROJECT.parent / 'Recipes/MirrorRace.cs').read_text(encoding='utf-8')
        effect = (pack.PROJECT.parent / 'Recipes/MirrorRace.ImageEffect.cs').read_text(encoding='utf-8')
        self.assertIn('Graphics.Blit(source, destination, new Vector2(-1f, 1f), new Vector2(1f, 0f));', effect)
        self.assertNotIn('projectionMatrix', source)
        self.assertNotIn('GL.invertCulling', source)
        self.assertNotIn('ERaceState', source)
        self.assertNotIn('BeforeRacingCameraFraming', source)
        self.assertNotIn('AfterRacingCameraFraming', source)
        self.assertIn('BeforeSteerInput', source)
        self.assertIn('E_HUMAN_LOCAL', source)
        self.assertIn('active before countdown', source)

    def test_export_includes_only_active_recipe_helpers(self):
        features = {f['id']: f for f in pack.catalog_features()}
        identity = 'Recipe.MirrorRace'
        paths = module_packages._source_dependencies([features[identity]])
        helper = 'MirrorRace.ImageEffect.cs'
        self.assertIn('plugins/TK2.Customization/Recipes/' + helper, paths)
        self.assertIn('plugins/TK2.Customization/Recipes/' + helper, pack.module_sources()[identity])
        self.assertFalse(any('/Models/' in path for path in paths))
        self.assertNotIn('Enabled', [s[0] for s in features[identity]['settings']])

    def test_camera_panel_requires_camera_module(self):
        source = (pack.PROJECT.parent / 'CameraPanel.cs').read_text(encoding='utf-8')
        self.assertIn('!p.CameraEnabled.Value || !p.CameraPanelEnabled.Value', source)
        camera = next(f for f in pack.catalog_features() if f['id'] == 'Camera')
        panel = next(setting for setting in camera['settings'] if setting[0] == 'PanelEnabled')
        self.assertIn('only while the Camera setup module is enabled', panel[6])

if __name__ == '__main__':
    unittest.main()
