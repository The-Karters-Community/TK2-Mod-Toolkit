"""Loopback-only desktop web UI, without third-party Python dependencies."""
from __future__ import annotations
import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import mimetypes
import os
from pathlib import Path
import secrets
import subprocess
import threading
import webbrowser
from urllib.parse import urlsplit, parse_qs
from . import core, pack

WEB = core.ROOT / "studio/web"


class Application:
    def __init__(self, game=core.DEFAULT_GAME):
        self.game = core.validate_game(Path(game))
        self.token = secrets.token_urlsafe(32)
        self.lock = threading.RLock()
        self.logs = []

    def log(self, message):
        self.logs.append(str(message))
        self.logs = self.logs[-100:]

    @property
    def config_path(self):
        return self.game / "BepInEx/config" / core.CONFIG_NAME

    def config_data(self):
        data = self.config_path.read_bytes() if self.config_path.exists() else b""
        text = data.decode("utf-8-sig")
        settings = pack.defaults()
        entries = core.parse_cfg_settings(text)
        for entry in entries:
            compound = entry["section"] + "/" + entry["key"]
            if compound not in settings: continue
            kind = pack.schema()[(entry["section"], entry["key"])][0]
            try:
                settings[compound] = entry["value"].lower() == "true" if kind == "bool" else (
                    float(entry["value"]) if kind in ("float", "int") else entry["value"])
            except ValueError: pass
        return {"settings": settings, "configHash": hashlib.sha256(data).hexdigest(),
                "recipes": [e for e in entries if e["section"].startswith("Recipe.")]}

    def state(self):
        backups = []
        for p in sorted((core.ROOT / "local/backups").glob("*/receipt.json"), reverse=True):
            try:
                receipt = json.loads(p.read_text())
                if receipt["game"] == str(self.game):
                    backups.append({"id": p.parent.name, "file": receipt["relative"], "existed": receipt["existed"]})
            except (OSError, ValueError, KeyError): pass
        installed = self.game / "BepInEx/plugins/TK2-Mod-Studio/TK2.Customization.dll"
        artifact = pack.ARTIFACT / "TK2.Customization.dll"
        current = installed.is_file() and artifact.is_file() and core.sha256(installed) == core.sha256(artifact)
        return {"game": str(self.game), "installed": installed.is_file(), "packCurrent": current, "features": pack.FEATURES,
                "plugin": "TK2.Customization.dll", "pluginCount": len([p for p in core.plugins(self.game) if p["enabled"]]),
                "runtimeValidated": False, "files": pack.source_files(), "logs": self.logs,
                "backups": backups[:30], **self.config_data()}

    def save_settings(self, body):
        current = self.config_data()
        if body.get("hash") != current["configHash"]: raise ValueError("Settings changed outside the app. Reload before saving.")
        values = pack.validate(body["values"])
        raw = self.config_path.read_bytes().decode("utf-8-sig") if self.config_path.exists() else "# TK2 Mod Garage Pack\n"
        entries = {e["section"] + "/" + e["key"]: e for e in current["recipes"]}
        for compound, value in body.get("recipes", {}).items():
            if compound not in entries: raise ValueError("Unknown recipe setting")
            entry = entries[compound]
            # Gameplay recipes remain runtime-gated by their declared ChangesGameplay property.
            values[(entry["section"], entry["key"])] = core.validate_cfg_value(entry, str(value).lower() if isinstance(value, bool) else str(value))
        changed = core.update_cfg(raw, values).encode("utf-8")
        if self.config_path.exists() and self.config_path.read_bytes() == changed: return {"message": "Settings already saved"}
        backup = core.backup_write(self.game, self.config_path, changed)
        return {"message": "Settings saved. The pack reloads them while the game is running.", "backup": str(backup)}

    def action(self, action, body):
        with self.lock:
            if action == "settings": return self.save_settings(body)
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
            if action in ("build", "install"):
                if action == "install": core.require_game_stopped()
                built = core.build_plugin(self.game, pack.PROJECT, self.log)
                if action == "install":
                    backup = core.deploy_plugin(self.game, Path(built["artifact"]))
                    if not self.config_path.exists():
                        core.backup_write(self.game, self.config_path, pack.initial_config().encode("utf-8"))
                    return {"message": "One pack plugin installed. Features start disabled. Start the game yourself to test it.", "backup": str(backup)}
                return {"message": "Pack compiled successfully. Install it when the game is closed."}
            if action == "restore":
                name = body["id"]
                if not str(name).isdigit(): raise ValueError("Invalid backup")
                restored = core.restore_backup(self.game, core.ROOT / "local/backups" / name)
                return {"message": "Restored " + restored.name}
            if action == "diagnose": return core.diagnose(self.game)
            raise ValueError("Unknown action")


class Server(ThreadingHTTPServer):
    daemon_threads = True

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
                    if parsed.path == "/api/health": return self.reply(200, {"app": "TK2 Mod Garage", "pid": os.getpid(), "game": str(app.game)})
                    if parsed.path == "/api/state": return self.reply(200, app.state())
                    if parsed.path == "/api/source":
                        name = parse_qs(parsed.query).get("file", [""])[0]
                        p = pack.source_path(name)
                        return self.reply(200, {"file": name, "content": p.read_text(encoding="utf-8"), "hash": core.sha256(p)})
                    if parsed.path == "/api/native":
                        query = parse_qs(parsed.query).get("q", [""])[0]
                        count, matches = core.search_native_functions(query)
                        return self.reply(200, {"count": count, "matches": matches})
                return self.reply(404, {"error": "Unknown API"})
            if parsed.path == "/":
                source = (WEB / "index.html").read_text(encoding="utf-8").replace("__SESSION_TOKEN__", self.server.app.token)
                return self.reply(200, source, "text/html")
            files = {"/app.js": WEB / "app.js", "/style.css": WEB / "style.css",
                     "/assets/logo.png": core.ROOT / "assets/Logo.png",
                     "/assets/art.jpg": core.ROOT / "assets/library_600x900_2x.jpg"}
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
                    self.reply(200, {"message": "Garage closed. You can close this window."})
                    threading.Thread(target=self.server.shutdown, daemon=True).start()
                return
            result = self.server.app.action(action, body)
            self.reply(200, result)
        except Exception as exc:
            self.server.app.log(str(exc))
            self.reply(400, {"error": str(exc)})


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
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--game", type=Path, default=core.DEFAULT_GAME)
    args = parser.parse_args()
    session = core.ROOT / "local/garage-session.json"
    try:
        server = Server(Application(args.game), args.port)
    except OSError:
        # Reuse only our authenticated instance; never open an unrelated service occupying the port.
        import urllib.request
        saved = json.loads(session.read_text())
        expected = f"http://127.0.0.1:{args.port}"
        if saved["origin"] != expected: raise RuntimeError("Garage port is occupied by another service")
        request = urllib.request.Request(expected + "/api/health", headers={"X-TK2-Token": saved["token"]})
        with urllib.request.urlopen(request, timeout=3) as response:
            health = json.load(response)
            if health.get("app") != "TK2 Mod Garage": raise RuntimeError("Unexpected service on Garage port")
            if health.get("game") != str(core.validate_game(args.game)):
                raise RuntimeError("Close the existing Garage before switching game installations")
        if not args.no_browser: open_window(expected)
        return
    core.write_json(session, {"origin": server.origin, "token": server.app.token})
    if not args.no_browser: open_window(server.origin)
    try: server.serve_forever()
    finally:
        server.server_close()
        if session.exists() and json.loads(session.read_text()).get("token") == server.app.token: session.unlink()


if __name__ == "__main__": main()
