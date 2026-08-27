using System.Collections.Generic;

namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// 流程执行选项，由生产运行时或设计器调试运行传入。
    /// </summary>
    public sealed class FlowExecutionOptions
    {
        public FlowExecutionOptions()
        {
            FanOutMode = FlowFanOutMode.Sequential;
            MaxDegreeOfParallelism = 1;
        }

        public FlowFanOutMode FanOutMode { get; set; }

        public int MaxDegreeOfParallelism { get; set; }

        public int DefaultNodeTimeoutMs { get; set; }

        /// <summary>
        /// 获取或设置由宿主持有的动态诊断开关；为 null 或关闭时不创建输入采集对象。
        /// </summary>
        public IFlowRuntimeDiagnosticsGate DiagnosticsGate { get; set; }

        /// <summary>
        /// 按环境变量稳定 Id 提供的运行值；FlowRunner 构造时会制作只读快照。
        /// 未提供的变量使用流程定义中的默认值。
        /// </summary>
        public IDictionary<string, object> EnvironmentVariableValues { get; set; }
    }
}
