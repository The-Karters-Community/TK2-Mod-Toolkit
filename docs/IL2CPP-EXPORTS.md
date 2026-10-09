# Local IL2CPP exports and source map

This project does not contain a complete original C# source tree for The Karters 2. The supported game build is IL2CPP: Unity compiled managed code to native code in `GameAssembly.dll`. Original comments, local names, compiler-independent C# structure, stripped implementations and the Unity project are not present to recover verbatim.

The practical modder workflow has three different evidence layers:

| Layer | What it contains | What it is useful for | What it does not establish |
| --- | --- | --- | --- |
| Generated DummyDll declarations | Type, member, parameter and return signatures, metadata tokens and native addresses. Methods often have empty or default-return bodies. | Finding classes, overloads and candidate hook signatures. | Executable game logic or original C# implementation. |
| Ghidra native pseudocode | C-like decompilation of native machine code, indexed by native address and function label. | Following branches, field reads/writes and call relationships as evidence for a feature. | Original C# syntax, reliable variable names, or correct semantics without review. |
| `src/Reconstructed/` | Maintained, readable C# models/adapters for a selected set of reviewed behavior. | Editing and testing specific mod logic with provenance. | A full replacement of the game's classes or methods. |

Generated declarations and Ghidra exports are game-derived local analysis data. Keep them under ignored `local/` or `exports/`; do not commit or include them in toolkit releases. This separation is about redistribution rights, not secrecy: the generated bodies are code-derived from the game's copyrighted binary. The scripts and documentation in this repository are the reproducible, shareable part.

The `Launch Studio.cmd` web app separates the declaration browser from a **Search native pseudocode** panel. It searches exported function names and body text, then opens the selected `.c` file in a read-only viewer. Its first search builds an ignored SQLite full-text index under `local/ghidra/`; following searches update that index when export files change. No search corpus is bundled into Git or the portable release. Contributors can regenerate exports against their own exact game build, then use the same search UI.

## Regenerate the declaration layer

Install an Il2CppDumper-compatible dumper and point it at the exact game `GameAssembly.dll` and `global-metadata.dat`. Keep the output outside Git. For the standard Il2CppDumper `DummyDll` output, dnSpy Console can turn those declaration assemblies into browseable C# files:

```powershell
$dummy = 'C:\Games\The Karters 2 Turbo Charged\Il2CppDumperOutput\DummyDll'
$out = 'local\decompiled-csharp\AllAssemblies'
$dnspy = 'C:\Tools\dnSpy\dnSpy.Console.exe'
New-Item -ItemType Directory -Force -Path $out | Out-Null
& $dnspy -r --no-color --threads 8 --asm-path $dummy --no-gac --no-stdlib -o $out $dummy
```

These are signature declarations with placeholder bodies. Do not copy them into `src/Reconstructed/` or describe them as complete decompiled implementations. Keep the dumper log and binary hashes with the local export.

## Regenerate the native function index and selected bodies

Use the existing Ghidra project for the same game build, open it read-only, and target `GameAssembly.dll`. `ExportTk2.java` writes a complete function index plus pseudocode for functions matching a regular expression. For a small reviewed sample:

```powershell
$ghidra = 'C:\Path\To\ghidra\support\analyzeHeadless.bat'
$projectDir = 'C:\Path\To\ghidraOutput'
$out = 'local\ghidra\sample'
& $ghidra $projectDir 'TK2_1_4_18' -process 'GameAssembly.dll' -noanalysis -readOnly `
  -scriptPath (Join-Path (Get-Location) 'tools\ghidra') `
  -postScript ExportTk2.java $out '^PixelKartPhysics\$\$AddVelocity$' 1
```

To map all generated Assembly-CSharp type declarations to candidate native functions and prepare the filter for a larger export:

```powershell
python tools\il2cpp_source_map.py `
  --declarations local\decompiled-csharp\AllAssemblies\Assembly-CSharp `
  --function-index local\ghidra\functions.jsonl `
  --filter-out local\ghidra\assembly-csharp-filter.txt `
  --manifest-out local\ghidra\assembly-csharp-source-map.json
```

For example, to export up to 20,000 functions whose names match the generated class list:

```powershell
$ghidra = 'C:\Path\To\ghidra\support\analyzeHeadless.bat'
$projectDir = 'C:\Path\To\ghidraOutput'
$filterFile = (Resolve-Path 'local\ghidra\assembly-csharp-filter.txt').Path
$out = 'local\ghidra\assembly-csharp-full'
& $ghidra $projectDir 'TK2_1_4_18' -process 'GameAssembly.dll' -noanalysis -readOnly `
  -scriptPath (Join-Path (Get-Location) 'tools\ghidra') `
  -postScript ExportTk2.java $out ('@' + $filterFile) 20000
```

A broad export may take a long time and produce a large local corpus; start with one subsystem if you only need a feature. Inspect the completion count and failures in `summary.json`. The current script scans the whole native function table to create its index even when pseudocode is limited. `-readOnly` prevents the export from modifying the analyzed program.

## Turn evidence into useful C#

1. Start from the exact managed type and overload in the DummyDll/API browser.
2. Match its native address to the Ghidra function index. Check aliases and shared implementations; a matching class-like name alone is insufficient.
3. Read the function and relevant callees/callers. Record native addresses, binary and metadata hashes, and any unresolved behavior.
4. Write a small semantic C# model only for behavior supported by the evidence. Label assumptions and keep native runtime initialization/marshaling details separate.
5. Test pure logic and validate the adapter in-game before relying on it. Add the source, evidence and tests to `src/Reconstructed/` only after review.

This grows a trustworthy modding reference subsystem by subsystem. It cannot produce the original complete C# project automatically; doing so would require source and information that IL2CPP compilation does not preserve.
