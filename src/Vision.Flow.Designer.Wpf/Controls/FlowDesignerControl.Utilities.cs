using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using Vision.Flow.Nodes;
using ShapesPath = System.Windows.Shapes.Path;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Services.Publishing;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Designer.Wpf.Controls
{
    // 宸ュ叿杈呭姪鏂规硶缁熶竴澶勭悊绀轰緥璺緞銆佺粦瀹氭枃鏈€佽繛绾挎爣绛惧拰鍙鏍戞煡鎵俱€?
    public sealed partial class FlowDesignerControl
    {
        private static string GetSampleFlowDirectory()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (var i = 0; i < 8 && directory != null; i++)
            {
                var sampleDirectory = System.IO.Path.Combine(directory.FullName, "samples", "flows");
                if (Directory.Exists(sampleDirectory))
                {
                    return sampleDirectory;
                }

                directory = directory.Parent;
            }

            return Environment.CurrentDirectory;
        }

        private static bool StringEquals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        internal static string CreatePortAnchorKey(string nodeId, FlowPortDirection direction, string portName)
        {
            return (nodeId ?? string.Empty) + "|" + FlowEnumConverter.ToWireValue(direction) + "|" + (portName ?? string.Empty);
        }

        internal static bool EdgeEquals(EdgeDefinition left, EdgeDefinition right)
        {
            return left != null &&
                right != null &&
                StringEquals(left.FromNodeId, right.FromNodeId) &&
                StringEquals(left.FromPort, right.FromPort) &&
                StringEquals(left.ToNodeId, right.ToNodeId) &&
                StringEquals(left.ToPort, right.ToPort);
        }

        internal static string FormatEdgeLabel(EdgeDefinition edge)
        {
            if (edge == null)
            {
                return "none";
            }

            return (edge.FromNodeId ?? string.Empty) + "." + (string.IsNullOrWhiteSpace(edge.FromPort) ? "?" : edge.FromPort) +
                " -> " +
                (edge.ToNodeId ?? string.Empty) + "." + (string.IsNullOrWhiteSpace(edge.ToPort) ? "?" : edge.ToPort);
        }

        private static bool IsTextEditorFocused()
        {
            var current = Keyboard.FocusedElement as DependencyObject;
            while (current != null)
            {
                if (current is TextBox || current is PasswordBox || current is ComboBox)
                {
                    return true;
                }

                DependencyObject parent = null;
                try
                {
                    parent = VisualTreeHelper.GetParent(current);
                }
                catch (InvalidOperationException)
                {
                    parent = null;
                }

                if (parent == null)
                {
                    parent = LogicalTreeHelper.GetParent(current);
                }

                current = parent;
            }

            return false;
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                var typed = current as T;
                if (typed != null)
                {
                    return typed;
                }

                DependencyObject parent = null;
                try
                {
                    parent = VisualTreeHelper.GetParent(current);
                }
                catch (InvalidOperationException)
                {
                    parent = null;
                }

                if (parent == null)
                {
                    parent = LogicalTreeHelper.GetParent(current);
                }

                current = parent;
            }

            return null;
        }
    }
}
