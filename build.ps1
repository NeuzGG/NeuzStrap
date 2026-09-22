# Builds a release NeuzStrap.exe into ./artifacts (and runs the tests first).
# Usage:  powershell -ExecutionPolicy Bypass -File build.ps1 [-SkipTests]
param([switch]$SkipTests)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Set-Location $root

if (-not $SkipTests) {
    Write-Host "Running tests..." -ForegroundColor Cyan
    dotnet test tests/NeuzStrap.Tests -c Release -nologo
    if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
}

Write-Host "Building release..." -ForegroundColor Cyan
dotnet build src/NeuzStrap -c Release -nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$out = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force $out | Out-Null
$exe = Join-Path $out 'NeuzStrap.exe'
Copy-Item (Join-Path $root 'src/NeuzStrap/bin/Release/net48/NeuzStrap.exe') $exe -Force

$hash = (Get-FileHash $exe -Algorithm SHA256).Hash
Set-Content (Join-Path $out 'NeuzStrap.exe.sha256') "$hash  NeuzStrap.exe"

$size = [math]::Round((Get-Item $exe).Length / 1KB)
Write-Host ""
Write-Host "Done: $exe ($size KB)" -ForegroundColor Green
Write-Host "SHA256: $hash"
