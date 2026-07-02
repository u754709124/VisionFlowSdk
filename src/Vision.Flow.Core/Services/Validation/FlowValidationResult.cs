using System.Collections.Generic;
using System.Linq;

namespace Vision.Flow.Core.Services.Validation
{
    /// <summary>
    /// ����У�������ۺϴ���;��沢�ṩ��ݲ�ѯ��
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
