param([string]$GamePath, [string]$LoaderPath, [switch]$RebuildPlugin, [switch]$IncludeLoader)
$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $repoRoot
try {
    $pythonCommand = Get-Command python -ErrorAction Stop
    & $pythonCommand.Source -c "import sys,struct; assert sys.version_info >= (3,10) and struct.calcsize('P') == 8, 'Use Python 3.10 or newer, 64-bit'"
    if ($LASTEXITCODE -ne 0) { throw 'Python 3.10 or newer (64-bit) is required.' }
    $nodeCommand = Get-Command node -ErrorAction SilentlyContinue
    $npmCommand = Get-Command npm -ErrorAction SilentlyContinue
    if (-not $nodeCommand -or -not $npmCommand) { throw 'Node.js 20.19+ and npm are required to build the Vite frontend.' }
    $nodeParts = ((& $nodeCommand.Source --version).TrimStart('v') -split '\.') | ForEach-Object { [int]$_ }
    if ($nodeParts[0] -lt 20 -or $nodeParts[0] -eq 21 -or ($nodeParts[0] -eq 20 -and $nodeParts[1] -lt 19) -or ($nodeParts[0] -eq 22 -and $nodeParts[1] -lt 12)) { throw 'Vite requires Node.js 20.19+ or 22.12+.' }
    & $npmCommand.Source ci
    if ($LASTEXITCODE -ne 0) { throw 'Could not install locked frontend dependencies at the repository root.' }
    & $npmCommand.Source run build
    if ($LASTEXITCODE -ne 0) { throw 'React frontend build failed.' }
    $buildPython = Join-Path $repoRoot 'local/portable-build-env/Scripts/python.exe'
    if (-not (Test-Path -LiteralPath $buildPython)) {
        & $pythonCommand.Source -m venv (Join-Path $repoRoot 'local/portable-build-env')
        if ($LASTEXITCODE -ne 0) { throw 'Could not create the isolated packaging environment.' }
    }
    & $buildPython -m pip install --disable-pip-version-check -r (Join-Path $PSScriptRoot 'requirements-build.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Could not install packaging dependencies. Check the output above.' }
    & (Join-Path $PSScriptRoot 'export_icon.ps1')
    $buildArguments = @((Join-Path $PSScriptRoot 'build_portable.py'), '--python', $buildPython)
    if ($GamePath) { $buildArguments += @('--game', $GamePath) }
    if ($LoaderPath) { $buildArguments += @('--loader', $LoaderPath) }
    if ($IncludeLoader) { $buildArguments += '--include-loader' }
    if ($RebuildPlugin) { $buildArguments += '--rebuild-plugin' }
    & $buildPython @buildArguments
    if ($LASTEXITCODE -ne 0) { throw 'Toolkit build failed. Existing portable output is preserved.' }
} finally { Pop-Location }
