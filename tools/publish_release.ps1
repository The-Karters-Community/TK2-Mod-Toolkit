param([switch]$Publish)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$versionSource = Get-Content -LiteralPath (Join-Path $repoRoot 'studio/version.py') -Raw
if ($versionSource -notmatch 'APP_VERSION\s*=\s*"([0-9]+\.[0-9]+\.[0-9]+)"') { throw 'Could not read the toolkit version.' }
$version = $Matches[1]
$tag = "v$version"
$archive = Join-Path $repoRoot "artifacts/portable/TK2-Mod-Toolkit-$version-win-x64.zip"
if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) { throw "Build the portable archive first: $archive" }
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'Install GitHub CLI (gh) and authenticate before creating a release.' }
Push-Location $repoRoot
try {
    $changes = git status --porcelain
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the repository status.' }
    if ($changes) { throw 'Commit or discard working-tree changes before releasing.' }
    $head = (git rev-parse HEAD).Trim()
    $remoteHead = (git ls-remote origin refs/heads/main).Split("`t")[0]
    if ($LASTEXITCODE -ne 0 -or -not $remoteHead -or $head -ne $remoteHead) { throw 'Push this exact commit to origin/main before creating its release.' }
    $existing = gh release view $tag --json tagName 2>$null
    if ($LASTEXITCODE -eq 0 -and $existing) { throw "Release $tag already exists." }
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    $notes = @"
TK2 Mod Toolkit $version for The Karters 2 Turbo Charged.

Portable Windows x64 package. The archive SHA-256 is $hash.
See README.md for prerequisites, installation, and update behavior.
"@
    $notesPath = Join-Path $env:TEMP "tk2-release-$version-notes.md"
    [IO.File]::WriteAllText($notesPath, $notes, [Text.UTF8Encoding]::new($false))
    $arguments = @('release','create',$tag,$archive,'--title',"TK2 Mod Toolkit $version",'--notes-file',$notesPath,'--target',$head)
    if (-not $Publish) { $arguments += '--draft' }
    gh @arguments
    if ($LASTEXITCODE -ne 0) { throw 'GitHub CLI did not create the release.' }
    Write-Output "Release $tag created. Archive SHA-256: $hash"
    if (-not $Publish) { Write-Output 'This is a draft. Publish it on GitHub after review to enable in-app update checks.' }
} finally { Pop-Location }
