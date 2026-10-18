"""Build the native app and embed its bridge. Requires installed Visual C++ x64 tools."""
import hashlib
import json
import shutil
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "local/standalone-build/current"
OUTPUT = ROOT / "artifacts/BobbyRaceTools"
locator = Path("C:/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe")
installs = json.loads(subprocess.check_output([str(locator), "-products", "*", "-requires", "Microsoft.VisualStudio.Component.VC.Tools.x86.x64", "-format", "json"]))
if not installs:
    raise SystemExit("Install Visual C++ x64 Build Tools first.")
installs.sort(key=lambda item: tuple(map(int, item["installationVersion"].split("."))), reverse=True)
compiler = Path(installs[0]["installationPath"]) / "VC/Auxiliary/Build/vcvars64.bat"
BUILD.mkdir(parents=True, exist_ok=True)
OUTPUT.mkdir(parents=True, exist_ok=True)
resource = BUILD / "App.rc"
resource.write_text('101 RCDATA "Bridge.dll"\n1 24 "' + (ROOT / "standalone/app.manifest").as_posix() + '"\n', encoding="utf-8")
source = ROOT / "standalone"
vendor = source / "vendor/minhook/src"
imgui = source / "vendor/imgui"
imgui_sources = [imgui / name for name in ("imgui.cpp", "imgui_draw.cpp", "imgui_tables.cpp", "imgui_widgets.cpp", "backends/imgui_impl_win32.cpp", "backends/imgui_impl_dx11.cpp")]
imgui_objects = "imgui.obj imgui_draw.obj imgui_tables.obj imgui_widgets.obj imgui_impl_win32.obj imgui_impl_dx11.obj"
flags = "/nologo /O2 /MT /W4 /utf-8 /DUNICODE /D_UNICODE /DWIN32_LEAN_AND_MEAN /DNOMINMAX /DIMGUI_IMPL_WIN32_DISABLE_GAMEPAD"
commands = [
    "@echo off",
    f'call "{compiler}" >nul',
    "if %errorlevel% neq 0 exit /b %errorlevel%",
    f'cl {flags} /c "{vendor / "buffer.c"}" "{vendor / "hook.c"}" "{vendor / "trampoline.c"}" "{vendor / "hde/hde64.c"}"',
    "if %errorlevel% neq 0 exit /b %errorlevel%",
    f'cl {flags} /EHsc /std:c++17 /I"{imgui}" /c ' + " ".join(f'"{path}"' for path in imgui_sources),
    "if %errorlevel% neq 0 exit /b %errorlevel%",
    f'cl {flags} /EHsc /std:c++17 /I"{imgui}" /LD "{source / "Bridge.cpp"}" "{source / "Renderer.cpp"}" {imgui_objects} buffer.obj hook.obj trampoline.obj hde64.obj /link /OUT:Bridge.dll /IMPLIB:Bridge.lib user32.lib gdi32.lib d3d11.lib dxgi.lib d3dcompiler.lib dwmapi.lib xinput.lib /DYNAMICBASE /NXCOMPAT',
    "if %errorlevel% neq 0 exit /b %errorlevel%",
    f'cl {flags} /EHsc /std:c++17 /I"{imgui}" "{source / "RendererTests.cpp"}" Renderer.obj {imgui_objects} buffer.obj hook.obj trampoline.obj hde64.obj /Fe:RendererTests.exe /link user32.lib gdi32.lib d3d11.lib dxgi.lib d3dcompiler.lib dwmapi.lib xinput.lib',
    "if %errorlevel% neq 0 exit /b %errorlevel%",
    "RendererTests.exe",
    "if %errorlevel% neq 0 exit /b %errorlevel%",
    f'cl {flags} /EHsc /std:c++17 "{source / "Tests.cpp"}" /Fe:Tests.exe',
    "if %errorlevel% neq 0 exit /b %errorlevel%",
    "Tests.exe",
    "if %errorlevel% neq 0 exit /b %errorlevel%",
    "rc /nologo App.rc",
    "if %errorlevel% neq 0 exit /b %errorlevel%",
    f'cl {flags} /EHsc /std:c++17 "{source / "App.cpp"}" App.res /Fe:"{OUTPUT / "BobbyRaceTools.exe"}" /link /SUBSYSTEM:WINDOWS /MANIFEST:NO user32.lib gdi32.lib gdiplus.lib comctl32.lib shell32.lib ole32.lib advapi32.lib bcrypt.lib /DYNAMICBASE /NXCOMPAT',
    "exit /b %errorlevel%",
]
batch = BUILD / "build.cmd"
batch.write_text("\n".join(commands) + "\n", encoding="utf-8")
subprocess.run(["cmd.exe", "/d", "/c", str(batch)], cwd=BUILD, check=True)
exe = OUTPUT / "BobbyRaceTools.exe"
receipt = {"version": "0.2.0", "executable": exe.name, "bytes": exe.stat().st_size,
           "sha256": hashlib.sha256(exe.read_bytes()).hexdigest(),
           "bridgeBytes": (BUILD / "Bridge.dll").stat().st_size,
           "dependencies": "Windows system libraries only; embedded bridge; static C++ runtime",
           "runtimeTested": False}
(OUTPUT / "build.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
for name in ("README.md", "THIRD-PARTY.txt", "MinHook-LICENSE.txt"):
    shutil.copyfile(source / name, OUTPUT / name)
shutil.copyfile(ROOT / "LICENSE", OUTPUT / "LICENSE.txt")
shutil.copyfile(imgui / "LICENSE.txt", OUTPUT / "ImGui-LICENSE.txt")
print(json.dumps(receipt, indent=2))
