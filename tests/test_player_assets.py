import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from studio import core
from tools.build_portable import PLAYER_DIRECTORIES, stage_player_assets


class PlayerAssetsTests(unittest.TestCase):
    def test_release_staging_contains_compiled_ui_not_npm_or_frontend_source(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / 'repo'
            staging = Path(folder) / 'staged'
            for directory in PLAYER_DIRECTORIES:
                path = root / directory
                path.mkdir(parents=True)
                (path / 'fixture.txt').write_text('fixture')
            (root / 'studio/web-dist/index.html').write_text('<div id="root"></div>')
            for directory in ('studio/ui/src', 'node_modules/react'):
                (root / directory).mkdir(parents=True)
                (root / directory / 'unshipped.js').write_text('source')
            (root / 'studio/web-dist/app.js.map').write_text('source map')
            with patch.object(core, 'ROOT', root):
                stage_player_assets(staging)
            self.assertTrue((staging / 'studio/web-dist/index.html').is_file())
            self.assertFalse((staging / 'studio/ui').exists())
            self.assertFalse((staging / 'node_modules').exists())
            self.assertFalse((staging / 'studio/web-dist/app.js.map').exists())

    def test_packaging_refuses_missing_frontend_build(self):
        with tempfile.TemporaryDirectory() as folder:
            with patch.object(core, 'ROOT', Path(folder)):
                with self.assertRaisesRegex(ValueError, 'Build the frontend'):
                    stage_player_assets(Path(folder) / 'staging')
