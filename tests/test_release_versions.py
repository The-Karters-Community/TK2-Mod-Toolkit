from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

from studio.version import APP_VERSION, display_version

ROOT = Path(__file__).resolve().parents[1]
POWERSHELL = shutil.which('powershell.exe')


class ReleaseVersionTests(unittest.TestCase):
    def test_source_is_development_and_packaged_version_is_numeric(self):
        self.assertEqual(display_version(), APP_VERSION + '-dev')
        self.assertEqual(display_version(True), APP_VERSION)

    @unittest.skipUnless(POWERSHELL, 'Windows release workflow')
    def test_release_success_advances_source_but_failure_does_not(self):
        for succeeds in (True, False):
            with self.subTest(succeeds=succeeds), tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                (root / 'tools').mkdir()
                (root / 'studio').mkdir()
                (root / 'plugins/TK2.Customization').mkdir(parents=True)
                (root / 'artifacts/portable').mkdir(parents=True)
                version = root / 'studio/version.py'
                version.write_text('APP_VERSION = "0.6.24"\n')
                plugin = root / 'plugins/TK2.Customization/ToolkitVersion.cs'
                plugin.write_text('internal const string Value = "0.6.24";\n')
                (root / 'artifacts/portable/TK2-Mod-Toolkit-0.6.24-win-x64.zip').write_bytes(b'fixture')
                for file in ('publish_release.ps1', 'set_version.ps1'):
                    shutil.copyfile(ROOT / 'tools' / file, root / 'tools' / file)
                harness = root / 'harness.ps1'
                harness.write_text('''
$ErrorActionPreference = 'Stop'
function Get-FileHash { param($LiteralPath, $Algorithm) [pscustomobject]@{Hash=('a' * 64)} }
function git {
    $global:LASTEXITCODE=0
    if ($args[0] -eq 'rev-parse') { 'fixture-head' }
    if ($args[0] -eq 'ls-remote') { "fixture-head`trefs/heads/main" }
    if ($args[0] -eq 'commit') { $args -join ' ' | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'commit.txt') }
}
function gh {
    if ($args[1] -eq 'view') { $global:LASTEXITCODE=1; return }
    $args -join ' ' | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'release.txt')
    $global:LASTEXITCODE=__RESULT__
}
& (Join-Path $PSScriptRoot 'tools/publish_release.ps1') -Publish
'''.replace('__RESULT__', '0' if succeeds else '1'), encoding='utf-8')
                result = subprocess.run([POWERSHELL, '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', str(harness)], capture_output=True, text=True, timeout=15)
                expected = '0.6.25' if succeeds else '0.6.24'
                self.assertEqual(version.read_text(), f'APP_VERSION = "{expected}"\n', result.stderr)
                self.assertEqual(plugin.read_text(), f'internal const string Value = "{expected}";\n', result.stderr)
                self.assertEqual((root / 'commit.txt').exists(), succeeds, result.stderr)
                self.assertIn('v0.6.24', (root / 'release.txt').read_text())
                self.assertEqual(result.returncode == 0, succeeds, result.stderr)
