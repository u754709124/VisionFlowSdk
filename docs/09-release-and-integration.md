# 09 - Release and Integration

## SDK Package

Run the package script from the repository root:

```powershell
./build/pack-sdk.ps1
```

The script builds the solution and writes the SDK package to:

```text
artifacts/sdk
artifacts/samples/flows
```

Production WinForms hosts normally reference:

```text
Vision.Flow.Core.dll
Vision.Flow.Nodes.dll
Vision.DeviceAdapters.dll
```

Reference `Vision.Flow.Designer.Wpf.dll` only from editor or debug tooling that hosts the WPF designer. Production runtime should load `.flowruntime` and run through `FlowRunner` without creating designer UI.

## Runtime Wiring

Register adapters implemented by the upper-machine application, then register common node factories:

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

Load the published runtime:

```csharp
var flow = RuntimeFlowSerializer.Load("Station01.flowruntime");
```

Subscribe to runtime events through `IFlowEventSink`:

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

Start the runner once during station initialization, then trigger flow entries from station events:

```csharp
await runner.StartAsync(CancellationToken.None);

var token = new FlowToken { TokenId = Guid.NewGuid().ToString("N") };
token.Set("PartId", partId);

await runner.TriggerAsync("ManualStart", token, CancellationToken.None);
```

## Flow Files

Use `.flowdesign` only for designer editing and debug publishing. Deploy `.flowruntime` to production stations. A runtime file must not contain canvas coordinates, zoom, WPF styles, or designer-only state.

The package script copies sample flows into `artifacts/samples/flows`, including:

```text
single-shot.flowdesign
single-shot.flowruntime
two-position-stitch.flowdesign
continuous-scan.flowdesign
```
## 2026-06 Integration Notes

Production hosts that use camera callback nodes should create or reuse a camera frame router and pass it to the runner options used by the application. The default router subscribes to registered camera adapters and buffers lightweight frame metadata for `camera.image_callback`.

Queue-enabled nodes (`recipe.run`, `image.save`, and `database.save`) can use named bounded queues. Configure queue names and capacity in the flow file, then provide a shared queue registry so repeated node executions reuse the same queue.

When image data crosses async or queued boundaries, use `IVisionImage.CloneReference()` or an adapter-owned image reference to keep native handles alive until the downstream save or algorithm work completes. Dispose image references when the host owns their lifetime.

The WinForms demo accepts an optional `.flowruntime` path as the first command-line argument. This is only a demo convenience; production stations should load runtime files from their own recipe/configuration system.
