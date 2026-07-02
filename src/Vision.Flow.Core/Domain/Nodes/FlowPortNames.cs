namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// 控制流端口名常量。端口名会出现在连线和运行态调度中，必须保持稳定。
    /// </summary>
    public static class FlowPortNames
    {
        public const string In = "In";
        public const string Next = "Next";
        public const string Error = "Error";
        public const string Timeout = "Timeout";
        public const string Waiting = "Waiting";
        public const string True = "True";
        public const string False = "False";
        public const string Completed = "Completed";
        public const string Frame = "Frame";
    }
}
