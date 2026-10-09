#!/usr/bin/env python3
"""Build an ignored, local map between Il2CppDumper declarations and Ghidra.

This deliberately indexes evidence instead of claiming to decompile IL2CPP back
to original C#. Dummy DLL method bodies are placeholders; Ghidra output is native
pseudocode. Both are useful for finding and reviewing behavior when kept distinct.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path


ADDRESS_RE = re.compile(r'\[Address\(RVA\s*=\s*"0x[0-9A-Fa-f]+".*?VA\s*=\s*"0x([0-9A-Fa-f]+)"\)\]', re.S)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def build_map(declarations: Path, function_index: Path, filter_file: Path, manifest: Path) -> dict:
    source_files = sorted(declarations.rglob("*.cs"))
    if not source_files:
        raise ValueError(f"No C# declaration files found under {declarations}")

    # Il2CppDumper names one type per file. Ghidra commonly names methods as
    # Type$$Method; this joins those naming conventions without guessing behavior.
    class_names = sorted({p.stem for p in source_files})
    patterns = [r"^" + re.escape(name) + r"\$\$" for name in class_names]
    filter_file.parent.mkdir(parents=True, exist_ok=True)
    filter_file.write_text("\n".join(patterns) + "\n", encoding="utf-8")

    addresses: set[int] = set()
    for source in source_files:
        text = source.read_text(encoding="utf-8", errors="replace")
        addresses.update(int(match.group(1), 16) for match in ADDRESS_RE.finditer(text))

    class_prefix = re.compile(r"^(?:" + "|".join(re.escape(name) for name in class_names) + r")\$\$")
    candidate_count = 0
    address_matches = 0
    function_rows = 0
    with function_index.open(encoding="utf-8") as stream:
        for line in stream:
            if not line.strip():
                continue
            row = json.loads(line)
            function_rows += 1
            if class_prefix.search(row.get("name", "")):
                candidate_count += 1
                try:
                    address_matches += int(row["address"], 16) in addresses
                except (KeyError, TypeError, ValueError):
                    pass

    result = {
        "format": 1,
        "scope": "local generated index; no game source bodies",
        "declarationRoot": str(declarations.resolve()),
        "declarationFiles": len(source_files),
        "distinctNativeAddressesInDeclarations": len(addresses),
        "functionIndex": str(function_index.resolve()),
        "functionIndexSha256": sha256(function_index),
        "nativeFunctionRows": function_rows,
        "matchingClassNamedFunctions": candidate_count,
        "matchingAddressesPresentInDeclarations": address_matches,
        "filterFile": str(filter_file.resolve()),
        "filterPatterns": len(patterns),
        "limitations": [
            "Il2CppDumper DummyDll bodies are placeholders, not recovered method implementations.",
            "Ghidra pseudocode is decompiled native C-like output, not original C#.",
            "Name and address matches require manual overload, alias, and behavior review.",
        ],
    }
    manifest.parent.mkdir(parents=True, exist_ok=True)
    manifest.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--declarations", type=Path, required=True, help="Root containing generated per-type .cs declaration files")
    parser.add_argument("--function-index", type=Path, required=True, help="Ghidra ExportTk2 functions.jsonl")
    parser.add_argument("--filter-out", type=Path, required=True, help="Output Ghidra regex list (keep under ignored local/)")
    parser.add_argument("--manifest-out", type=Path, required=True, help="Output JSON counts/provenance (keep under ignored local/)")
    args = parser.parse_args()
    try:
        result = build_map(args.declarations, args.function_index, args.filter_out, args.manifest_out)
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        parser.error(str(exc))
    print(json.dumps({key: result[key] for key in (
        "declarationFiles", "distinctNativeAddressesInDeclarations", "nativeFunctionRows",
        "matchingClassNamedFunctions", "matchingAddressesPresentInDeclarations",
    )}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
