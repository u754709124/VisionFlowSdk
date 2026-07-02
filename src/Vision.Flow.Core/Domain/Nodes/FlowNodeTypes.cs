namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// ���̽ڵ����ͳ����������ֵ����������ļ��ͽڵ�ע�����޸Ļ��ƻ��ѷ������̼����ԡ�
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
