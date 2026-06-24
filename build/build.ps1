param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$sln = Join-Path $root "VisionFlowSdk.sln"

if (!(Test-Path $sln)) {
    Write-Host "VisionFlowSdk.sln not found. Create the solution first." -ForegroundColor Yellow
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

Write-Host "Building $sln ($Configuration)..." -ForegroundColor Cyan
& $msbuild $sln /p:Configuration=$Configuration /m
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Build completed." -ForegroundColor Green
