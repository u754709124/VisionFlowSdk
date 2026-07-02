using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 算法配方适配器接口，供项目专属节点调用上位机或测试桩中的算法实现。
    /// </summary>
    public interface IRecipeAdapter
    {
        string RecipeId { get; }

        Task<RecipeRunResult> RunAsync(RecipeRunRequest request, CancellationToken cancellationToken);
    }
}
