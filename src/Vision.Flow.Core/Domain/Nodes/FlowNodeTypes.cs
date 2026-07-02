namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// 流程节点类型常量。这里的值会进入流程文件和节点注册表，修改会破坏已发布流程兼容性。
    /// </summary>
    public static class FlowNodeTypes
    {
        public const string DelayWait = "delay.wait";
        public const string LogWrite = "log.write";
        public const string VariableSet = "variable.set";
        public const string FlowSplit = "flow.split";
        public const string JoinAnd = "join.and";
        public const string ConditionIf = "condition.if";
    }
}
