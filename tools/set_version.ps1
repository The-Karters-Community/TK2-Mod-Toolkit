param([Parameter(Mandatory=$true)][string]$Version)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must use MAJOR.MINOR.PATCH, for example 0.6.24.' }
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$pythonPath = Join-Path $root 'studio/version.py'
$pluginPath = Join-Path $root 'plugins/TK2.Customization/ToolkitVersion.cs'
$python = [IO.File]::ReadAllText($pythonPath)
$plugin = [IO.File]::ReadAllText($pluginPath)
if ($python -notmatch '(?m)^APP_VERSION\s*=\s*"\d+\.\d+\.\d+"(?=\r?$)') { throw 'Could not find APP_VERSION in studio/version.py.' }
if ($plugin -notmatch '(?m)^\s*internal const string Value = "\d+\.\d+\.\d+";(?=\r?$)') { throw 'Could not find ToolkitVersion.Value in ToolkitVersion.cs.' }
$python = [Regex]::Replace($python, '(?m)(^APP_VERSION\s*=\s*")\d+\.\d+\.\d+("(?=\r?$))', "`$1$Version`$2", 1)
$plugin = [Regex]::Replace($plugin, '(?m)(^\s*internal const string Value = ")\d+\.\d+\.\d+(";(?=\r?$))', "`$1$Version`$2", 1)
[IO.File]::WriteAllText($pythonPath, $python, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($pluginPath, $plugin, [Text.UTF8Encoding]::new($false))
Write-Output "Toolkit version set to $Version. Use this only while preparing a release; builds do not alter the version."
