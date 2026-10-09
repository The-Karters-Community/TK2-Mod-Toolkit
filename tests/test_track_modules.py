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

    def test_inspector_toggles_and_draws_track_collision_layers(self):
        source = (pack.PROJECT.parent / 'Recipes/TrackInspector.cs').read_text(encoding='utf-8')
        self.assertIn('bForceDebugShowTriggerCollisionMeshes', source)
        self.assertIn('GetComponentsInChildren<Collider>(true)', source)
        self.assertIn('GetComponent<MeshRenderer>()', source)
        self.assertIn('PatchTriggerSetup(harmony, "Init")', source)
        self.assertIn('PatchTriggerSetup(harmony, "Start")', source)
        self.assertIn('PatchMapSetup(harmony)', source)
        self.assertIn('Patch(target,', source)
        self.assertIn('AfterMapSetup', source)
        self.assertIn('Input.GetKeyDown(_key.Value)', source)
        self.assertIn('Track Inspector: {_key.Value} pressed', source)
        self.assertIn('module.CaptureTriggerDefaults(__instance);', source)
        self.assertIn('OriginalFlag', source)
        self.assertIn('wallLayerToCollDetect.value', source)
        self.assertIn('wallRespawn_Flat_ColliderLayer.value', source)
        self.assertIn('wallRespawn_Always_ColliderLayer.value', source)
        self.assertIn('MaxShown = 48', source)
        self.assertIn('RefreshInterval = 2f', source)
        self.assertIn('FindObjectsOfType<Collider>(true)', source)
        self.assertIn('case MeshCollider meshCollider:', source)
        self.assertIn('case SphereCollider sphere:', source)
        self.assertIn('case CapsuleCollider capsule:', source)
        self.assertIn('Showing {visualized} wall/respawn shapes', source)
        self.assertIn('TrackInspector.DrawStatus()', (pack.PROJECT.parent / 'StudioBehaviour.cs').read_text(encoding='utf-8'))
        self.assertNotIn('ClassInjector', source)

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
        for name, helpers in [('MirrorRace', ['MirrorRace.ImageEffect.cs']), ('TrackInspector', [])]:
            identity = 'Recipe.' + name
            paths = module_packages._source_dependencies([features[identity]])
            for helper in helpers:
                self.assertIn('plugins/TK2.Customization/Recipes/' + helper, paths)
                self.assertIn('plugins/TK2.Customization/Recipes/' + helper,
                              pack.module_sources()[identity])
            self.assertFalse(any('/Models/' in path for path in paths))
            self.assertNotIn('Enabled', [s[0] for s in features[identity]['settings']])

    def test_camera_panel_requires_camera_module(self):
        source = (pack.PROJECT.parent / 'CameraPanel.cs').read_text(encoding='utf-8')
        self.assertIn('!p.CameraEnabled.Value || !p.CameraPanelEnabled.Value', source)
        camera = next(f for f in pack.catalog_features() if f['id'] == 'Camera')
        panel = next(setting for setting in camera['settings'] if setting[0] == 'PanelEnabled')
        self.assertIn('only while the Camera setup module is enabled', panel[6])

    def test_track_inspector_is_not_duplicated_under_user_recipes(self):
        packs = pack.catalog_packs()
        memberships = [p['id'] for p in packs if 'Recipe.TrackInspector' in p['features']]
        self.assertEqual(memberships, ['garage'])


if __name__ == '__main__':
    unittest.main()
