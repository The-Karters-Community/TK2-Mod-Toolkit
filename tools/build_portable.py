"""Build a self-contained Windows player app using an isolated PyInstaller env."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys
import time
import zipfile
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from studio import core, pack, setup, symbols
from studio.version import APP_VERSION

VERSION = APP_VERSION


def publish_directory(source, target):
    # Windows scanners can briefly hold freshly collected DLLs. Retry only a
    # sharing/access error; keep the old package intact on persistent failure.
    for attempt in range(6):
        try:
            source.rename(target)
            return
        except PermissionError:
            if attempt == 5: raise
            time.sleep(.25 * (2 ** attempt))


def write_portable_zip(root, package, target):
    """Write a compatible DEFLATE ZIP at maximum compression to a temp file."""
    temporary = target.with_name(target.name + ".tmp")
    try:
        with zipfile.ZipFile(temporary, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
            for path in sorted(package.rglob("*")):
                if path.is_file(): archive.write(path, path.relative_to(root).as_posix())
        temporary.replace(target)
    finally:
        if temporary.exists(): temporary.unlink()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--python', type=Path, default=core.ROOT / 'local/portable-build-env/Scripts/python.exe')
    parser.add_argument('--rebuild-plugin', action='store_true')
    parser.add_argument('--game', type=Path, help='Game folder; otherwise discover it in Steam')
    parser.add_argument('--loader', type=Path, help='BepInEx distribution folder; otherwise use vendor/BepInEx or the sibling distribution')
    parser.add_argument('--include-loader', action='store_true', help='Bundle the 70+ MB BepInEx runtime. Default slim package requires BepInEx to be installed separately.')
    args = parser.parse_args()
    if args.rebuild_plugin or not (pack.ARTIFACT / 'build.json').is_file():
        games = [core.validate_game(args.game)] if args.game else setup.discover()
        if not games: raise ValueError('Game not found. Pass --game with the initialized game folder.')
        core.build_plugin(games[0], pack.PROJECT, print)
    include_loader = args.include_loader or args.loader is not None
    source = (args.loader.resolve() if args.loader else setup.loader_source()) if include_loader else None
    if include_loader and source is None: raise ValueError('Provide the BepInEx distribution when using --include-loader.')
    if source is not None and not all((source / name).is_file() for name in setup.CRITICAL): raise ValueError('Loader distribution is incomplete. Use the full Unity IL2CPP x64 distribution.')
    artifact = core.contained(core.ROOT, core.ROOT / 'artifacts/portable')
    artifact.mkdir(parents=True, exist_ok=True)
    output = core.contained(artifact, artifact / ('TK2 Mod Toolkit ' + VERSION))
    executable = output / 'TK2 Mod Toolkit.exe'
    if executable.is_file():
        try:
            with executable.open('r+b'): pass  # Windows rejects writing to a running executable.
        except OSError as error: raise ValueError('Close the portable Toolkit before building it again. Your existing output is preserved.') from error
    staging = core.contained(core.ROOT, core.ROOT / 'local/portable-stage')
    if staging.exists(): shutil.rmtree(staging)
    staging.mkdir(parents=True, exist_ok=True)
    # Include editable source, assets and the prebuilt plugin. Never include game
    # binaries, generated interop assemblies, credentials, session tokens or logs.
    for directory in ('assets', 'studio/web', 'src/Reconstructed', 'templates', 'docs'):
        shutil.copytree(core.ROOT / directory, staging / directory, dirs_exist_ok=True)
    (staging / 'tools').mkdir(parents=True, exist_ok=True)
    shutil.copy2(core.ROOT / 'tools/update_portable.ps1', staging / 'tools/update_portable.ps1')
    for file in pack.source_files() + ['plugins/TK2.Customization/TK2.Customization.csproj', 'README.md', 'LICENSE']:
        target = staging / file; target.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(core.ROOT / file, target)
    for file in ('mk_catalog.json', 'community_catalog.json'):
        target = staging / 'studio' / file; target.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(core.ROOT / 'studio' / file, target)
    shutil.copytree(pack.ARTIFACT, staging / 'artifacts/TK2.Customization', dirs_exist_ok=True)
    if source is not None:
        shutil.copytree(source, staging / 'vendor/BepInEx', dirs_exist_ok=True,
            ignore=shutil.ignore_patterns('plugins', 'config', 'interop', 'cache', '*.log'))
    # The portable player app has a function-name/signature catalog even without
    # the author's local Il2CppDumper files. It contains no original game bodies.
    core.write_json(staging / 'exports/functions.json', symbols.methods())
    fresh_dist = core.contained(core.ROOT, core.ROOT / 'local/portable-dist' / str(time.time_ns()))
    command = [str(args.python.resolve()), '-m', 'PyInstaller', '--noconfirm', '--onedir', '--windowed', '--contents-directory', '.',
               '--icon', str(core.ROOT / 'assets/TheKartersLogoModified.ico'), '--name', 'TK2 Mod Toolkit', '--distpath', str(fresh_dist), '--workpath', str(core.ROOT / 'local/portable-pyinstaller'),
               '--specpath', str(core.ROOT / 'local/portable-pyinstaller'), '--add-data', str(staging) + ':.', str(core.ROOT / 'launch.py')]
    print('Packaging TK2 Mod Toolkit (no game files are installed)...', flush=True)
    process = subprocess.run(command, cwd=core.ROOT, capture_output=True, text=True, errors='replace', creationflags=core.CREATE_NO_WINDOW)
    core.atomic_write(core.ROOT / 'local/portable-build.log', (process.stdout + process.stderr).encode('utf-8'))
    if process.returncode:
        print(process.stdout + process.stderr)
        raise ValueError('Packaging failed. See local/portable-build.log. Existing output was not replaced.')
    fresh_output = core.contained(fresh_dist, fresh_dist / 'TK2 Mod Toolkit')
    core.write_json(fresh_output / 'portable-manifest.json', {'version':VERSION, 'playerRequirements':['Windows x64', 'The Karters 2 0.1.4.18', 'BepInEx Unity IL2CPP x64 6.0.0-be.788 (5b766a3)', 'Edge or another browser'],
        'pythonIncluded':True, 'loaderIncluded':source is not None, 'loaderVersion':setup.SUPPORTED_BEPINEX_VERSION, 'compilerRequiredForPlayerInstall':False, 'authorBuildRequires':'.NET SDK',
        'gameBinaryIncluded':False, 'supportedGameHash':json.loads((pack.ARTIFACT / 'build.json').read_text())['fingerprint']['files']['GameAssembly.dll']})
    previous = None
    if output.exists():
        previous = core.contained(core.ROOT, core.ROOT / 'local/portable-backups' / str(time.time_ns()) / output.name)
        previous.parent.mkdir(parents=True, exist_ok=True)
        publish_directory(output, previous)  # Preserve editable sources, preferences and backups from the old output.
    try: publish_directory(fresh_output, output)
    except OSError:
        if previous and not output.exists(): publish_directory(previous, output)
        raise
    archive = artifact / ('TK2-Mod-Toolkit-' + VERSION + '-win-x64.zip')
    write_portable_zip(artifact, output, archive)
    print(json.dumps({'executable':str(output / 'TK2 Mod Toolkit.exe'),'archive':str(archive),'previousOutputBackup':str(previous) if previous else None},indent=2))


if __name__ == '__main__': main()
