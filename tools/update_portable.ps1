param(
    [Parameter(Mandatory=$true)][string]$OldRoot,
    [Parameter(Mandatory=$true)][string]$NewRoot,
    [Parameter(Mandatory=$true)][int]$WaitPid,
    # Compatibility with update callers from older releases; no permanent backup is kept.
    [string]$BackupRoot,
    [ValidateRange(1,120)][int]$StartupTimeoutSeconds = 60
)
$ErrorActionPreference = 'Stop'
$old = [IO.Path]::GetFullPath($OldRoot).TrimEnd('\')
$new = [IO.Path]::GetFullPath($NewRoot).TrimEnd('\')
$parent = Split-Path -Parent $old
$temporary = Split-Path -Parent (Split-Path -Parent $new)
$previous = Join-Path $temporary 'previous'
$failed = Join-Path $temporary 'failed'
# Validate final paths before moving or recursively cleaning anything.
if (-not $parent -or $old -eq [IO.Path]::GetPathRoot($old).TrimEnd('\') -or
    (Split-Path -Parent $temporary) -ne $parent -or
    -not (Split-Path -Leaf $temporary).StartsWith('tk2-toolkit-update-') -or
    (Split-Path -Leaf (Split-Path -Parent $new)) -ne 'extracted' -or
    $old -eq $new -or -not (Test-Path -LiteralPath $old -PathType Container) -or
    (Test-Path -LiteralPath $previous) -or (Test-Path -LiteralPath $failed) -or
    -not (Test-Path -LiteralPath (Join-Path $new 'portable-manifest.json')) -or
    -not (Test-Path -LiteralPath (Join-Path $new 'TK2 Mod Toolkit.exe'))) { exit 2 }
foreach ($path in @($old, $temporary, (Split-Path -Parent $new), $new)) {
    if ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) { exit 2 }
}
$manifest = Get-Content -LiteralPath (Join-Path $new 'portable-manifest.json') -Raw | ConvertFrom-Json
if ($manifest.version -notmatch '^\d+\.\d+\.\d+$') { exit 2 }
$name = Split-Path -Leaf $old
$targetName = $name
if ($name -match '^TK2-Mod-Toolkit-\d+\.\d+\.\d+-win-x64$') {
    $targetName = "TK2-Mod-Toolkit-$($manifest.version)-win-x64"
} elseif ($name -match '^TK2 Mod Toolkit \d+\.\d+\.\d+$') {
    $targetName = "TK2 Mod Toolkit $($manifest.version)"
}
$target = [IO.Path]::GetFullPath((Join-Path $parent $targetName)).TrimEnd('\')
if ((Split-Path -Parent $target) -ne $parent -or $target -eq $temporary -or
    ($target -ne $old -and (Test-Path -LiteralPath $target))) { exit 2 }
while (Get-Process -Id $WaitPid -ErrorAction SilentlyContinue) { Start-Sleep -Milliseconds 250 }
$launched = $null
try {
    Move-Item -LiteralPath $old -Destination $previous
    Move-Item -LiteralPath $new -Destination $target
    $launched = Start-Process -FilePath (Join-Path $target 'TK2 Mod Toolkit.exe') -WorkingDirectory $target -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    $ready = $false
    while ([DateTime]::UtcNow -lt $deadline) {
        if (-not (Get-Process -Id $launched.Id -ErrorAction SilentlyContinue)) { break }
        try {
            $session = Get-Content -LiteralPath (Join-Path $target 'local/garage-session.json') -Raw | ConvertFrom-Json
            if ($session.origin -match '^http://127\.0\.0\.1:\d+$') {
                $health = Invoke-RestMethod -Uri ($session.origin + '/api/health') -Headers @{'X-TK2-Token'=$session.token} -TimeoutSec 2
                if ($health.app -eq 'TK2 Mod Toolkit' -and $health.version -eq $manifest.version -and $health.pid -eq $launched.Id) {
                    $ready = $true
                    break
                }
            }
        } catch { }
        Start-Sleep -Milliseconds 250
    }
    if (-not $ready) { throw 'Updated Toolkit did not report a healthy startup.' }
} catch {
    $failure = $_.Exception.Message
    if ($launched) { Stop-Process -Id $launched.Id -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $previous) {
        if (Test-Path -LiteralPath $target) { Move-Item -LiteralPath $target -Destination $failed }
        Move-Item -LiteralPath $previous -Destination $old
        Start-Process -FilePath (Join-Path $old 'TK2 Mod Toolkit.exe') -WorkingDirectory $old
    }
    [IO.File]::WriteAllText((Join-Path $temporary 'update-error.txt'), $failure)
    exit 3
}
# Cleanup only after a healthy startup. Never recurse through user-added junctions.
$links = Get-ChildItem -LiteralPath $temporary -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }
if ($links) {
    [IO.File]::WriteAllText((Join-Path $temporary 'cleanup-warning.txt'), 'Update succeeded. Staging cleanup skipped because it contains a junction or symlink.')
} else {
    Remove-Item -LiteralPath $temporary -Recurse -Force
}
exit 0
