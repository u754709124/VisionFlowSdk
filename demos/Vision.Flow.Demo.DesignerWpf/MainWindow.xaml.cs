using System.Windows;
using Vision.DeviceAdapters;

namespace Vision.Flow.Demo.DesignerWpf
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Designer.DebugDevices = CreateFakeDevices();
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
