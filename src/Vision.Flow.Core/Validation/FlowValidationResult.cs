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
    /// 流程校验结果，聚合错误和警告并提供便捷查询。
    /// </summary>
    public sealed class FlowValidationResult
    {
        public FlowValidationResult()
        {
            Issues = new List<FlowValidationIssue>();
        }

        public List<FlowValidationIssue> Issues { get; private set; }

        public bool IsValid
        {
            get { return !Issues.Any(x => x.Severity == FlowValidationSeverity.Error); }
        }

        public IEnumerable<FlowValidationIssue> Errors
        {
            get { return Issues.Where(x => x.Severity == FlowValidationSeverity.Error); }
        }

        public IEnumerable<FlowValidationIssue> Warnings
        {
            get { return Issues.Where(x => x.Severity == FlowValidationSeverity.Warning); }
        }

        public void AddError(
            string code,
            string message,
            string nodeId = null,
            int? edgeIndex = null,
            string entryName = null,
            string field = null)
        {
            AddIssue(FlowValidationSeverity.Error, code, message, nodeId, edgeIndex, entryName, field);
        }

        public void AddWarning(
            string code,
            string message,
            string nodeId = null,
            int? edgeIndex = null,
            string entryName = null,
            string field = null)
        {
            AddIssue(FlowValidationSeverity.Warning, code, message, nodeId, edgeIndex, entryName, field);
        }

        public void AddIssue(
            FlowValidationSeverity severity,
            string code,
            string message,
            string nodeId = null,
            int? edgeIndex = null,
            string entryName = null,
            string field = null)
        {
            Issues.Add(new FlowValidationIssue
            {
                Severity = severity,
                Code = code,
                Message = message,
                NodeId = nodeId,
                EdgeIndex = edgeIndex,
                EntryName = entryName,
                Field = field
            });
        }
    }
}
