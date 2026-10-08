"""Build a self-contained Windows player app using an isolated PyInstaller env."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from studio import core, pack, setup, symbols


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--python', type=Path, default=core.ROOT / 'local/portable-build-env/Scripts/python.exe')
    parser.add_argument('--rebuild-plugin', action='store_true')
    args = parser.parse_args()
    if args.rebuild_plugin: core.build_plugin(core.DEFAULT_GAME, pack.PROJECT, print)
    if not (pack.ARTIFACT / 'build.json').is_file(): raise ValueError('Build the plugin before packaging.')
    source = setup.loader_source()
    if source is None: raise ValueError('Provide the BepInEx distribution before packaging.')
    artifact = core.contained(core.ROOT, core.ROOT / 'artifacts/portable')
    artifact.mkdir(parents=True, exist_ok=True)
    staging = core.contained(core.ROOT, core.ROOT / 'local/portable-stage')
    if staging.exists(): shutil.rmtree(staging)
    staging.mkdir(parents=True, exist_ok=True)
    # Include editable source, assets and the prebuilt plugin. Never include game
    # binaries, generated interop assemblies, credentials, session tokens or logs.
    for directory in ('assets', 'studio/web', 'src/Reconstructed', 'templates', 'docs'):
        shutil.copytree(core.ROOT / directory, staging / directory, dirs_exist_ok=True)
    for file in pack.source_files() + ['plugins/TK2.Customization/TK2.Customization.csproj', 'README.md']:
        target = staging / file; target.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(core.ROOT / file, target)
    for file in ('mk_catalog.json', 'community_catalog.json'):
        target = staging / 'studio' / file; target.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(core.ROOT / 'studio' / file, target)
    shutil.copytree(pack.ARTIFACT, staging / 'artifacts/TK2.Customization', dirs_exist_ok=True)
    shutil.copytree(source, staging / 'vendor/BepInEx', dirs_exist_ok=True,
        ignore=shutil.ignore_patterns('plugins', 'config', 'interop', 'cache', '*.log'))
    # The portable player app has a function-name/signature catalog even without
    # the author's local Il2CppDumper files. It contains no original game bodies.
    core.write_json(staging / 'exports/functions.json', symbols.methods())
    command = [str(args.python.resolve()), '-m', 'PyInstaller', '--noconfirm', '--onedir', '--windowed', '--contents-directory', '.',
               '--icon', str(core.ROOT / 'assets/TheKartersLogoModified.ico'), '--name', 'TK2 Mod Toolkit', '--distpath', str(artifact), '--workpath', str(core.ROOT / 'local/portable-pyinstaller'),
               '--specpath', str(core.ROOT / 'local/portable-pyinstaller'), '--add-data', str(staging) + ':.', str(core.ROOT / 'launch.py')]
    core.contained(artifact, artifact / 'TK2 Mod Toolkit')  # PyInstaller may replace this output folder.
    subprocess.run(command, cwd=core.ROOT, check=True, creationflags=core.CREATE_NO_WINDOW)
    output = artifact / 'TK2 Mod Toolkit'
    core.write_json(output / 'portable-manifest.json', {'version':'0.4.0', 'playerRequirements':['Windows x64', 'The Karters 2 supported game build', 'Edge or another browser'],
        'pythonIncluded':True, 'loaderIncluded':True, 'compilerRequiredForPlayerInstall':False, 'authorBuildRequires':'.NET SDK',
        'gameBinaryIncluded':False, 'supportedGameHash':json.loads((pack.ARTIFACT / 'build.json').read_text())['fingerprint']['files']['GameAssembly.dll']})
    archive = shutil.make_archive(str(artifact / 'TK2-Mod-Toolkit-0.4.0-win-x64'), 'zip', artifact, output.name)
    print(json.dumps({'executable':str(output / 'TK2 Mod Toolkit.exe'),'archive':archive},indent=2))


if __name__ == '__main__': main()
