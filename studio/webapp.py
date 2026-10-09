"""Loopback-only desktop web UI, without third-party Python dependencies."""
from __future__ import annotations
import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import mimetypes
import os
from pathlib import Path
import secrets
import socket
import subprocess
import threading
import webbrowser
from urllib.parse import urlsplit, parse_qs
from . import core, pack, settings, setup, symbols

WEB = core.ROOT / "studio/web"


class Application:
    def __init__(self, game=None):
        found = setup.discover() if game is None else []
        self.game = core.validate_game(Path(game)) if game is not None else (found[0] if found else None)
        self.token = secrets.token_urlsafe(32)
        self.lock = threading.RLock()
        previous_log = core.ROOT / "local/garage-build.log"
        self.logs = previous_log.read_text(encoding="utf-8", errors="replace").splitlines()[-80:] if previous_log.is_file() else []

    def log(self, message):
        self.logs.append(str(message))
        self.logs = self.logs[-100:]
        core.atomic_write(core.ROOT / "local/garage-build.log", "\n".join(self.logs).encode("utf-8"))

    @property
    def config_path(self):
        if self.game is None: raise ValueError("Choose the game folder in Installation first.")
        return self.game / "BepInEx/config" / core.CONFIG_NAME

    def config_data(self):
        data = self.config_path.read_bytes() if self.game is not None and self.config_path.exists() else b""
        result = settings.read(data)
        result.pop("entries")
        return result

    def state(self):
        backups = []
        for p in sorted((core.ROOT / "local/backups").glob("*/receipt.json"), reverse=True):
            try:
                receipt = json.loads(p.read_text())
                if receipt["game"] == str(self.game):
                    backups.append({"id": p.parent.name, "file": receipt["relative"], "existed": receipt["existed"]})
            except (OSError, ValueError, KeyError): pass
        installed = self.game / "BepInEx/plugins/TK2-Mod-Studio/TK2.Customization.dll" if self.game else core.ROOT / "local/no-game"
        artifact = pack.ARTIFACT / "TK2.Customization.dll"
        current = installed.is_file() and artifact.is_file() and core.sha256(installed) == core.sha256(artifact)
        return {"game": str(self.game) if self.game else "", "installed": installed.is_file(), "packCurrent": current, "features": pack.catalog_features(),
                "plugin": "TK2.Customization.dll", "pluginCount": len([p for p in core.plugins(self.game) if p["enabled"]]) if self.game else 0,
                "setup": setup.readiness(self.game),
                "runtimeValidated": False, "packs": pack.catalog_packs(), "files": pack.source_files(), "moduleSources": pack.module_sources(), "sourceRoot": str(core.ROOT), "logs": self.logs,
                "backups": backups[:30], "buildLog": self.latest_build(), **self.config_data()}

    def latest_build(self):
        lines = "\n".join(self.logs).splitlines()
        starts = [i for i,line in enumerate(lines) if line.startswith("Building against installed")]
        return "\n".join(lines[starts[-1]:]) if starts else ""

    def save_settings(self, body):
        for _ in range(4):
            before = self.config_path.read_bytes() if self.config_path.exists() else b""
            changed = settings.merge(before, body)
            after = self.config_path.read_bytes() if self.config_path.exists() else b""
            if after != before: continue
            if changed == before: return {"message": "Settings already saved", **self.config_data()}
            backup = core.backup_write(self.game, self.config_path, changed)
            return {"message": "Settings saved", "backup": str(backup), **self.config_data()}
        raise ValueError("The game is actively rewriting its config. Try saving again in a moment.")

    def action(self, action, body):
        with self.lock:
            if action == 'browse-file':
                from tkinter import Tk, filedialog
                choices = {'package': ('Choose a module package', [('Toolkit modules','*.tk2mod')])}
                if body.get('kind') not in choices: raise ValueError('Unknown file picker')
                title, filters = choices[body['kind']]
                window = Tk(); window.withdraw()
                try: chosen = filedialog.askopenfilename(title=title,filetypes=filters,parent=window)
                finally: window.destroy()
                return {'path':chosen,'message':'File selected' if chosen else 'Selection cancelled'}
            if action in ('export-package','preview-package','import-package'):
                from . import module_packages
                if action == 'export-package':
                    return module_packages.export_package(body.get('ids',[]),body.get('name',''),body.get('values',{}),self.game)
                if action == 'preview-package': return module_packages.preview_import(body.get('path',''))
                result = module_packages.import_package(body.get('path',''),body.get('hash',''),body.get('replace',False))
                return result
            if action == "scan-games": return {"games": [str(p) for p in setup.discover()]}
            if action == "browse-game":
                from tkinter import Tk, filedialog
                window = Tk(); window.withdraw()
                try: chosen = filedialog.askdirectory(title="Choose the folder containing TheKarters2.exe", initialdir=str(self.game or Path.home()), parent=window)
                finally: window.destroy()
                if not chosen: return {"message": "Folder selection cancelled"}
                self.game = core.validate_game(Path(chosen))
                core.write_json(core.ROOT / "local/preferences.json", {"game": str(self.game)})
                return {"message": "Game folder selected"}
            if action == "select-game":
                if body.get("discardEdits") is not True: raise ValueError("Save your pending settings before choosing another installation.")
                self.game = core.validate_game(Path(body["path"]))
                core.write_json(core.ROOT / "local/preferences.json", {"game": str(self.game)})
                return {"message": "Game folder selected"}
            if action == "install-loader":
                if self.game is None: raise ValueError("Choose a game first")
                return setup.install_loader(self.game)
            if action == "open-game-folder":
                if self.game is None: raise ValueError("Choose a game first")
                os.startfile(self.game)
                return {"message": "Game folder opened. Start the game yourself."}
            if action == "settings": return self.save_settings(body)
            if action == "open-source-folder":
                path = pack.source_path(body["file"])
                os.startfile(path.parent)
                return {"message": "Source folder opened. External changes can be loaded with Reload file."}
            if action == "save-source":
                path = pack.source_path(body["file"])
                if core.sha256(path) != body.get("hash"): raise ValueError("Source changed outside the app. Reload before saving.")
                content = body["content"]
                if not isinstance(content, str) or len(content) > 1_000_000: raise ValueError("Source too large")
                # Authoring backups stay inside the SDK; game backups are separately managed.
                backup = core.ROOT / "local/source-backups" / f"{__import__('time').time_ns()}-{path.name}"
                core.atomic_write(backup, path.read_bytes())
                core.atomic_write(path, content.encode("utf-8"))
                return {"message": "C# saved", "hash": core.sha256(path)}
            if action == "create-recipe": return {"file": pack.create_recipe(body["name"]), "message": "Recipe created. Edit, then build the pack."}
            if action in ("build", "install", "build-install"):
                if self.game is None: raise ValueError("Choose a game folder first")
                if action in ("install", "build-install"): core.require_game_stopped()
                if action == "install":
                    backup = setup.install_prebuilt(self.game)
                    original = self.config_path.read_text(encoding="utf-8-sig") if self.config_path.exists() else pack.initial_config()
                    seeded = pack.seed_config(original).encode("utf-8")
                    if not self.config_path.exists() or self.config_path.read_bytes() != seeded:
                        core.backup_write(self.game, self.config_path, seeded)
                    return {"message": "Plugin installed. Your existing settings were preserved. Restart the game to load the updated plugin.", "backup": str(backup)}
                built = core.build_plugin(self.game, pack.PROJECT, self.log)
                if action == "build-install":
                    backup = core.deploy_plugin(self.game, Path(built["artifact"]))
                    original = self.config_path.read_text(encoding="utf-8-sig") if self.config_path.exists() else pack.initial_config()
                    seeded = pack.seed_config(original).encode("utf-8")
                    if not self.config_path.exists() or self.config_path.read_bytes() != seeded:
                        core.backup_write(self.game, self.config_path, seeded)
                    return {"message": "One pack plugin installed. Existing settings preserved; new modules start disabled. Start the game yourself to test it.", "backup": str(backup)}
                return {"message": "Pack compiled successfully. Install it when the game is closed."}
            if action == "restore":
                name = body["id"]
                if not str(name).isdigit(): raise ValueError("Invalid backup")
                restored = core.restore_backup(self.game, core.ROOT / "local/backups" / name)
                return {"message": "Restored " + restored.name}
            if action == "diagnose":
                ready = setup.readiness(self.game)
                self.log("Installation: " + ready["message"])
                for error in ready.get("runtimeErrors", []) + ready.get("runtimeWarnings", []): self.log(error)
                return ready
            raise ValueError("Unknown action")


class Server(ThreadingHTTPServer):
    daemon_threads = True
    # Windows SO_REUSEADDR permits multiple live listeners on one port; each
    # listener has a different session token/config snapshot. Require one owner.
    allow_reuse_address = False

    def server_bind(self):
        if os.name == "nt":
            self.socket.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
        super().server_bind()

    def __init__(self, app, port=8765):
        super().__init__(("127.0.0.1", port), Handler)
        self.app = app
        self.origin = f"http://127.0.0.1:{self.server_port}"


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_): pass

    def reply(self, status, body, mime="application/json"):
        if mime == "application/json": body = json.dumps(body).encode("utf-8")
        elif isinstance(body, str): body = body.encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", mime + ("; charset=utf-8" if mime.startswith("text/") or mime == "application/json" else ""))
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'")
        self.send_header("Referrer-Policy", "no-referrer")
        self.end_headers()
        self.wfile.write(body)

    def trusted_host(self):
        return self.headers.get("Host") == urlsplit(self.server.origin).netloc

    def authorized(self):
        origin = self.headers.get("Origin")
        return self.trusted_host() and (origin is None or origin == self.server.origin) and secrets.compare_digest(
            self.headers.get("X-TK2-Token", ""), self.server.app.token)

    def do_GET(self):
        if not self.trusted_host(): return self.reply(403, {"error": "Invalid host"})
        parsed = urlsplit(self.path)
        try:
            if parsed.path.startswith("/api/"):
                if not self.authorized(): return self.reply(403, {"error": "Invalid session"})
                app = self.server.app
                with app.lock:
                    if parsed.path == "/api/health": return self.reply(200, {"app": "TK2 Mod Toolkit", "version": "0.6.7", "pid": os.getpid(), "game": str(app.game)})
                    if parsed.path == "/api/state": return self.reply(200, app.state())
                    if parsed.path == '/api/download':
                        from . import module_packages
                        path = module_packages.download_path(parse_qs(parsed.query).get('id',[''])[0])
                        return self.reply(200,path.read_bytes(),'application/zip')
                    if parsed.path == "/api/source":
                        name = parse_qs(parsed.query).get("file", [""])[0]
                        p = pack.source_path(name)
                        return self.reply(200, {"file": name, "content": p.read_text(encoding="utf-8"), "hash": core.sha256(p)})
                    if parsed.path == "/api/native":
                        query = parse_qs(parsed.query).get("q", [""])[0]
                        count, matches = core.search_native_functions(query)
                        return self.reply(200, {"count": count, "matches": matches})
                    if parsed.path == "/api/functions":
                        query = parse_qs(parsed.query)
                        return self.reply(200, symbols.search(query.get("q", [""])[0], int(query.get("offset", ["0"])[0]), query.get("recovered", ["false"])[0] == "true", query.get("topic", ["all"])[0]))
                    if parsed.path == "/api/function":
                        return self.reply(200, symbols.detail(parse_qs(parsed.query).get("id", [""])[0]))
                return self.reply(404, {"error": "Unknown API"})
            if parsed.path == "/":
                source = (WEB / "index.html").read_text(encoding="utf-8").replace("__SESSION_TOKEN__", self.server.app.token)
                return self.reply(200, source, "text/html")
            files = {"/app.js": WEB / "app.js", "/style.css": WEB / "style.css",
                     "/assets/logo.png": core.ROOT / "assets/Logo.png",
                     "/assets/art.jpg": core.ROOT / "assets/library_600x900_2x.jpg",
                     "/assets/app.ico": core.ROOT / "assets/TheKartersLogoModified.ico"}
            if parsed.path in files:
                path = files[parsed.path]
                return self.reply(200, path.read_bytes(), mimetypes.guess_type(path.name)[0] or "application/octet-stream")
            self.reply(404, {"error": "Not found"})
        except Exception as exc: self.reply(400, {"error": str(exc)})

    def do_POST(self):
        if not self.authorized(): return self.reply(403, {"error": "Invalid session"})
        try:
            length = int(self.headers.get("Content-Length", "0"))
            if not 0 < length <= 2_000_000: raise ValueError("Invalid request size")
            if self.headers.get("Content-Type") != "application/json": raise ValueError("Expected JSON")
            body = json.loads(self.rfile.read(length))
            action = urlsplit(self.path).path.removeprefix("/api/")
            if action == "shutdown":
                # Another window must not stop the process halfway through a build/install.
                with self.server.app.lock:
                    self.reply(200, {"message": "Toolkit closed. You can close this window."})
                    threading.Thread(target=self.server.shutdown, daemon=True).start()
                return
            result = self.server.app.action(action, body)
            self.reply(200, result)
        except Exception as exc:
            if isinstance(exc, PermissionError):
                exc = ValueError("Windows denied access to the game folder. Close Toolkit, run the executable as administrator, then retry the installation.")
            self.server.app.log(str(exc))
            self.reply(409 if isinstance(exc, settings.SettingsConflict) else 400,
                       {"error": str(exc), **({"conflicts": exc.keys} if isinstance(exc, settings.SettingsConflict) else {})})


def open_window(url):
    # Edge app mode provides a desktop window; the default browser is a portable fallback.
    candidates = [Path(os.environ.get("PROGRAMFILES(X86)", "C:/Program Files (x86)")) / "Microsoft/Edge/Application/msedge.exe",
                  Path(os.environ.get("PROGRAMFILES", "C:/Program Files")) / "Microsoft/Edge/Application/msedge.exe"]
    edge = next((p for p in candidates if p.exists()), None)
    if edge:
        subprocess.Popen([str(edge), "--app=" + url, "--window-size=1320,900"], creationflags=core.CREATE_NO_WINDOW)
    else: webbrowser.open(url)


def main():
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument("--no-browser", action="store_true")
    parser.add_argument("--port", type=int)
    parser.add_argument("--game", type=Path)
    args = parser.parse_args()
    session = core.ROOT / "local/garage-session.json"
    if args.port is None and session.is_file():
        # Reopen our live instance; a closed Windows exclusive socket can remain
        # in TIME_WAIT, so cold launches use an OS-assigned free loopback port.
        import urllib.request
        try:
            saved = json.loads(session.read_text(encoding="utf-8"))
            parsed = urlsplit(saved["origin"])
            if parsed.hostname != "127.0.0.1" or parsed.scheme != "http": raise ValueError("Invalid saved Toolkit address")
            request = urllib.request.Request(saved["origin"] + "/api/health", headers={"X-TK2-Token": saved["token"]})
            with urllib.request.urlopen(request, timeout=2) as response: health = json.load(response)
            if health.get("app") in ("TK2 Mod Toolkit", "TK2 Mod Garage"):
                if args.game is not None and health.get("game") != str(core.validate_game(args.game)):
                    raise RuntimeError("Close the existing Toolkit before switching game installations")
                if health.get("version") == "0.6.7":
                    if not args.no_browser: open_window(saved["origin"])
                    return
                request = urllib.request.Request(saved["origin"] + "/api/shutdown", data=b"{}", headers={"X-TK2-Token": saved["token"], "Content-Type": "application/json"})
                with urllib.request.urlopen(request, timeout=3) as response: response.read()
        except (OSError, ValueError, KeyError):
            pass  # Stale or unavailable saved session: start on a fresh port.
    requested_port = args.port if args.port is not None else 0
    try:
        server = Server(Application(args.game), requested_port)
    except OSError:
        # Reuse only our authenticated instance; never open an unrelated service occupying the port.
        import urllib.request
        if not session.is_file():
            raise RuntimeError("Another Toolkit copy is already open on this port. Close it before launching this copy.")
        saved = json.loads(session.read_text(encoding="utf-8"))
        expected = f"http://127.0.0.1:{requested_port}"
        if saved["origin"] != expected: raise RuntimeError("Toolkit port is occupied by another service")
        request = urllib.request.Request(expected + "/api/health", headers={"X-TK2-Token": saved["token"]})
        with urllib.request.urlopen(request, timeout=3) as response:
            health = json.load(response)
            if health.get("app") not in ("TK2 Mod Toolkit", "TK2 Mod Garage"): raise RuntimeError("Unexpected service on Toolkit port")
            if args.game is not None and health.get("game") != str(core.validate_game(args.game)):
                raise RuntimeError("Close the existing Toolkit before switching game installations")
        if health.get("version") == "0.6.7":
            if not args.no_browser: open_window(expected)
            return
        # Upgrade our old authenticated service so launching the new app cannot
        # silently reuse the obsolete backend with its old save/gating behavior.
        request = urllib.request.Request(expected + "/api/shutdown", data=b"{}", headers={"X-TK2-Token": saved["token"], "Content-Type": "application/json"})
        with urllib.request.urlopen(request, timeout=3) as response: response.read()
        import time
        for attempt in range(40):
            try:
                server = Server(Application(args.game), requested_port)
                break
            except OSError:
                if attempt == 39: raise RuntimeError("The old Toolkit is still closing. Try launching again.")
                time.sleep(.1)
    core.write_json(session, {"origin": server.origin, "token": server.app.token})
    if not args.no_browser: open_window(server.origin)
    try: server.serve_forever()
    finally:
        server.server_close()
        if session.exists() and json.loads(session.read_text()).get("token") == server.app.token: session.unlink()


if __name__ == "__main__": main()
