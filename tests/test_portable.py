from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch
from tools import build_portable


class PortablePublicationTests(unittest.TestCase):
    def test_transient_windows_sharing_error_retries_then_publishes(self):
        source = Mock()
        source.rename.side_effect = [PermissionError('scanner lock'), PermissionError('scanner lock'), None]
        with patch.object(build_portable.time, 'sleep') as pause:
            build_portable.publish_directory(source, Path('target'))
        self.assertEqual(source.rename.call_count, 3)
        self.assertEqual(pause.call_count, 2)

    def test_persistent_lock_keeps_source_directory_and_content(self):
        with tempfile.TemporaryDirectory() as folder:
            source = Path(folder) / 'package'
            source.mkdir()
            edited = source / 'MyKartHop.cs'
            edited.write_text('// keep these edits')
            with patch.object(Path, 'rename', side_effect=PermissionError('locked')), patch.object(build_portable.time, 'sleep'):
                with self.assertRaises(PermissionError): build_portable.publish_directory(source, Path(folder) / 'published')
            self.assertEqual(edited.read_text(), '// keep these edits')
            self.assertFalse((Path(folder) / 'published').exists())
