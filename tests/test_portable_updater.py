"""Run the actual PowerShell filesystem flow, with only process/HTTP boundaries mocked."""
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / 'tools/update_portable.ps1'
POWERSHELL = shutil.which('powershell.exe')


@unittest.skipUnless(POWERSHELL, 'Windows updater integration')
class PortableUpdaterTests(unittest.TestCase):
    def run_update(self, healthy=True, safe=True):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        root = Path(temp.name)
        old = root / 'TK2-Mod-Toolkit-0.6.23-win-x64'
        staging = root / ('tk2-toolkit-update-fixture' if safe else 'unrelated-folder')
        new = staging / 'extracted/new-release'
        old.mkdir()
        (new / 'tools').mkdir(parents=True)
        (new / 'local').mkdir()
        (old / 'old-only.txt').write_text('old')
        (old / 'TK2 Mod Toolkit.exe').write_bytes(b'old fixture')
        (new / 'TK2 Mod Toolkit.exe').write_bytes(b'new fixture')
        (new / 'portable-manifest.json').write_text(json.dumps({'version': '0.6.24'}))
        (new / 'local/user.txt').write_text('preserved preferences')
        (new / 'local/garage-session.json').write_text(json.dumps({'origin': 'http://127.0.0.1:12345', 'token': 'fixture'}))
        updater = new / 'tools/update_portable.ps1'
        shutil.copyfile(SCRIPT, updater)
        wrapper = root / 'harness.ps1'
        def quote(path):
            return "'" + str(path).replace("'", "''") + "'"
        wrapper.write_text("""
function Start-Process { param($FilePath, $WorkingDirectory, [switch]$PassThru) if ($PassThru) { [pscustomobject]@{Id=4444} } }
function Get-Process { param($Id, $ErrorAction) if ($Id -eq 4444) { [pscustomobject]@{Id=4444} } }
function Stop-Process { param($Id, [switch]$Force, $ErrorAction) }
function Invoke-RestMethod { param($Uri, $Headers, $TimeoutSec)
    [pscustomobject]@{app='TK2 Mod Toolkit'; version='__HEALTH_VERSION__'; pid=4444}
}
& """.replace("__HEALTH_VERSION__", "0.6.24" if healthy else "0.6.23") + quote(updater) + ' -OldRoot ' + quote(old) + ' -NewRoot ' + quote(new) +
            ' -WaitPid 9000001 -BackupRoot ' + quote(root / 'old.backup') + ' -StartupTimeoutSeconds 1\nexit $LASTEXITCODE', encoding='utf-8')
        result = subprocess.run([POWERSHELL, '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', str(wrapper)], capture_output=True, text=True, timeout=15)
        return result, root, old, staging

    def test_healthy_update_preserves_path_data_and_leaves_no_backup(self):
        result, root, old, staging = self.run_update()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual((old / 'TK2 Mod Toolkit.exe').read_bytes(), b'new fixture')
        self.assertEqual((old / 'local/user.txt').read_text(), 'preserved preferences')
        self.assertFalse((old / 'old-only.txt').exists())
        self.assertFalse(staging.exists())
        self.assertFalse((root / 'old.backup').exists())

    def test_unhealthy_startup_restores_old_folder(self):
        result, _, old, staging = self.run_update(healthy=False)
        self.assertEqual(result.returncode, 3, result.stderr)
        self.assertEqual((old / 'TK2 Mod Toolkit.exe').read_bytes(), b'old fixture')
        self.assertTrue((old / 'old-only.txt').exists())
        self.assertTrue((staging / 'update-error.txt').exists())
        self.assertFalse((staging / 'previous').exists())

    def test_rejects_unexpected_staging_before_moving_files(self):
        result, _, old, staging = self.run_update(safe=False)
        self.assertEqual(result.returncode, 2, result.stderr)
        self.assertTrue((old / 'old-only.txt').exists())
        self.assertTrue((staging / 'extracted/new-release').exists())
