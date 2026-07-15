using System;

namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// 流程文件结构版本入口；当前开发版本只接受 v2。
    /// </summary>
    public static class FlowSchema
    {
        public const int CurrentVersion = 2;

        public static void EnsureSupported(int schemaVersion)
        {
            if (schemaVersion != CurrentVersion)
            {
                throw new UnsupportedFlowSchemaVersionException(schemaVersion);
            }
        }
    }

    /// <summary>
    /// 读取、保存或发布不受支持的流程结构版本时抛出的异常。
    /// </summary>
    public sealed class UnsupportedFlowSchemaVersionException : InvalidOperationException
    {
        public UnsupportedFlowSchemaVersionException(int actualVersion)
            : base("Unsupported flow schema version " + actualVersion + ". Expected version " + FlowSchema.CurrentVersion + ".")
        {
            ActualVersion = actualVersion;
            ExpectedVersion = FlowSchema.CurrentVersion;
        }

        public int ActualVersion { get; private set; }

        public int ExpectedVersion { get; private set; }
    }
}
