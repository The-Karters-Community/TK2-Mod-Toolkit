import tempfile
import unittest
from pathlib import Path

from studio.frontend import render_html


class FrontendTemplatesTests(unittest.TestCase):
    def test_nested_views_are_composed_without_include_directives(self):
        root = Path(__file__).resolve().parents[1] / 'studio/web'
        result = render_html(root / 'index.html', root)
        self.assertNotIn('@include', result)
        for element in ('feature-list', 'panel-editor', 'panel-api', 'game-directory'):
            self.assertIn(f'id="{element}"', result)
        self.assertIn('type="module"', result)

    def test_templates_reject_cycles_and_escape(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / 'web'
            root.mkdir()
            entry = root / 'index.html'
            entry.write_text('<!-- @include ./index.html -->', encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'Circular'):
                render_html(entry, root)
            entry.write_text('<!-- @include ../outside.html -->', encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'inside'):
                render_html(entry, root)
