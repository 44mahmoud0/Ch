Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

New-Item -ItemType Directory -Force artifacts | Out-Null

$stageResults = [System.Collections.Generic.List[object]]::new()
$currentStage = "Preflight"
$failedStage = $null
$failureMessage = $null
$gateStatus = "failed"
$commit = $env:GITHUB_SHA
if ([string]::IsNullOrWhiteSpace($commit)) {
    $commit = (& git rev-parse HEAD 2>$null).Trim()
}

function Write-StageResult {
    param(
        [Parameter(Mandatory = $true)][string]$Stage,
        [Parameter(Mandatory = $true)][int]$ExitCode
    )

    $entry = [PSCustomObject]@{
        Stage = $Stage
        ExitCode = $ExitCode
        Status = if ($ExitCode -eq 0) { "passed" } else { "failed" }
        Timestamp = (Get-Date).ToString("o")
    }

    $stageResults.Add($entry)
    $entry | ConvertTo-Json -Depth 4 | Out-File -Encoding utf8 "artifacts\$Stage.json"
}

try {
    if (-not $IsWindows) {
        throw "Full Mahmoud AI verification requires Windows."
    }

    $currentStage = "Sdk"
    Write-Host "=== SDK ==="
    dotnet --info
    $sdkInfoCode = $LASTEXITCODE
    Write-StageResult "Sdk" $sdkInfoCode
    if ($sdkInfoCode -ne 0) {
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

    $currentStage = "Restore"
    Write-Host "=== RESTORE + SECURITY AUDIT ==="
    & $msbuild MahmoudAI.sln /t:Restore /p:Configuration=Release
    $restoreCode = $LASTEXITCODE
    Write-StageResult "Restore" $restoreCode
    if ($restoreCode -ne 0) {
        throw "Restore failed."
    }

    $currentStage = "Build"
    Write-Host "=== FULL WINDOWS BUILD ==="
    & $msbuild MahmoudAI.sln /m /t:Build /p:Configuration=Release /bl:artifacts\build.binlog
    $buildCode = $LASTEXITCODE
    Write-StageResult "Build" $buildCode
    if ($buildCode -ne 0) {
        throw "Windows build failed."
    }

    $currentStage = "Tests"
    Write-Host "=== TESTS ==="
    dotnet test MahmoudAI.sln --configuration Release --no-build --logger "trx;LogFileName=tests.trx"
    $testsCode = $LASTEXITCODE
    Write-StageResult "Tests" $testsCode
    if ($testsCode -ne 0) {
        throw "Tests failed."
    }

    $currentStage = "Format"
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
        "tests\MahmoudAI.Core.Tests\MahmoudAI.Core.Tests.csproj",
        "tests\MahmoudAI.WindowsIntegration.Tests\MahmoudAI.WindowsIntegration.Tests.csproj"
    )

    foreach ($project in $formatProjects) {
        Write-Host "Formatting check: $project"
        & dotnet format $project whitespace --verify-no-changes --no-restore --verbosity minimal
        $formatCode = $LASTEXITCODE
        $formatStage = "Format-$([System.IO.Path]::GetFileNameWithoutExtension($project))"
        Write-StageResult $formatStage $formatCode
        if ($formatCode -ne 0) {
            throw "Code formatting/analyzer gate failed for $project."
        }
    }

    $gateStatus = "passed"
    Write-Host "================================"
    Write-Host "MAHMOUD AI QUALITY GATE: PASSED"
    Write-Host "================================"
}
catch {
    $failedStage = $currentStage
    $failureMessage = $_.Exception.Message
    Write-Error "Quality Gate failed at stage '$failedStage': $failureMessage"
    throw
}
finally {
    $summary = [PSCustomObject]@{
        Commit = $commit
        Status = $gateStatus
        FailedStage = $failedStage
        Failure = $failureMessage
        Stages = @($stageResults)
        Timestamp = (Get-Date).ToString("o")
    }

    $summary | ConvertTo-Json -Depth 6 | Out-File -Encoding utf8 "artifacts\GateSummary.json"
}
