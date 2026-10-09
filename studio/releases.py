"""Verified GitHub Releases discovery and portable Toolkit updates."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import tempfile
import threading
import time
import urllib.error
import urllib.request
import zipfile

REPOSITORY = "The-Karters-Community/TK2-Mod-Toolkit"
RELEASE_API = f"https://api.github.com/repos/{REPOSITORY}/releases/latest"
RELEASE_PAGE = f"https://github.com/{REPOSITORY}/releases/latest"
MAX_DOWNLOAD = 600 * 1024 * 1024
MAX_EXPANDED = 1600 * 1024 * 1024


def version_tuple(value: str) -> tuple[int, ...] | None:
    value = value.strip().lstrip("vV")
    parts = value.split(".")
    if not parts or any(not part.isdigit() for part in parts): return None
    return tuple(int(part) for part in parts)


def _request(url: str):
    request = urllib.request.Request(url, headers={
        "Accept": "application/vnd.github+json",
        "User-Agent": "TK2-Mod-Toolkit",
        "X-GitHub-Api-Version": "2022-11-28",
    })
    return urllib.request.urlopen(request, timeout=8)


def latest_release():
    try:
        with _request(RELEASE_API) as response:
            release = json.load(response)
    except urllib.error.HTTPError as error:
        if error.code == 404: return None
        raise
    if release.get("draft") or release.get("prerelease"): return None
    tag = str(release.get("tag_name", ""))
    parsed = version_tuple(tag)
    if parsed is None: raise ValueError("Latest GitHub release has an invalid version tag.")
    asset_name = f"TK2-Mod-Toolkit-{tag.lstrip('vV')}-win-x64.zip"
    asset = next((item for item in release.get("assets", []) if item.get("name") == asset_name), None)
    digest = str(asset.get("digest", "")) if asset else ""
    digest = digest.removeprefix("sha256:").lower()
    verified = len(digest) == 64 and all(char in "0123456789abcdef" for char in digest)
    download_url = str(asset.get("browser_download_url", "")) if asset else ""
    if download_url and not download_url.startswith(f"https://github.com/{REPOSITORY}/releases/download/"):
        raise ValueError("Release asset download URL is outside the official Toolkit repository.")
    return {
        "version": tag.lstrip("vV"), "tag": tag,
        "url": str(release.get("html_url") or RELEASE_PAGE),
        "asset": asset_name if asset else "", "downloadUrl": download_url,
        "size": int(asset.get("size", 0)) if asset else 0,
        "sha256": digest if verified else "", "verified": verified,
    }


def extract_verified_archive(archive_path: Path, destination: Path, expected_version: str):
    """Extract a bounded ZIP only after path, size, and embedded-version checks."""
    destination.mkdir(parents=True, exist_ok=False)
    total = 0
    with zipfile.ZipFile(archive_path) as archive:
        infos = archive.infolist()
        if not infos or len(infos) > 100_000: raise ValueError("Update archive has an invalid file count.")
        roots = set()
        for info in infos:
            name = info.filename.replace("\\", "/")
            path = PurePosixPath(name)
            if path.is_absolute() or not path.parts or any(":" in part or part == ".." for part in path.parts):
                raise ValueError("Update archive contains an unsafe path.")
            if len(path.parts) < 2: raise ValueError("Update archive must contain one named application folder.")
            roots.add(path.parts[0])
            mode = (info.external_attr >> 16) & 0xFFFF
            if mode and (mode & 0o170000) == 0o120000: raise ValueError("Update archive contains a symbolic link.")
            total += info.file_size
            if info.file_size < 0 or total > MAX_EXPANDED: raise ValueError("Update archive exceeds the expanded size limit.")
        if len(roots) != 1: raise ValueError("Update archive must contain exactly one application folder.")
        archive.extractall(destination)
    root = destination / next(iter(roots))
    manifest_path = root / "portable-manifest.json"
    try: manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as error: raise ValueError("Update package has no valid portable manifest.") from error
    if manifest.get("version") != expected_version or not (root / "TK2 Mod Toolkit.exe").is_file():
        raise ValueError("Update package version or executable does not match the release.")
    if not (root / "tools/update_portable.ps1").is_file():
        raise ValueError("Update package is missing its rollback-safe updater.")
    return root


class ReleaseChecker:
    def __init__(self, current_version: str, root: Path, frozen: bool):
        self.current_version, self.root, self.frozen = current_version, root, frozen
        self._lock = threading.Lock()
        self._status = {"state": "checking", "currentVersion": current_version}
        self._thread = None

    def start(self):
        with self._lock:
            if self._thread and self._thread.is_alive(): return
            self._thread = threading.Thread(target=self.check, daemon=True, name="tk2-release-check")
            self._thread.start()

    def check(self):
        with self._lock: self._status = {"state": "checking", "currentVersion": self.current_version}
        try:
            release = latest_release()
            if release is None:
                result = {"state": "current", "currentVersion": self.current_version}
            else:
                newer = (version_tuple(release["version"]) or ()) > (version_tuple(self.current_version) or ())
                manifest_version = ""
                if self.frozen:
                    try: manifest_version = json.loads((self.root / "portable-manifest.json").read_text(encoding="utf-8")).get("version", "")
                    except (OSError, ValueError, AttributeError): pass
                result = {"state": "available" if newer else "current", "currentVersion": self.current_version, **release,
                          "canInstall": bool(newer and self.frozen and manifest_version == self.current_version and release["verified"] and release["downloadUrl"])}
        except Exception as error:
            result = {"state": "error", "currentVersion": self.current_version, "message": str(error)[:240]}
        with self._lock: self._status = result
        return result

    def snapshot(self):
        with self._lock: return dict(self._status)

    def prepare_update(self):
        info = self.check()
        if info.get("state") != "available": raise ValueError("No newer stable toolkit release is available.")
        if not info.get("canInstall"): raise ValueError("This copy cannot install updates automatically. Use the GitHub release page to update it.")
        response = _request(info["downloadUrl"])
        with response:
            declared = int(response.headers.get("Content-Length", "0") or 0)
            if declared <= 0 or declared > MAX_DOWNLOAD or declared != info.get("size"):
                raise ValueError("Update download has an invalid size.")
            digest = hashlib.sha256()
            parent = Path(tempfile.mkdtemp(prefix="tk2-toolkit-update-", dir=self.root.parent))
            archive = parent / info["asset"]
            try:
                with archive.open("wb") as stream:
                    remaining = MAX_DOWNLOAD
                    while chunk := response.read(1024 * 1024):
                        remaining -= len(chunk)
                        if remaining < 0: raise ValueError("Update download exceeds the size limit.")
                        digest.update(chunk); stream.write(chunk)
                if digest.hexdigest() != info["sha256"]: raise ValueError("Update SHA-256 does not match GitHub's release asset digest.")
                extracted = parent / "extracted"
                package = extract_verified_archive(archive, extracted, info["version"])
                old_local = self.root / "local"
                if old_local.is_dir(): shutil.copytree(old_local, package / "local", dirs_exist_ok=True)
                script = package / "tools/update_portable.ps1"
                command = ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script),
                           "-OldRoot", str(self.root), "-NewRoot", str(package), "-WaitPid", str(os.getpid()),
                           "-BackupRoot", str(self.root.parent / (self.root.name + ".backup-" + time.strftime("%Y%m%d-%H%M%S")))]
                subprocess.Popen(command, cwd=str(self.root.parent), creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
                return {"message": f"Verified Toolkit {info['version']} downloaded. The app will close and reopen to finish updating."}
            except Exception:
                shutil.rmtree(parent, ignore_errors=True)
                raise
