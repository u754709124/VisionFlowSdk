namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>指定“设置变量”节点写入当前 FlowRun 还是 Runner 共享的全局存储。</summary>
    public enum FlowVariableTargetScope
    {
        /// <summary>写入当前 FlowRun 的局部变量池。</summary>
        FlowRun = 0,

        /// <summary>写入当前 Runner 的 Session 级全局变量存储。</summary>
        GlobalVariable = 1
    }
}
