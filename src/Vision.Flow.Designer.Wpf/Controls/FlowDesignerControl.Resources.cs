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
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Designer.Wpf.Controls
{
    // 资源辅助方法集中管理共享颜色和 WPF 控件模板。
    public sealed partial class FlowDesignerControl
    {
        private static NodeRegistry CreateDefaultNodeRegistry()
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            return registry;
        }

        private void InitializeResources()
        {
            Resources["FlowPageBackground"] = BrushFromRgb(246, 248, 252);
            Resources["FlowPanelBackground"] = Brushes.White;
            Resources["FlowPanelBorder"] = BrushFromRgb(222, 229, 238);
            Resources["FlowAccent"] = BrushFromRgb(22, 101, 52);
            Resources["FlowText"] = BrushFromRgb(17, 24, 39);
            Resources["FlowMutedText"] = BrushFromRgb(100, 116, 139);
            InstallScrollBarResources();
        }

        private void InstallScrollBarResources()
        {
            var dictionary = (ResourceDictionary)XamlReader.Parse(@"
<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <Style x:Key=""FlowScrollBarPageButton"" TargetType=""{x:Type RepeatButton}"">
        <Setter Property=""Focusable"" Value=""False"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type RepeatButton}"">
                    <Border Background=""Transparent"" />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key=""FlowScrollBarThumb"" TargetType=""{x:Type Thumb}"">
        <Setter Property=""Focusable"" Value=""False"" />
        <Setter Property=""Background"" Value=""#94A3B8"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type Thumb}"">
                    <Border Margin=""2""
                            Background=""{TemplateBinding Background}""
                            CornerRadius=""4"" />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
                <Setter Property=""Background"" Value=""#64748B"" />
            </Trigger>
            <Trigger Property=""IsDragging"" Value=""True"">
                <Setter Property=""Background"" Value=""#166534"" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <ControlTemplate x:Key=""FlowVerticalScrollBarTemplate"" TargetType=""{x:Type ScrollBar}"">
        <Border Width=""10""
                Background=""{TemplateBinding Background}""
                CornerRadius=""5""
                SnapsToDevicePixels=""True"">
            <Track x:Name=""PART_Track"" IsDirectionReversed=""True"">
                <Track.DecreaseRepeatButton>
                    <RepeatButton Command=""ScrollBar.PageUpCommand""
                                  Style=""{StaticResource FlowScrollBarPageButton}"" />
                </Track.DecreaseRepeatButton>
                <Track.Thumb>
                    <Thumb MinHeight=""28"" Style=""{StaticResource FlowScrollBarThumb}"" />
                </Track.Thumb>
                <Track.IncreaseRepeatButton>
                    <RepeatButton Command=""ScrollBar.PageDownCommand""
                                  Style=""{StaticResource FlowScrollBarPageButton}"" />
                </Track.IncreaseRepeatButton>
            </Track>
        </Border>
    </ControlTemplate>

    <ControlTemplate x:Key=""FlowHorizontalScrollBarTemplate"" TargetType=""{x:Type ScrollBar}"">
        <Border Height=""10""
                Background=""{TemplateBinding Background}""
                CornerRadius=""5""
                SnapsToDevicePixels=""True"">
            <Track x:Name=""PART_Track"" IsDirectionReversed=""False"">
                <Track.DecreaseRepeatButton>
                    <RepeatButton Command=""ScrollBar.PageLeftCommand""
                                  Style=""{StaticResource FlowScrollBarPageButton}"" />
                </Track.DecreaseRepeatButton>
                <Track.Thumb>
                    <Thumb MinWidth=""28"" Style=""{StaticResource FlowScrollBarThumb}"" />
                </Track.Thumb>
                <Track.IncreaseRepeatButton>
                    <RepeatButton Command=""ScrollBar.PageRightCommand""
                                  Style=""{StaticResource FlowScrollBarPageButton}"" />
                </Track.IncreaseRepeatButton>
            </Track>
        </Border>
    </ControlTemplate>

    <Style TargetType=""{x:Type ScrollBar}"">
        <Setter Property=""Background"" Value=""#E2E8F0"" />
        <Setter Property=""Width"" Value=""10"" />
        <Setter Property=""MinWidth"" Value=""10"" />
        <Setter Property=""Template"" Value=""{StaticResource FlowVerticalScrollBarTemplate}"" />
        <Style.Triggers>
            <Trigger Property=""Orientation"" Value=""Horizontal"">
                <Setter Property=""Width"" Value=""Auto"" />
                <Setter Property=""MinWidth"" Value=""32"" />
                <Setter Property=""Height"" Value=""10"" />
                <Setter Property=""MinHeight"" Value=""10"" />
                <Setter Property=""Template"" Value=""{StaticResource FlowHorizontalScrollBarTemplate}"" />
            </Trigger>
        </Style.Triggers>
    </Style>
</ResourceDictionary>");

            foreach (var key in dictionary.Keys)
            {
                Resources[key] = dictionary[key];
            }
        }

        internal static SolidColorBrush BrushFromRgb(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
