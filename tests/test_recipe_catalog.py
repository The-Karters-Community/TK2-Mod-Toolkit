import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from studio import core, pack, recipe_catalog, settings


RECIPE = '''public sealed class ToyKart : IModRecipe {
 public string Name => "ToyKart";
 public bool ChangesGameplay => true;
 void Configure(ConfigFile config) {
  config.Bind("Recipe." + Name, "Strength", 10f,
    new ConfigDescription("Impulse, with a bound.", new AcceptableValueRange<float>(0, 20)));
  config.Bind("Recipe." + Name, "Key", KeyCode.F9, "Release key");
 }
}'''


class RecipeCatalogTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory(); self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)
        self.sources = self.root / 'plugins/TK2.Customization/Recipes'
        self.sources.mkdir(parents=True)
        (self.sources / 'ToyKart.cs').write_text(RECIPE)
        replacement = patch.object(core, 'ROOT', self.root); replacement.start(); self.addCleanup(replacement.stop)

    def test_reads_bounded_literals_and_key_choices(self):
        feature = recipe_catalog.features()[0]
        self.assertEqual(feature['id'], 'Recipe.ToyKart')
        self.assertFalse(feature['compiled'])
        self.assertEqual(feature['settings'][0][3:6], (10.0,0,20))
        self.assertIn('F9', feature['settings'][1][7])
        self.assertFalse(feature['catalogWarnings'])

    def test_unbuilt_default_edit_does_not_change_compiled_defaults(self):
        original = recipe_catalog.source_features()
        artifact = self.root / 'artifacts/TK2.Customization/recipe-catalog.json'
        artifact.parent.mkdir(parents=True); artifact.write_text(json.dumps(original))
        (self.sources / 'ToyKart.cs').write_text(RECIPE.replace('10f', '12f'))
        self.assertEqual(recipe_catalog.features()[0]['settings'][0][3],10)
        artifact.write_text(json.dumps(recipe_catalog.source_features()))
        self.assertEqual(recipe_catalog.features()[0]['settings'][0][3],12)

    def test_settings_are_typed_and_not_duplicated_in_unknown_recipe_list(self):
        result = settings.read(b'[Recipe.ToyKart]\nEnabled = false\nStrength = 7\n')
        self.assertEqual(result['settings']['Recipe.ToyKart/Strength'],7)
        self.assertFalse(result['recipes'])
        self.assertEqual(pack.validate({'Recipe.ToyKart/Key':'F9'})[('Recipe.ToyKart','Key')],'F9')
        with self.assertRaises(ValueError): pack.validate({'Recipe.ToyKart/Strength':21})

    def test_seeding_preserves_existing_values_and_is_idempotent(self):
        first = pack.seed_config('[Recipe.ToyKart]\nStrength = 7\n')
        self.assertIn('Strength = 7', first)
        self.assertEqual(pack.seed_config(first),first)
        self.assertEqual(first.count('[Recipe.ToyKart]'),1)
        self.assertIn('Enabled = false',first)

    def test_expressions_are_not_executed_or_treated_as_defaults(self):
        (self.sources / 'ToyKart.cs').write_text(RECIPE.replace('10f','DangerousMethod()'))
        feature = recipe_catalog.features()[0]
        self.assertNotIn('Strength',[s[0] for s in feature['settings']])
        self.assertTrue(feature['catalogWarnings'])

    def test_removed_module_config_does_not_reappear_in_settings(self):
        data = b'[Recipe.AirGlider]\nEnabled = true\nLift = 18\n[Recipe.CosmeticModel]\nEnabled = true\nModelPath = old/model.obj\n'
        result = settings.read(data)
        self.assertFalse(result['recipes'])
        self.assertFalse(any(k.startswith(('Recipe.AirGlider/','Recipe.CosmeticModel/')) for k in result['settings']))
        with self.assertRaises(ValueError): settings.merge(data,{'hash':result['configHash'],'recipes':{'Recipe.AirGlider/Enabled':'false'}})
        saved = settings.merge(data,{'hash':result['configHash'],'values':{'Camera/PanelEnabled':False,'Camera/PanelHotkey':'C'}})
        self.assertIn(b'ModelPath = old/model.obj',saved)
        after = settings.read(saved)
        self.assertIs(after['settings']['Camera/PanelEnabled'],False)
        self.assertEqual(after['settings']['Camera/PanelHotkey'],'C')
