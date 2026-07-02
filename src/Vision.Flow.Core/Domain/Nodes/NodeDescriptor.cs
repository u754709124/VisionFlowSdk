using System.Collections.Generic;

namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// �ڵ������������������У������¶�ڵ�Ķ˿ڡ����ú����������
    /// </summary>
    public sealed class NodeDescriptor
    {
        public NodeDescriptor()
        {
            InputPorts = new List<NodePortDescriptor>();
            OutputPorts = new List<NodePortDescriptor>();
            Settings = new List<NodeSettingDescriptor>();
            Outputs = new List<NodeOutputDescriptor>();
        }

        /// <summary>
        /// �ڵ�����Э��ֵ��Ӧ��ڵ㹤��ע��� `NodeType` ��ȫһ�¡�
        /// </summary>
        public string NodeType { get; set; }

        /// <summary>
        /// ������ڵ����ʾ���ơ�
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// ������ڵ����ࡣ
        /// </summary>
        public string Category { get; set; }

        public string Version { get; set; }

        public string Description { get; set; }

        /// <summary>
        /// ����˿��б�����������������󶨺�����У�顣
        /// </summary>
        public List<NodePortDescriptor> InputPorts { get; set; }

        /// <summary>
        /// ����˿��б�����ڿ��������Ⱥ���������ߡ�
        /// </summary>
        public List<NodePortDescriptor> OutputPorts { get; set; }

        /// <summary>
        /// �ڵ��������б�����ڶ�̬�������ͷ���ǰУ�顣
        /// </summary>
        public List<NodeSettingDescriptor> Settings { get; set; }

        /// <summary>
        /// �ڵ����к�д������ص�����������塣
        /// </summary>
        public List<NodeOutputDescriptor> Outputs { get; set; }
    }
}
