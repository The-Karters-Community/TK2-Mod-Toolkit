import json
from pathlib import Path
import tempfile
import unittest
import zipfile
from unittest.mock import patch

from studio import releases


class ReleaseTests(unittest.TestCase):
    def test_versions_are_numeric_and_prerelease_tags_are_not_versions(self):
        self.assertEqual(releases.version_tuple("v0.7.12"), (0, 7, 12))
        self.assertIsNone(releases.version_tuple("v0.7.0-rc1"))
        self.assertIsNone(releases.version_tuple("latest"))

    def test_checker_exposes_newer_release_only_when_digest_and_portable_manifest_are_present(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "portable-manifest.json").write_text(json.dumps({"version": "0.6.21"}), encoding="utf-8")
            release = {"version": "0.7.0", "tag": "v0.7.0", "url": releases.RELEASE_PAGE,
                       "asset": "TK2-Mod-Toolkit-0.7.0-win-x64.zip", "downloadUrl": "https://example.invalid/package.zip",
                       "sha256": "a" * 64, "verified": True, "size": 1}
            checker = releases.ReleaseChecker("0.6.21", root, True)
            with patch.object(releases, "latest_release", return_value=release):
                status = checker.check()
            self.assertEqual(status["state"], "available")
            self.assertTrue(status["canInstall"])
            release["verified"] = False
            with patch.object(releases, "latest_release", return_value=release):
                status = checker.check()
            self.assertFalse(status["canInstall"])

    def test_safe_extraction_checks_manifest_version_and_updater_presence(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp); archive = base / "good.zip"; destination = base / "extract"
            with zipfile.ZipFile(archive, "w") as output:
                output.writestr("TK2 Mod Toolkit 0.7.0/portable-manifest.json", json.dumps({"version": "0.7.0"}))
                output.writestr("TK2 Mod Toolkit 0.7.0/TK2 Mod Toolkit.exe", b"fixture")
                output.writestr("TK2 Mod Toolkit 0.7.0/tools/update_portable.ps1", b"fixture")
            package = releases.extract_verified_archive(archive, destination, "0.7.0")
            self.assertTrue((package / "TK2 Mod Toolkit.exe").is_file())

    def test_extraction_rejects_traversal_and_mismatched_version(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp); archive = base / "unsafe.zip"; destination = base / "extract"
            with zipfile.ZipFile(archive, "w") as output:
                output.writestr("../escape.txt", b"no")
                output.writestr("TK2 Mod Toolkit 0.7.0/portable-manifest.json", json.dumps({"version": "0.7.0"}))
            with self.assertRaisesRegex(ValueError, "unsafe path"):
                releases.extract_verified_archive(archive, destination, "0.7.0")


if __name__ == "__main__":
    unittest.main()
