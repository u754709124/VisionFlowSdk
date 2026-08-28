using System;

namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// 节点设置的变量选择器无法解析或绑定值无法转换时抛出的稳定异常。
    /// </summary>
    public sealed class SettingBindingException : Exception
    {
        public SettingBindingException(string message)
            : base(message)
        {
        }

        public SettingBindingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// 节点静态配置无效时抛出的稳定异常；此类失败不会进入重试。
    /// </summary>
    public sealed class NodeConfigurationException : Exception
    {
        public NodeConfigurationException(string message)
            : base(message)
        {
        }

        public NodeConfigurationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// 分支失败无法恢复时使用的异常；调度器会先让兄弟分支收敛，再以此异常结束 FlowRun。
    /// </summary>
    public sealed class NodeExecutionFailedException : Exception
    {
        public NodeExecutionFailedException(string message, NodeFailureKind failureKind)
            : base(message)
        {
            FailureKind = failureKind;
        }

        public NodeFailureKind FailureKind { get; private set; }
    }
}
