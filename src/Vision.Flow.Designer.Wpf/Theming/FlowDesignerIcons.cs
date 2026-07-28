using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Vision.Flow.Designer.Wpf.Theming
{
    /// <summary>
    /// 生成设计器使用的矢量图标，避免字体图标在不同宿主中发生字形漂移。
    /// </summary>
    public static class FlowDesignerIcons
    {
        public static Path Create(string name, Brush stroke, double size)
        {
            var geometry = Geometry.Parse(GetGeometry(name));
            var path = new Path
            {
                Data = geometry,
                Stroke = stroke,
                StrokeThickness = 1.65,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = name == "stop" ? stroke : Brushes.Transparent,
                Stretch = Stretch.Uniform,
                Width = size,
                Height = size,
                IsHitTestVisible = false
            };

            return path;
        }

        public static FrameworkElement CreateNode(string nodeType, Brush stroke, double size)
        {
            var type = nodeType ?? string.Empty;
            var icon = type.IndexOf("condition", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.IndexOf("branch", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "condition"
                    : type.IndexOf("delay", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "clock"
                        : type.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0
                            ? "camera"
                            : type.IndexOf("variable", StringComparison.OrdinalIgnoreCase) >= 0
                                ? "variable"
                                : "node";
            return Create(icon, stroke, size);
        }

        private static string GetGeometry(string name)
        {
            switch ((name ?? string.Empty).ToLowerInvariant())
            {
                case "search":
                    return "M3,3 A5,5 0 1 0 13,13 A5,5 0 1 0 3,3 M11.5,11.5 L17,17";
                case "edit":
                    return "M3,14 L3,17 L6,17 L16,7 L13,4 Z M11.8,5.2 L14.8,8.2";
                case "debug":
                    return "M6,6 L15,11 L6,16 Z M2,3 L2,19 M18,5 L18,17";
                case "new":
                    return "M10,3 L10,17 M3,10 L17,10";
                case "sample":
                    return "M3,4 L8,4 L10,6 L17,6 L17,17 L3,17 Z";
                case "open":
                    return "M2,7 L7,7 L9,5 L18,5 L15,16 L3,16 Z";
                case "save":
                    return "M3,3 L15,3 L18,6 L18,17 L3,17 Z M6,3 L6,8 L14,8 L14,3 M6,13 L15,13";
                case "publish":
                    return "M10,15 L10,3 M6,7 L10,3 L14,7 M3,12 L3,17 L17,17 L17,12";
                case "run":
                    return "M5,3 L17,10 L5,17 Z";
                case "stop":
                    return "M5,5 L15,5 L15,15 L5,15 Z";
                case "chevron":
                    return "M4,7 L10,13 L16,7";
                case "clock":
                    return "M10,2 A8,8 0 1 0 10,18 A8,8 0 1 0 10,2 M10,5 L10,10 L14,12";
                case "camera":
                    return "M3,6 L7,6 L8,4 L13,4 L14,6 L17,6 L17,16 L3,16 Z M10,8 A3,3 0 1 0 10,14 A3,3 0 1 0 10,8";
                case "condition":
                    return "M10,2 L18,10 L10,18 L2,10 Z M7,10 L9,12 L13,8";
                case "variable":
                    return "M4,4 L8,4 L10,9 L12,4 L16,4 M5,16 L9,16 L11,11 L13,16 L17,16";
                default:
                    return "M3,4 L8,2 L13,4 L18,2 L18,15 L13,18 L8,15 L3,18 Z M8,2 L8,15 M13,4 L13,18";
            }
        }
    }
}
