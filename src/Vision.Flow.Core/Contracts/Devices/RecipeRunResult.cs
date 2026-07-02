using System.Collections.Generic;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 配方运行结果，Outputs 字典会被节点转写为下游可绑定变量。
    /// </summary>
    public sealed class RecipeRunResult
    {
        public RecipeRunResult()
        {
            Outputs = new Dictionary<string, object>();
        }

        public bool IsSuccess { get; set; }

        public string Status { get; set; }

        public string Message { get; set; }

        public IDictionary<string, object> Outputs { get; set; }
    }
}
