using System.Collections.Generic;

namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// 设计器画布视图状态，不允许进入生产运行态文件。
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
        /// 设计器画布宽度，仅用于 `.flowdesign` 视图状态，发布运行态时必须移除。
        /// </summary>
        public double CanvasWidth { get; set; }

        /// <summary>
        /// 设计器画布高度，仅用于 `.flowdesign` 视图状态，发布运行态时必须移除。
        /// </summary>
        public double CanvasHeight { get; set; }

        public Dictionary<string, NodeViewState> Nodes { get; set; }
    }
}
