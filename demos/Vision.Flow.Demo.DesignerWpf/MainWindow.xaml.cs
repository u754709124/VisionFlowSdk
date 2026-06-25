using System.Windows;
using Vision.DeviceAdapters;
using Vision.Flow.Core;
using Vision.Flow.Designer.Wpf;
using Vision.Flow.Nodes;

namespace Vision.Flow.Demo.DesignerWpf
{
    public partial class MainWindow : Window
    {
        private readonly FlowDesignerControl _designer;

        public MainWindow()
        {
            InitializeComponent();
            _designer = new FlowDesignerControl(
                CreateNodeRegistry(),
                CreateFakeDevices(),
                new FlowDesignerOptions
                {
                    LoadSampleOnStartup = true
                });
            DesignerHost.Children.Add(_designer);
        }

        private static NodeRegistry CreateNodeRegistry()
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            return registry;
        }

        private static DefaultDeviceRegistry CreateFakeDevices()
        {
            var recipe = new FakeRecipeAdapter("Recipe01");
            recipe.DefaultOutputs["IsOk"] = true;
            recipe.DefaultOutputs["ResultImage"] = new FakeVisionImage("RecipeResult", 640, 480, "Mono8", null);

            return new DefaultDeviceRegistry()
                .RegisterCamera(new FakeCameraAdapter("Camera01"))
                .RegisterLight(new FakeLightAdapter("Light01"))
                .RegisterRecipe(recipe)
                .RegisterImageSaver(new FakeImageSaveAdapter("ImageSave01"))
                .RegisterDatabase(new FakeDatabaseAdapter("VisionDb"));
        }
    }
}
