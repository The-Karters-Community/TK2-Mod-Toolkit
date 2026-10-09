"""Search and read local Ghidra pseudocode without presenting it as original C#."""
from __future__ import annotations

import hashlib
import json
from contextlib import closing
from pathlib import Path
import re
import sqlite3
import threading

from . import core

_lock = threading.Lock()
_FORMAT = "tk2-ghidra-fts-v1"
_HEADER = re.compile(r"^/\* Ghidra pseudocode, not original C#; (.+?) @ ([0-9A-Fa-f]+) \*/", re.M)
_TERMS = re.compile(r"[\w$]+", re.UNICODE)
_MAX_CODE_BYTES = 2_000_000


def _files(root: Path) -> list[Path]:
    base = root / "local/ghidra"
    if not base.is_dir():
        return []
    return sorted({p for folder in base.glob("**/pseudocode") for p in folder.glob("*.c") if p.is_file()})


def _stamp(root: Path, files: list[Path]) -> str:
    digest = hashlib.sha256(_FORMAT.encode())
    for path in files:
        stat = path.stat()
        digest.update(path.relative_to(root).as_posix().encode("utf-8"))
        digest.update(f"\0{stat.st_size}\0{stat.st_mtime_ns}\n".encode("ascii"))
    return digest.hexdigest()


def _index(root: Path) -> tuple[Path | None, int]:
    files = _files(root)
    if not files:
        return None, 0
    expected = _stamp(root, files)
    db_path = root / "local/ghidra/pseudocode-search.sqlite3"
    db_path.parent.mkdir(parents=True, exist_ok=True)
    with _lock:
        with closing(sqlite3.connect(db_path, timeout=60)) as db:
            with db:
                db.execute("CREATE VIRTUAL TABLE IF NOT EXISTS pseudocode USING fts5(path, name, body)")
                db.execute("CREATE TABLE IF NOT EXISTS metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL)")
                current = db.execute("SELECT value FROM metadata WHERE key='stamp'").fetchone()
                if not current or current[0] != expected:
                    db.execute("DELETE FROM pseudocode")
                    for path in files:
                        body = path.read_text(encoding="utf-8", errors="replace")
                        match = _HEADER.search(body[:1000])
                        name = match.group(1) if match else path.stem
                        db.execute("INSERT INTO pseudocode(path, name, body) VALUES (?, ?, ?)",
                                   (path.relative_to(root).as_posix(), name, body))
                    db.execute("INSERT OR REPLACE INTO metadata(key,value) VALUES ('stamp',?)", (expected,))
    return db_path, len(files)


def search(query: str, offset: int = 0, limit: int = 50, root: Path | None = None) -> dict:
    root = Path(root or core.ROOT).resolve()
    files = _files(root)
    if not files:
        return {"total": 0, "indexed": 0, "results": [],
                "message": "No local Ghidra pseudocode exports found. Generate them using docs/IL2CPP-EXPORTS.md."}
    terms = _TERMS.findall(query[:240])
    if not terms:
        return {"total": 0, "indexed": len(files), "results": [],
                "message": "Enter a class, method, field, or code term to search the local pseudocode exports."}
    match_query = " AND ".join('"' + term.replace('"', '""') + '"' for term in terms)
    db_path, indexed = _index(root)
    assert db_path is not None
    offset = max(0, min(int(offset), 100_000))
    limit = max(1, min(int(limit), 100))
    with closing(sqlite3.connect(db_path, timeout=60)) as db:
        total = db.execute("SELECT count(*) FROM pseudocode WHERE pseudocode MATCH ?", (match_query,)).fetchone()[0]
        rows = db.execute(
            "SELECT path, name, snippet(pseudocode, 2, '', '', ' … ', 18) "
            "FROM pseudocode WHERE pseudocode MATCH ? ORDER BY bm25(pseudocode), path LIMIT ? OFFSET ?",
            (match_query, limit, offset),
        ).fetchall()
    results = []
    for relative, name, snippet in rows:
        address = Path(relative).name.split("_", 1)[0]
        results.append({"id": relative, "name": name, "address": address, "snippet": snippet})
    return {"total": total, "indexed": indexed, "results": results,
            "next": offset + limit if offset + limit < total else None,
            "message": "Ghidra native pseudocode; approximate C-like output, not original C#. Review aliases, calls, and initialization before drawing conclusions."}


def read(identity: str, root: Path | None = None) -> dict:
    root = Path(root or core.ROOT).resolve()
    if not identity or "\\" in identity or Path(identity).is_absolute() or ".." in Path(identity).parts:
        raise ValueError("Invalid pseudocode path")
    allowed = {p.relative_to(root).as_posix(): p for p in _files(root)}
    path = allowed.get(identity)
    if path is None:
        raise ValueError("Pseudocode file is not in the local Ghidra export")
    data = path.read_bytes()
    truncated = len(data) > _MAX_CODE_BYTES
    code = data[:_MAX_CODE_BYTES].decode("utf-8", errors="replace")
    return {"id": identity, "code": code, "truncated": truncated,
            "message": "File truncated at 2 MB for display." if truncated else ""}
