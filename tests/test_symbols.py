import unittest
from unittest.mock import patch
from studio import symbols


class ApiCatalogTests(unittest.TestCase):
    def record(self, identity, owner, signature, source=None):
        return dict(id=identity, type=owner, namespace='', signature=signature,
                    address=None, source=source, confidence=None, evidence=None)

    def test_important_categories_exclude_generated_types_and_keep_full_catalog(self):
        records = [self.record('0', '<>f__AnonymousType', 'public string ToString();'),
                   self.record('1', 'PixelKartPhysics', 'internal void AddVelocity(Vector3 velocity);', 'src/Reconstructed/KartLogic.cs'),
                   self.record('2', 'PixelGameKartCamera', 'public bool IsCameraASpectatorCamera();'),
                   self.record('3', 'SomeOtherGameType', 'public void Tick();')]
        with patch.object(symbols, 'methods', return_value=records):
            self.assertEqual(symbols.search(topic='important')['total'], 2)
            self.assertEqual(symbols.search(topic='all')['total'], 4)
            self.assertEqual(symbols.search(topic='camera')['methods'][0]['id'], '2')
            entry = symbols.search('velocity', topic='driving')['methods'][0]
            self.assertEqual(entry['name'], 'AddVelocity')
            self.assertEqual(entry['label'], 'Add Velocity')
            self.assertEqual(entry['access'], 'internal')
            self.assertEqual(symbols.search(recovered=True)['total'], 1)
            detail = symbols.detail('1')
            self.assertEqual(detail['declaration'], records[1]['signature'])
            self.assertNotIn('{', detail['declaration'])
            with self.assertRaises(ValueError): symbols.search(topic='unknown')

    def test_pages_do_not_repeat_and_all_remains_searchable(self):
        records = [self.record(str(i), 'PixelKartPhysics', f'public void Tick{i}();') for i in range(165)]
        with patch.object(symbols, 'methods', return_value=records):
            first = symbols.search(topic='driving')
            second = symbols.search(offset=first['next'], topic='driving')
            last = symbols.search(offset=second['next'], topic='driving')
            ids = [m['id'] for page in (first, second, last) for m in page['methods']]
            self.assertEqual(len(ids), 165)
            self.assertEqual(len(set(ids)), 165)
            self.assertIsNone(last['next'])
            self.assertEqual(symbols.search('Tick164', topic='all')['total'], 1)
