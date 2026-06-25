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
using Vision.Flow.Core;
using Vision.Flow.Nodes;
using ShapesPath = System.Windows.Shapes.Path;

namespace Vision.Flow.Designer.Wpf
{
    // Utility helpers normalize sample paths, binding text, edge labels, and visual tree lookups.
    public sealed partial class FlowDesignerControl
    {
        private static List<Dictionary<string, object>> CreateLightChannels(string channelName, double intensity)
        {
            return new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    { "ChannelName", channelName },
                    { "IsEnabled", true },
                    { "Intensity", intensity },
                    { "DurationMs", 0 }
                }
            };
        }

        private static List<Dictionary<string, object>> CreateCameraParameters(string text)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var item in SplitPairs(text))
            {
                result.Add(new Dictionary<string, object>
                {
                    { "Name", item.Key },
                    { "Value", item.Value }
                });
            }

            return result;
        }

        private static List<Dictionary<string, object>> CreateFieldMappings(string text)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var item in SplitPairs(text))
            {
                var mapping = new Dictionary<string, object>
                {
                    { "FieldName", item.Key }
                };

                if (item.Value != null && item.Value.Trim().StartsWith("{{", StringComparison.Ordinal))
                {
                    mapping["ValueBinding"] = item.Value;
                }
                else
                {
                    mapping["Value"] = item.Value;
                }

                result.Add(mapping);
            }

            return result;
        }

        private static IList<KeyValuePair<string, string>> SplitPairs(string text)
        {
            var result = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return result;
            }

            var parts = text.Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var index = part.IndexOf('=');
                if (index <= 0)
                {
                    continue;
                }

                var key = part.Substring(0, index).Trim();
                var value = part.Substring(index + 1).Trim();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    result.Add(new KeyValuePair<string, string>(key, value));
                }
            }

            return result;
        }

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

        internal static string CreatePortAnchorKey(string nodeId, string direction, string portName)
        {
            return (nodeId ?? string.Empty) + "|" + (direction ?? string.Empty) + "|" + (portName ?? string.Empty);
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
