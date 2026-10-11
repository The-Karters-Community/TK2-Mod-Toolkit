import re
import unittest
from studio.version import APP_VERSION
from studio import core


class VersioningTests(unittest.TestCase):
    def test_desktop_and_plugin_share_one_release_version(self):
        plugin = (core.ROOT / "plugins/TK2.Customization/ToolkitVersion.cs").read_text(encoding="utf-8")
        value = re.search(r'internal const string Value = "([0-9]+\.[0-9]+\.[0-9]+)";', plugin)
        self.assertIsNotNone(value)
        self.assertEqual(value.group(1), APP_VERSION)

    def test_build_uses_version_without_mutating_it(self):
        source = (core.ROOT / "tools/build_portable.py").read_text(encoding="utf-8")
        self.assertIn("VERSION = APP_VERSION", source)
        self.assertNotIn("VERSION +=", source)


if __name__ == "__main__":
    unittest.main()
