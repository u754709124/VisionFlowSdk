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

namespace Vision.Flow.Core.Definitions
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
