param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts\sdk"

& (Join-Path $PSScriptRoot "build.ps1") -Configuration $Configuration

if (Test-Path $artifacts) {
    Remove-Item $artifacts -Recurse -Force
}
New-Item -ItemType Directory -Path $artifacts | Out-Null

$dlls = @(
    "src\Vision.Flow.Core\bin\$Configuration\Vision.Flow.Core.dll",
    "src\Vision.Flow.Nodes\bin\$Configuration\Vision.Flow.Nodes.dll",
    "src\Vision.DeviceAdapters\bin\$Configuration\Vision.DeviceAdapters.dll",
    "src\Vision.Flow.Designer.Wpf\bin\$Configuration\Vision.Flow.Designer.Wpf.dll"
)

foreach ($dll in $dlls) {
    $path = Join-Path $root $dll
    if (Test-Path $path) {
        Copy-Item $path $artifacts
    } else {
        Write-Host "Missing: $dll" -ForegroundColor Yellow
    }
}

$samplesSource = Join-Path $root "samples\flows"
$samplesTarget = Join-Path $artifacts "samples\flows"
if (Test-Path $samplesSource) {
    New-Item -ItemType Directory -Path $samplesTarget -Force | Out-Null
    Copy-Item (Join-Path $samplesSource "*") $samplesTarget -Recurse -Force
}

@"
# VisionFlowSdk Integration

Production runtime normally references:

- Vision.Flow.Core.dll
- Vision.Flow.Nodes.dll
- Vision.DeviceAdapters.dll

Only designer/editor hosts reference:

- Vision.Flow.Designer.Wpf.dll

Basic production steps:

1. Register real adapters.
2. Register CommonNodeRegistration.
3. Load .flowruntime.
4. Create FlowRunner.
5. Trigger entries from motion/camera/IO events.
6. Subscribe RuntimeEvent.
"@ | Set-Content -Path (Join-Path $artifacts "README-INTEGRATION.md") -Encoding UTF8

Write-Host "SDK packaged to $artifacts" -ForegroundColor Green
