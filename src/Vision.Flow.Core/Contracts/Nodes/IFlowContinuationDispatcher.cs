using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Core.Contracts.Nodes
{
    /// <summary>
    /// ����ִ�е������ӿڣ�������������ʵ����֧����ʽ��֡�����
    /// </summary>
    public interface IFlowContinuationDispatcher
    {
        Task DispatchAsync(FlowContinuation continuation, CancellationToken cancellationToken);
    }
}
