using System.Collections.Generic;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// ����̬�ڵ㶨�壬����ڵ����͡����úͱ����󶨡�
    /// </summary>
    public sealed class NodeDefinition
    {
        public NodeDefinition()
        {
            Settings = new Dictionary<string, object>();
            InputBindings = new Dictionary<string, VariableBinding>();
        }

        /// <summary>
        /// �ڵ�ʵ�� ID���Ǳ��������������õ��ȶ�����
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// �ڵ����ͣ�����ʹ�� `FlowNodeTypes` �е���ע��Э��ֵ��
        /// </summary>
        public string Type { get; set; }

        public string Name { get; set; }

        public string Version { get; set; }

        /// <summary>
        /// �ڵ������ֵ䣬��Ӧ����ʹ�� `FlowSettingNames` ������
        /// </summary>
        public Dictionary<string, object> Settings { get; set; }

        /// <summary>
        /// ����˿ڵ��������ʽ�İ󶨣�������Ҫͨ���ýṹ��ڵ㴫�ݡ�
        /// </summary>
        public Dictionary<string, VariableBinding> InputBindings { get; set; }
    }
}
