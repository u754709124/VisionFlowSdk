using System.Collections.Generic;

namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// �����������ͼ״̬�������������������̬�ļ���
    /// </summary>
    public sealed class FlowViewState
    {
        public const double DefaultCanvasWidth = 1800;
        public const double DefaultCanvasHeight = 1100;

        public FlowViewState()
        {
            Zoom = 1.0;
            CanvasWidth = DefaultCanvasWidth;
            CanvasHeight = DefaultCanvasHeight;
            Nodes = new Dictionary<string, NodeViewState>();
        }

        public double Zoom { get; set; }

        public double OffsetX { get; set; }

        public double OffsetY { get; set; }

        /// <summary>
        /// �����������ȣ������� `.flowdesign` ��ͼ״̬����������̬ʱ�����Ƴ���
        /// </summary>
        public double CanvasWidth { get; set; }

        /// <summary>
        /// ����������߶ȣ������� `.flowdesign` ��ͼ״̬����������̬ʱ�����Ƴ���
        /// </summary>
        public double CanvasHeight { get; set; }

        public Dictionary<string, NodeViewState> Nodes { get; set; }
    }
}
