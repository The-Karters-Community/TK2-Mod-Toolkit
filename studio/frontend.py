"""Compose readable frontend templates for the source-mode HTTP server."""
from pathlib import Path
import re

INCLUDE = re.compile(r"<!--\s*@include\s+([^\s]+)\s*-->")


def render_html(path: Path, root: Path, stack: tuple[Path, ...] = ()) -> str:
    path, root = path.resolve(), root.resolve()
    if not path.is_relative_to(root) or path.suffix != '.html':
        raise ValueError('Frontend template must stay inside the web source directory.')
    if path in stack:
        raise ValueError('Circular frontend template include.')
    source = path.read_text(encoding='utf-8')
    return INCLUDE.sub(lambda match: render_html(path.parent / match[1], root, (*stack, path)), source)
