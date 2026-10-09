param(
    [Parameter(Mandatory=$true)][string]$OldRoot,
    [Parameter(Mandatory=$true)][string]$NewRoot,
    [Parameter(Mandatory=$true)][int]$WaitPid,
    [Parameter(Mandatory=$true)][string]$BackupRoot
)
$ErrorActionPreference = 'Stop'
$old = [IO.Path]::GetFullPath($OldRoot).TrimEnd('\')
$new = [IO.Path]::GetFullPath($NewRoot).TrimEnd('\')
$backup = [IO.Path]::GetFullPath($BackupRoot).TrimEnd('\')
$parentPrefix = (Split-Path -Parent $old).TrimEnd('\') + '\'
if (-not $new.StartsWith($parentPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    -not $backup.StartsWith($parentPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $new 'portable-manifest.json')) -or
    -not (Test-Path -LiteralPath (Join-Path $new 'TK2 Mod Toolkit.exe'))) { exit 2 }
while (Get-Process -Id $WaitPid -ErrorAction SilentlyContinue) { Start-Sleep -Milliseconds 250 }
try {
    Move-Item -LiteralPath $old -Destination $backup
    Move-Item -LiteralPath $new -Destination $old
    Start-Process -FilePath (Join-Path $old 'TK2 Mod Toolkit.exe') -WorkingDirectory $old
} catch {
    if (-not (Test-Path -LiteralPath $old) -and (Test-Path -LiteralPath $backup)) {
        Move-Item -LiteralPath $backup -Destination $old
    }
    exit 3
}
$temporaryParent = Split-Path -Parent (Split-Path -Parent $new)
if ((Split-Path -Leaf $temporaryParent).StartsWith('tk2-toolkit-update-')) {
    Remove-Item -LiteralPath $temporaryParent -Recurse -Force -ErrorAction SilentlyContinue
}
exit 0
