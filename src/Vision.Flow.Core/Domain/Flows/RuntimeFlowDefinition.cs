using System.Collections.Generic;
using Vision.Flow.Core.Runtime.Engine;

namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// ��������̬���̶��壬WinForms ��λ������ `.flowruntime` ���� `FlowRunner` ִ�С�
    /// </summary>
    public sealed class RuntimeFlowDefinition
    {
        public RuntimeFlowDefinition()
        {
            SchemaVersion = 1;
            Nodes = new List<NodeDefinition>();
            Edges = new List<EdgeDefinition>();
            Entries = new List<FlowEntryDefinition>();
            Settings = new Dictionary<string, object>();
        }

        public string FlowId { get; set; }

        public string FlowName { get; set; }

        public int SchemaVersion { get; set; }

        public string Version { get; set; }

        /// <summary>
        /// ����̬�ڵ㼯�ϣ�ֻ����ִ��������Ϣ��
        /// </summary>
        public List<NodeDefinition> Nodes { get; set; }

        /// <summary>
        /// ���������߼��ϣ�������˿����������ڵ���ȡ�
        /// </summary>
        public List<EdgeDefinition> Edges { get; set; }

        /// <summary>
        /// �ⲿ�¼��ɴ�����������ڡ�
        /// </summary>
        public List<FlowEntryDefinition> Entries { get; set; }

        public Dictionary<string, object> Settings { get; set; }
    }
}
