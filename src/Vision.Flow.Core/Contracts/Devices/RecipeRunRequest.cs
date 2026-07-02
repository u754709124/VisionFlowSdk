using System.Collections.Generic;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// �䷽�������󣬽ڵ�� Token��ͼ���ҵ����������󽻸��㷨��������
    /// </summary>
    public sealed class RecipeRunRequest
    {
        public RecipeRunRequest()
        {
            Inputs = new Dictionary<string, object>();
        }

        public string RecipeId { get; set; }

        public FlowToken Token { get; set; }

        public IDictionary<string, object> Inputs { get; set; }
    }
}
