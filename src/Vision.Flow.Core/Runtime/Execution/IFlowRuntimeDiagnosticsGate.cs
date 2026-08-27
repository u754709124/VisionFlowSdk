namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// 由宿主控制运行期诊断采集开关；Runner 只读取状态，不拥有也不释放实现对象。
    /// </summary>
    public interface IFlowRuntimeDiagnosticsGate
    {
        /// <summary>
        /// 获取是否为后续节点执行尝试捕获实际读取的设置输入。
        /// </summary>
        bool IsNodeInputCaptureEnabled { get; }
    }
}
