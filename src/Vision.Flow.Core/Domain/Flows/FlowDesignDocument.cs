namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// ���̬�����ĵ��������ɷ���������̬������������ͼ״̬��
    /// </summary>
    public sealed class FlowDesignDocument
    {
        public FlowDesignDocument()
        {
            SchemaVersion = 1;
            Runtime = new RuntimeFlowDefinition();
            View = new FlowViewState();
        }

        /// <summary>
        /// ����Ψһ��ʶ����������ļ��������ļ��������¼�֮�佨��������
        /// </summary>
        public string FlowId { get; set; }

        /// <summary>
        /// �����˵��������ƣ�������ִ�е��ȡ�
        /// </summary>
        public string FlowName { get; set; }

        /// <summary>
        /// �ļ��ṹ�汾�����ں�������������
        /// </summary>
        public int SchemaVersion { get; set; }

        /// <summary>
        /// �ɷ�������������������̬���塣
        /// </summary>
        public RuntimeFlowDefinition Runtime { get; set; }

        /// <summary>
        /// ���������ʹ�õĻ���״̬������ `.flowruntime` ʱ�����Ƴ���
        /// </summary>
        public FlowViewState View { get; set; }
    }
}
