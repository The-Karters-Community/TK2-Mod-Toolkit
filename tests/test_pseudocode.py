import hashlib
import json
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

    def test_reviewed_source_link_requires_exact_provenance_evidence(self):
        evidence = self.first.relative_to(self.root).as_posix()
        source = self.root / "src/Reconstructed/KartLogic.cs"
        source.parent.mkdir(parents=True)
        source.write_text("internal static class KartLogic {}\n", encoding="utf-8")
        provenance = self.root / "src/Reconstructed/provenance.json"
        provenance.write_text(json.dumps({"methods": [{
            "method": "Kart.Update",
            "address": "180000100",
            "source": "src/Reconstructed/KartLogic.cs",
            "evidence": evidence,
            "evidenceSha256": hashlib.sha256(self.first.read_bytes()).hexdigest(),
            "confidence": "reviewed normal-state semantics",
        }]}), encoding="utf-8")

        item = pseudocode.search("groundFriction", root=self.root)["results"][0]
        self.assertEqual(item["reconstruction"]["method"], "Kart.Update")
        self.assertEqual(item["reconstruction"]["source"], "src/Reconstructed/KartLogic.cs")
        self.assertEqual(pseudocode.read(item["id"], root=self.root)["reconstruction"], item["reconstruction"])

        self.first.write_text(self.first.read_text(encoding="utf-8") + "// changed evidence\n", encoding="utf-8")
        self.assertIsNone(pseudocode.read(item["id"], root=self.root)["reconstruction"])

    def test_behavior_slice_is_linked_as_partial_coverage(self):
        evidence = self.first.relative_to(self.root).as_posix()
        source = self.root / "src/Reconstructed/BoostLogic.cs"
        source.parent.mkdir(parents=True)
        source.write_text("internal static class BoostLogic {}\n", encoding="utf-8")
        provenance = self.root / "src/Reconstructed/provenance.json"
        provenance.write_text(json.dumps({"slices": [{
            "behavior": "Boost.Fire slider strength curve",
            "address": "180000100",
            "source": "src/Reconstructed/BoostLogic.cs",
            "evidence": evidence,
            "evidenceSha256": hashlib.sha256(self.first.read_bytes()).hexdigest(),
            "confidence": "curve only; enclosing method not reconstructed",
        }]}), encoding="utf-8")

        item = pseudocode.search("groundFriction", root=self.root)["results"][0]
        reconstruction = item["reconstruction"]
        self.assertEqual(reconstruction["kind"], "behavior slice")
        self.assertEqual(reconstruction["method"], "Boost.Fire slider strength curve")
        self.assertEqual(reconstruction["source"], "src/Reconstructed/BoostLogic.cs")

    def test_blank_query_does_not_build_an_index(self):
        result = pseudocode.search("   ", root=self.root)
        self.assertEqual(result["indexed"], 1)
        self.assertEqual(result["total"], 0)
        self.assertFalse((self.root / "local/ghidra/pseudocode-search.sqlite3").exists())


if __name__ == "__main__":
    unittest.main()
