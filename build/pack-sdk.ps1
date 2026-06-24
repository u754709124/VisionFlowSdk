param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $root "artifacts"
$sdkArtifacts = Join-Path $artifactsRoot "sdk"
$sampleFlowArtifacts = Join-Path $artifactsRoot "samples\flows"
$script:RootFullPath = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/') + '\'

function Assert-WorkspacePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($script:RootFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify path outside workspace: $fullPath"
    }

    return $fullPath
}

& (Join-Path $PSScriptRoot "build.ps1") -Configuration $Configuration

foreach ($target in @($sdkArtifacts, $sampleFlowArtifacts)) {
    $safeTarget = Assert-WorkspacePath $target
    if (Test-Path -LiteralPath $safeTarget) {
        Remove-Item -LiteralPath $safeTarget -Recurse -Force
    }

    New-Item -ItemType Directory -Path $safeTarget -Force | Out-Null
}

$assemblies = @(
    "src\Vision.Flow.Core\bin\$Configuration\Vision.Flow.Core.dll",
    "src\Vision.Flow.Nodes\bin\$Configuration\Vision.Flow.Nodes.dll",
    "src\Vision.DeviceAdapters\bin\$Configuration\Vision.DeviceAdapters.dll",
    "src\Vision.Flow.Designer.Wpf\bin\$Configuration\Vision.Flow.Designer.Wpf.dll"
)

foreach ($assembly in $assemblies) {
    $dllPath = Join-Path $root $assembly
    if (Test-Path -LiteralPath $dllPath) {
        Copy-Item -LiteralPath $dllPath -Destination $sdkArtifacts -Force

        $xmlPath = [System.IO.Path]::ChangeExtension($dllPath, ".xml")
        if (Test-Path -LiteralPath $xmlPath) {
            Copy-Item -LiteralPath $xmlPath -Destination $sdkArtifacts -Force
        }
    } else {
        Write-Host "Missing: $assembly" -ForegroundColor Yellow
    }
}

$samplesSource = Join-Path $root "samples\flows"
if (Test-Path -LiteralPath $samplesSource) {
    Get-ChildItem -Path $samplesSource -Force | Copy-Item -Destination $sampleFlowArtifacts -Recurse -Force
}

@'
# VisionFlowSdk Integration

## Package Layout

Production hosts reference these runtime DLLs from `artifacts/sdk`:

- Vision.Flow.Core.dll
- Vision.Flow.Nodes.dll
- Vision.DeviceAdapters.dll

`Vision.Flow.Designer.Wpf.dll` is optional. Reference it only from editor/debug tools that host the WPF designer. Production WinForms stations should load `.flowruntime` files and run through `FlowRunner` without creating designer controls.

Sample design/runtime flow files are copied to `artifacts/samples/flows`.

## Production Runtime Wiring

Register adapters implemented by the upper-machine application, then register common node factories. The `Vision.DeviceAdapters` project provides `DefaultDeviceRegistry` and fake adapters for demos; production hosts normally register their own adapter implementations through the same Core interfaces.

```csharp
var devices = new DefaultDeviceRegistry();
devices.RegisterCamera("Camera01", new UpperMachineCameraAdapter(existingCamera));
devices.RegisterLight("Light01", new UpperMachineLightAdapter(existingLight));
devices.RegisterMotion("Motion01", new UpperMachineMotionAdapter(existingMotion));
devices.RegisterRecipe("Recipe01", new UpperMachineRecipeAdapter(existingRecipeSystem));
devices.RegisterImageSaver("ImageSave01", new UpperMachineImageSaveAdapter(existingImageStorage));
devices.RegisterDatabase("VisionDb", new UpperMachineDatabaseAdapter(existingDatabase));

var nodes = new NodeRegistry();
CommonNodeRegistration.RegisterAll(nodes);
```

Load the published runtime file:

```csharp
var flow = RuntimeFlowSerializer.Load("Station01.flowruntime");
```

Subscribe to runtime events with an `IFlowEventSink` before starting so the host can log node status, errors, and outputs:

```csharp
public sealed class StationEventSink : IFlowEventSink
{
    public Task PublishAsync(FlowRuntimeEvent runtimeEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Forward to station logs, alarms, UI status, or trace storage.
        return Task.FromResult(0);
    }
}

var eventSink = new StationEventSink();
var runner = new FlowRunner(flow, nodes, eventSink, devices);
```

Start the runner during station initialization, then trigger entries from motion, camera, IO, or recipe events:

```csharp
await runner.StartAsync(CancellationToken.None);

var token = new FlowToken { TokenId = Guid.NewGuid().ToString("N") };
token.Set("PartId", partId);

await runner.TriggerAsync("ManualStart", token, CancellationToken.None);
```

The WPF designer may compile a `.flowdesign` to `.flowruntime` for debugging or publishing, but the production process should deploy and load only `.flowruntime`.
'@ | Set-Content -Path (Join-Path $sdkArtifacts "README-INTEGRATION.md") -Encoding UTF8

Write-Host "SDK packaged to $sdkArtifacts" -ForegroundColor Green
Write-Host "Sample flows packaged to $sampleFlowArtifacts" -ForegroundColor Green
