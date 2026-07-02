using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// �㷨�䷽�������ӿڣ�����Ŀר��ڵ������λ�������׮�е��㷨ʵ�֡�
    /// </summary>
    public interface IRecipeAdapter
    {
        string RecipeId { get; }

        Task<RecipeRunResult> RunAsync(RecipeRunRequest request, CancellationToken cancellationToken);
    }
}
