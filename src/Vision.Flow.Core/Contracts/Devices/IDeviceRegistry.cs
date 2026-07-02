namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 设备适配器注册表，运行时节点只能通过该接口获取相机、光源、运控、算法和存储适配器。
    /// </summary>
    public interface IDeviceRegistry
    {
        bool TryGetCamera(string cameraId, out ICameraAdapter camera);

        ICameraAdapter GetCamera(string cameraId);

        bool TryGetLight(string lightId, out ILightAdapter light);

        ILightAdapter GetLight(string lightId);

        bool TryGetMotion(string motionId, out IMotionAdapter motion);

        IMotionAdapter GetMotion(string motionId);

        bool TryGetRecipe(string recipeId, out IRecipeAdapter recipe);

        IRecipeAdapter GetRecipe(string recipeId);

        bool TryGetImageSaver(string saverId, out IImageSaveAdapter imageSaver);

        IImageSaveAdapter GetImageSaver(string saverId);

        bool TryGetDatabase(string databaseId, out IDatabaseAdapter database);

        IDatabaseAdapter GetDatabase(string databaseId);
    }
}
