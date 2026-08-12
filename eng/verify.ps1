$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

New-Item -ItemType Directory -Force artifacts | Out-Null

if (-not $IsWindows) {
    throw "Full Mahmoud AI verification requires Windows."
}

Write-Host "=== SDK ==="
dotnet --info
if ($LASTEXITCODE -ne 0) {
    throw ".NET SDK verification failed."
}

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (!(Test-Path $vswhere)) {
    throw "Visual Studio vswhere was not found."
}

$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (!$msbuild) {
    throw "Visual Studio MSBuild was not found."
}

Write-Host "=== RESTORE + SECURITY AUDIT ==="
& $msbuild MahmoudAI.sln /t:Restore /p:Configuration=Release
if ($LASTEXITCODE -ne 0) {
    throw "Restore failed."
}

Write-Host "=== FULL WINDOWS BUILD ==="
& $msbuild MahmoudAI.sln /m /t:Build /p:Configuration=Release /bl:artifacts\build.binlog
if ($LASTEXITCODE -ne 0) {
    throw "Windows build failed."
}

Write-Host "=== TESTS ==="
dotnet test MahmoudAI.sln --configuration Release --no-build --logger "trx;LogFileName=tests.trx"
if ($LASTEXITCODE -ne 0) {
    throw "Tests failed."
}

Write-Host "=== FORMAT ==="
dotnet format MahmoudAI.sln --verify-no-changes --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Code formatting/analyzer gate failed."
}

Write-Host "================================"
Write-Host "MAHMOUD AI QUALITY GATE: PASSED"
Write-Host "================================"
