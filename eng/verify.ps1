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

$actualSdk = dotnet --version
if ($actualSdk -notlike "10.0.4*") {
    throw "Unexpected .NET SDK version: $actualSdk. Expected 10.0.4xx."
}

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (!(Test-Path $vswhere)) {
    throw "Visual Studio vswhere was not found."
}

$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (!$msbuild) {
    throw "Visual Studio MSBuild was not found."
}

function Write-StageResult($stage, $exitCode) {
    $result = [PSCustomObject]@{ Stage = $stage; ExitCode = $exitCode; Timestamp = (Get-Date).ToString("o") }
    $result | ConvertTo-Json | Out-File -Encoding utf8 "artifacts\$stage.json"
    if ($exitCode -ne 0) {
        Write-Error "Stage '$stage' failed with exit code $exitCode."
    }
}

Write-Host "=== RESTORE + SECURITY AUDIT ==="
& $msbuild MahmoudAI.sln /t:Restore /p:Configuration=Release
Write-StageResult "Restore" $LASTEXITCODE
if ($LASTEXITCODE -ne 0) {
    throw "Restore failed."
}

Write-Host "=== FULL WINDOWS BUILD ==="
& $msbuild MahmoudAI.sln /m /t:Build /p:Configuration=Release /bl:artifacts\build.binlog
Write-StageResult "Build" $LASTEXITCODE
if ($LASTEXITCODE -ne 0) {
    throw "Windows build failed."
}

Write-Host "=== TESTS ==="
dotnet test MahmoudAI.sln --configuration Release --no-build --logger "trx;LogFileName=tests.trx"
Write-StageResult "Tests" $LASTEXITCODE
if ($LASTEXITCODE -ne 0) {
    throw "Tests failed."
}

Write-Host "=== FORMAT ==="
# The Windows App SDK XAML compiler is not a Roslyn workspace and can make
# solution-wide dotnet format design-time builds nondeterministic. MSBuild above
# remains the authoritative compile/analyzer gate; whitespace is verified per
# non-WinUI project to keep formatting checks deterministic.
$formatProjects = @(
    "src\MahmoudAI.Core\MahmoudAI.Core.csproj",
    "src\MahmoudAI.Mcp\MahmoudAI.Mcp.csproj",
    "src\MahmoudAI.Security\MahmoudAI.Security.csproj",
    "src\MahmoudAI.Storage\MahmoudAI.Storage.csproj",
    "src\MahmoudAI.WindowsIntegration\MahmoudAI.WindowsIntegration.csproj",
    "tests\MahmoudAI.Core.Tests\MahmoudAI.Core.Tests.csproj"
)

foreach ($project in $formatProjects) {
    Write-Host "Formatting check: $project"
    & dotnet format $project whitespace --verify-no-changes --no-restore --verbosity minimal
    $formatCode = $LASTEXITCODE
    Write-StageResult "Format-$([System.IO.Path]::GetFileNameWithoutExtension($project))" $formatCode
    if ($formatCode -ne 0) {
        throw "Code formatting/analyzer gate failed for $project."
    }
}

Write-Host "================================"
Write-Host "MAHMOUD AI QUALITY GATE: PASSED"
Write-Host "================================"
