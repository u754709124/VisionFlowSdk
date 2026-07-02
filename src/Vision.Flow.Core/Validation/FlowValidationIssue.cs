using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Vision.Flow.Core.Constants;
using Vision.Flow.Core.Definitions;
using Vision.Flow.Core.Descriptors;
using Vision.Flow.Core.Devices;
using Vision.Flow.Core.Publishing;
using Vision.Flow.Core.Registry;
using Vision.Flow.Core.Runtime;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Serialization;
using Vision.Flow.Core.Validation;

namespace Vision.Flow.Core.Validation
{
    /// <summary>
    /// 单条流程校验问题，包含稳定错误码和定位字段。
    /// </summary>
    public sealed class FlowValidationIssue
    {
        public FlowValidationSeverity Severity { get; set; }

        /// <summary>
        /// 稳定错误码，外部工具应优先依赖错误码而不是消息文本。
        /// </summary>
        public string Code { get; set; }

        public string Message { get; set; }

        public string NodeId { get; set; }

        public int? EdgeIndex { get; set; }

        public string EntryName { get; set; }

        /// <summary>
        /// 问题字段路径，通常对应运行态定义中的节点、连线、入口或设置位置。
        /// </summary>
        public string Field { get; set; }
    }
}
