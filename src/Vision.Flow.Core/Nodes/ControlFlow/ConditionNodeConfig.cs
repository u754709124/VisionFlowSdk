namespace Vision.Flow.Nodes
{
    public sealed class ConditionNodeConfig
    {
        public ConditionNodeConfig()
        {
            Operator = "Equal";
        }

        public string LeftBinding { get; set; }

        public string Operator { get; set; }

        public object RightValue { get; set; }

        public string RightBinding { get; set; }
    }
}
