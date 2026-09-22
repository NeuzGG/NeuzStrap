# Builds NeuzStrap and renders every window to PNG (nothing is launched, installed or changed).
# Usage:  powershell -ExecutionPolicy Bypass -File tools/screenshots.ps1 [-Out docs/screenshots] [-Demo]
#   -Demo  uses a sample laptop + sample game history instead of your real PC (use this for the README).
param([string]$Out = "", [switch]$Demo)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$proj = Join-Path $repo 'src\NeuzStrap'
$dev = Join-Path $repo '.dev\screenshot-runner'
if (-not $Out) { $Out = Join-Path $repo '.dev\shots' }

$build = dotnet build $proj -c Debug -nologo -v q 2>&1 | Select-String -Pattern ' error | warning ' | ForEach-Object { $_.Line } | Select-Object -Unique
if ($build) { $build; if ($build -match ' error ') { exit 1 } }

Remove-Item -Recurse -Force (Join-Path $dev 'Logs') -ErrorAction SilentlyContinue
Get-ChildItem $Out -Filter *.png -ErrorAction SilentlyContinue | Remove-Item -Force
New-Item -ItemType Directory -Force $dev | Out-Null
Copy-Item (Join-Path $proj 'bin\Debug\net48\NeuzStrap.exe') $dev -Force
Set-Content (Join-Path $dev 'portable.txt') 'portable mode for screenshots'
# start from default settings so the shots are reproducible
Remove-Item (Join-Path $dev 'Settings.json'), (Join-Path $dev 'State.json') -Force -ErrorAction SilentlyContinue

$argList = @('-screenshot', "`"$Out`"")
if ($Demo) { $argList += '-demo' }
$p = Start-Process -FilePath (Join-Path $dev 'NeuzStrap.exe') -ArgumentList $argList -PassThru -Wait
"exit code: $($p.ExitCode)"
Get-ChildItem (Join-Path $dev 'Logs') | ForEach-Object {
    Get-Content $_.FullName | Select-String -Pattern 'ERROR|WARN|   at ' | Select-Object -First 30 | ForEach-Object { $_.Line }
}
Get-ChildItem $Out -ErrorAction SilentlyContinue | ForEach-Object { "$($_.Name)  $($_.Length)" }
