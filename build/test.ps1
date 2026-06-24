param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $root "tests\Vision.Flow.Tests\Vision.Flow.Tests.csproj"

if (!(Test-Path $testProject)) {
    Write-Host "Test project not found. Create tests/Vision.Flow.Tests first." -ForegroundColor Yellow
    exit 1
}

function Find-MSBuild {
    $cmd = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    $candidates = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\18\Community\Msbuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\18\Community\Msbuild\Current\Bin\amd64\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "MSBuild.exe not found."
}

$msbuild = Find-MSBuild

Write-Host "Building tests..." -ForegroundColor Cyan
& $msbuild $testProject /p:Configuration=$Configuration /m

$testExe = Join-Path $root "tests\Vision.Flow.Tests\bin\$Configuration\Vision.Flow.Tests.exe"
if (!(Test-Path $testExe)) {
    throw "Test executable not found: $testExe"
}

Write-Host "Running tests..." -ForegroundColor Cyan
& $testExe
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Tests completed." -ForegroundColor Green
