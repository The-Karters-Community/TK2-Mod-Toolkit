import tempfile
import unittest
from pathlib import Path

from studio import pseudocode


class PseudocodeSearchTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source_dir = self.root / "local/ghidra/run/pseudocode"
        self.source_dir.mkdir(parents=True)
        self.first = self.source_dir / "180000100_Kart__Update.c"
        self.first.write_text(
            "/* Ghidra pseudocode, not original C#; Kart$$Update @ 180000100 */\n"
            "void Kart__Update() {\n  velocity = groundFriction * deltaTime;\n}\n",
            encoding="utf-8",
        )

    def test_search_returns_native_source_and_read_is_export_scoped(self):
        result = pseudocode.search("groundFriction", root=self.root)
        self.assertEqual(result["total"], 1)
        self.assertEqual(result["indexed"], 1)
        item = result["results"][0]
        self.assertEqual(item["name"], "Kart$$Update")
        source = pseudocode.read(item["id"], root=self.root)
        self.assertIn("groundFriction", source["code"])
        self.assertFalse(source["truncated"])
        with self.assertRaises(ValueError): pseudocode.read("../../outside.c", root=self.root)
        with self.assertRaises(ValueError): pseudocode.read("local/ghidra/not-an-export.c", root=self.root)

    def test_index_refreshes_when_an_export_is_added(self):
        first = pseudocode.search("groundFriction", root=self.root)
        second = self.source_dir / "180000200_Kart__Jump.c"
        second.write_text(
            "/* Ghidra pseudocode, not original C#; Kart$$Jump @ 180000200 */\n"
            "void Kart__Jump() {\n  airborne = true;\n}\n",
            encoding="utf-8",
        )
        updated = pseudocode.search("airborne", root=self.root)
        self.assertEqual(first["indexed"], 1)
        self.assertEqual(updated["indexed"], 2)
        self.assertEqual(updated["results"][0]["name"], "Kart$$Jump")

    def test_blank_query_does_not_build_an_index(self):
        result = pseudocode.search("   ", root=self.root)
        self.assertEqual(result["indexed"], 1)
        self.assertEqual(result["total"], 0)
        self.assertFalse((self.root / "local/ghidra/pseudocode-search.sqlite3").exists())


if __name__ == "__main__":
    unittest.main()
