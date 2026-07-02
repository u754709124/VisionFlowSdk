namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// 输出端口扇出执行模式，控制一个输出端口连接多条边时的调度方式。
    /// </summary>
    public enum FlowFanOutMode
    {
        Sequential = 0,
        Parallel = 1
    }
}
