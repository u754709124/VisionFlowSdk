using System.Collections.Generic;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 配方运行请求，节点把 Token、图像和业务输入整理后交给算法适配器。
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
