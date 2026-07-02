using System.Collections.Generic;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// �䷽���н����Outputs �ֵ�ᱻ�ڵ�תдΪ���οɰ󶨱�����
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
