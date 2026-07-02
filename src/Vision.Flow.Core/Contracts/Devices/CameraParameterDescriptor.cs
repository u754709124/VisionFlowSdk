namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 相机参数描述，用于设计器或上位机呈现可配置参数。
    /// </summary>
    public sealed class CameraParameterDescriptor
    {
        public string ParameterName { get; set; }

        public string DisplayName { get; set; }

        public string ValueType { get; set; }

        public string Unit { get; set; }

        public bool IsWritable { get; set; }

        public object Minimum { get; set; }

        public object Maximum { get; set; }

        public object DefaultValue { get; set; }
    }
}
