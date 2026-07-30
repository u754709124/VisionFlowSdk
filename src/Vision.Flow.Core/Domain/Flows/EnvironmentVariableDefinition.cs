using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// 流程运行环境变量定义。Id 是稳定协议标识，Name 仅用于用户界面展示。
    /// </summary>
    public sealed class EnvironmentVariableDefinition
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public FlowDataType DataType { get; set; }

        public object DefaultValue { get; set; }
    }
}
