import json
import tempfile
import unittest
from pathlib import Path

from tools.il2cpp_source_map import build_map


class Il2CppSourceMapTests(unittest.TestCase):
    def test_maps_declaration_addresses_and_escapes_class_names(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            declarations = root / "declarations"
            declarations.mkdir()
            (declarations / "Kart+Physics.cs").write_text(
                '[Address(RVA = "0x10", Offset = "0x10", VA = "0x180000010")]\n',
                encoding="utf-8",
            )
            index = root / "functions.jsonl"
            index.write_text(
                json.dumps({"name": "Kart+Physics$$Apply", "address": "180000010"}) + "\n"
                + json.dumps({"name": "KartPhysics$$Ignore", "address": "180000020"}) + "\n",
                encoding="utf-8",
            )

            filter_file = root / "local" / "filter.txt"
            manifest = root / "local" / "manifest.json"
            result = build_map(declarations, index, filter_file, manifest)

            self.assertEqual(result["declarationFiles"], 1)
            self.assertEqual(result["distinctNativeAddressesInDeclarations"], 1)
            self.assertEqual(result["matchingClassNamedFunctions"], 1)
            self.assertEqual(result["matchingAddressesPresentInDeclarations"], 1)
            self.assertEqual(filter_file.read_text(encoding="utf-8"), r"^Kart\+Physics\$\$" + "\n")
            self.assertIn("bodies are placeholders", json.loads(manifest.read_text(encoding="utf-8"))["limitations"][0])


if __name__ == "__main__":
    unittest.main()
