using System;
using System.Windows;
using System.Windows.Markup;

namespace Vision.Flow.Designer.Wpf.Theming
{
    /// <summary>
    /// 提供可由独立设计器和业务宿主共同使用的现代设计器主题。
    /// </summary>
    public static class FlowDesignerTheme
    {
        public const string PageBackgroundBrushKey = "FlowPageBackground";
        public const string PanelBackgroundBrushKey = "FlowPanelBackground";
        public const string PanelBorderBrushKey = "FlowPanelBorder";
        public const string AccentBrushKey = "FlowAccent";
        public const string AccentHoverBrushKey = "FlowAccentHover";
        public const string SelectionBrushKey = "FlowSelection";
        public const string TextBrushKey = "FlowText";
        public const string MutedTextBrushKey = "FlowMutedText";
        public const string ErrorBrushKey = "FlowError";
        public const string FieldBackgroundBrushKey = "FlowFieldBackground";
        public const string ReadOnlyBackgroundBrushKey = "FlowReadOnlyBackground";
        public const string FieldTextBoxStyleKey = "FlowFieldTextBoxStyle";
        public const string FieldComboBoxStyleKey = "FlowFieldComboBoxStyle";
        public const string ToolbarButtonStyleKey = "FlowToolbarButtonStyle";
        public const string PrimaryButtonStyleKey = "FlowPrimaryButtonStyle";
        public const string SecondaryButtonStyleKey = "FlowSecondaryButtonStyle";
        public const string SegmentButtonStyleKey = "FlowSegmentButtonStyle";
        public const string CardBorderStyleKey = "FlowCardBorderStyle";
        public const string ErrorTextStyleKey = "FlowErrorTextStyle";
        public const string ExpanderStyleKey = "FlowExpanderStyle";
        public const string SwitchCheckBoxStyleKey = "FlowSwitchCheckBoxStyle";

        /// <summary>
        /// 创建独立的资源字典。每个调用方都获得可安全合并的独立实例。
        /// </summary>
        public static ResourceDictionary CreateModern()
        {
            return (ResourceDictionary)XamlReader.Parse(@"
<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <FontFamily x:Key=""FlowFontFamily"">Microsoft YaHei UI, Segoe UI</FontFamily>
    <SolidColorBrush x:Key=""FlowPageBackground"" Color=""#F4F7FA"" />
    <SolidColorBrush x:Key=""FlowWorkspaceBackground"" Color=""#F7F9FC"" />
    <SolidColorBrush x:Key=""FlowPanelBackground"" Color=""#FFFFFF"" />
    <SolidColorBrush x:Key=""FlowPanelBorder"" Color=""#DDE5EF"" />
    <SolidColorBrush x:Key=""FlowDivider"" Color=""#E7ECF2"" />
    <SolidColorBrush x:Key=""FlowAccent"" Color=""#10A372"" />
    <SolidColorBrush x:Key=""FlowAccentHover"" Color=""#0D8B61"" />
    <SolidColorBrush x:Key=""FlowAccentSoft"" Color=""#EAF8F2"" />
    <SolidColorBrush x:Key=""FlowSelection"" Color=""#2F80ED"" />
    <SolidColorBrush x:Key=""FlowSelectionSoft"" Color=""#EAF3FF"" />
    <SolidColorBrush x:Key=""FlowText"" Color=""#243247"" />
    <SolidColorBrush x:Key=""FlowMutedText"" Color=""#7A879A"" />
    <SolidColorBrush x:Key=""FlowError"" Color=""#D14343"" />
    <SolidColorBrush x:Key=""FlowWarning"" Color=""#C97A10"" />
    <SolidColorBrush x:Key=""FlowFieldBackground"" Color=""#FFFFFF"" />
    <SolidColorBrush x:Key=""FlowReadOnlyBackground"" Color=""#F5F7FA"" />

    <Style x:Key=""FlowFieldTextBoxStyle"" TargetType=""{x:Type TextBox}"">
        <Setter Property=""MinHeight"" Value=""40"" />
        <Setter Property=""Padding"" Value=""11,8"" />
        <Setter Property=""VerticalContentAlignment"" Value=""Center"" />
        <Setter Property=""Background"" Value=""{StaticResource FlowFieldBackground}"" />
        <Setter Property=""Foreground"" Value=""{StaticResource FlowText}"" />
        <Setter Property=""BorderBrush"" Value=""{StaticResource FlowPanelBorder}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""FontFamily"" Value=""{StaticResource FlowFontFamily}"" />
        <Setter Property=""FontSize"" Value=""12"" />
        <Style.Triggers>
            <Trigger Property=""IsKeyboardFocused"" Value=""True"">
                <Setter Property=""BorderBrush"" Value=""{StaticResource FlowAccent}"" />
                <Setter Property=""CaretBrush"" Value=""{StaticResource FlowAccent}"" />
            </Trigger>
            <Trigger Property=""IsReadOnly"" Value=""True"">
                <Setter Property=""Background"" Value=""{StaticResource FlowReadOnlyBackground}"" />
                <Setter Property=""Foreground"" Value=""{StaticResource FlowMutedText}"" />
            </Trigger>
            <Trigger Property=""IsEnabled"" Value=""False"">
                <Setter Property=""Opacity"" Value=""0.62"" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <Style x:Key=""FlowFieldComboBoxStyle"" TargetType=""{x:Type ComboBox}"">
        <Setter Property=""MinHeight"" Value=""40"" />
        <Setter Property=""Padding"" Value=""9,6"" />
        <Setter Property=""VerticalContentAlignment"" Value=""Center"" />
        <Setter Property=""Background"" Value=""{StaticResource FlowFieldBackground}"" />
        <Setter Property=""Foreground"" Value=""{StaticResource FlowText}"" />
        <Setter Property=""BorderBrush"" Value=""{StaticResource FlowPanelBorder}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""FontFamily"" Value=""{StaticResource FlowFontFamily}"" />
        <Setter Property=""FontSize"" Value=""12"" />
        <Style.Triggers>
            <Trigger Property=""IsKeyboardFocusWithin"" Value=""True"">
                <Setter Property=""BorderBrush"" Value=""{StaticResource FlowAccent}"" />
            </Trigger>
            <Trigger Property=""IsEnabled"" Value=""False"">
                <Setter Property=""Background"" Value=""{StaticResource FlowReadOnlyBackground}"" />
                <Setter Property=""Opacity"" Value=""0.62"" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <Style x:Key=""FlowToolbarButtonStyle"" TargetType=""{x:Type Button}"">
        <Setter Property=""MinHeight"" Value=""34"" />
        <Setter Property=""Padding"" Value=""12,0"" />
        <Setter Property=""Margin"" Value=""0,0,6,0"" />
        <Setter Property=""Background"" Value=""Transparent"" />
        <Setter Property=""Foreground"" Value=""{StaticResource FlowText}"" />
        <Setter Property=""BorderBrush"" Value=""Transparent"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""FontFamily"" Value=""{StaticResource FlowFontFamily}"" />
        <Setter Property=""Cursor"" Value=""Hand"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type Button}"">
                    <Border x:Name=""Chrome""
                            Padding=""{TemplateBinding Padding}""
                            Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""5""
                            SnapsToDevicePixels=""True"">
                        <ContentPresenter HorizontalAlignment=""Center""
                                          VerticalAlignment=""Center"" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""Chrome"" Property=""Background"" Value=""#F0F4F8"" />
                            <Setter TargetName=""Chrome"" Property=""BorderBrush"" Value=""#DDE5EF"" />
                        </Trigger>
                        <Trigger Property=""IsPressed"" Value=""True"">
                            <Setter TargetName=""Chrome"" Property=""Background"" Value=""#E7EDF4"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter TargetName=""Chrome"" Property=""Opacity"" Value=""0.42"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key=""FlowPrimaryButtonStyle"" TargetType=""{x:Type Button}""
           BasedOn=""{StaticResource FlowToolbarButtonStyle}"">
        <Setter Property=""Background"" Value=""{StaticResource FlowAccent}"" />
        <Setter Property=""BorderBrush"" Value=""{StaticResource FlowAccent}"" />
        <Setter Property=""Foreground"" Value=""White"" />
        <Setter Property=""FontWeight"" Value=""SemiBold"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type Button}"">
                    <Border x:Name=""Chrome""
                            Padding=""{TemplateBinding Padding}""
                            Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""5""
                            SnapsToDevicePixels=""True"">
                        <ContentPresenter HorizontalAlignment=""Center""
                                          VerticalAlignment=""Center"" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""Chrome"" Property=""Background"" Value=""{StaticResource FlowAccentHover}"" />
                            <Setter TargetName=""Chrome"" Property=""BorderBrush"" Value=""{StaticResource FlowAccentHover}"" />
                        </Trigger>
                        <Trigger Property=""IsPressed"" Value=""True"">
                            <Setter TargetName=""Chrome"" Property=""Background"" Value=""#087554"" />
                            <Setter TargetName=""Chrome"" Property=""BorderBrush"" Value=""#087554"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter TargetName=""Chrome"" Property=""Opacity"" Value=""0.42"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key=""FlowSecondaryButtonStyle"" TargetType=""{x:Type Button}""
           BasedOn=""{StaticResource FlowToolbarButtonStyle}"">
        <Setter Property=""Background"" Value=""White"" />
        <Setter Property=""BorderBrush"" Value=""{StaticResource FlowPanelBorder}"" />
    </Style>

    <Style x:Key=""FlowSegmentButtonStyle"" TargetType=""{x:Type Button}"">
        <Setter Property=""Height"" Value=""36"" />
        <Setter Property=""Padding"" Value=""5,0"" />
        <Setter Property=""Margin"" Value=""0"" />
        <Setter Property=""Background"" Value=""White"" />
        <Setter Property=""Foreground"" Value=""{StaticResource FlowMutedText}"" />
        <Setter Property=""BorderBrush"" Value=""{StaticResource FlowPanelBorder}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""FontFamily"" Value=""{StaticResource FlowFontFamily}"" />
        <Setter Property=""FontSize"" Value=""11.5"" />
        <Setter Property=""Cursor"" Value=""Hand"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type Button}"">
                    <Border x:Name=""Chrome""
                            Padding=""{TemplateBinding Padding}""
                            Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""4"">
                        <ContentPresenter HorizontalAlignment=""Center""
                                          VerticalAlignment=""Center"" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""Chrome"" Property=""Opacity"" Value=""0.82"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter TargetName=""Chrome"" Property=""Opacity"" Value=""0.48"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key=""FlowCardBorderStyle"" TargetType=""{x:Type Border}"">
        <Setter Property=""Background"" Value=""{StaticResource FlowPanelBackground}"" />
        <Setter Property=""BorderBrush"" Value=""{StaticResource FlowPanelBorder}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""CornerRadius"" Value=""7"" />
        <Setter Property=""Padding"" Value=""12"" />
    </Style>

    <Style x:Key=""FlowErrorTextStyle"" TargetType=""{x:Type TextBlock}"">
        <Setter Property=""Foreground"" Value=""{StaticResource FlowError}"" />
        <Setter Property=""FontFamily"" Value=""{StaticResource FlowFontFamily}"" />
        <Setter Property=""FontSize"" Value=""11"" />
        <Setter Property=""Margin"" Value=""1,3,0,3"" />
        <Setter Property=""TextWrapping"" Value=""Wrap"" />
    </Style>

    <Style x:Key=""FlowExpanderStyle"" TargetType=""{x:Type Expander}"">
        <Setter Property=""Foreground"" Value=""{StaticResource FlowText}"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type Expander}"">
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition Height=""Auto"" />
                            <RowDefinition Height=""Auto"" />
                        </Grid.RowDefinitions>
                        <ToggleButton x:Name=""HeaderSite""
                                      Grid.Row=""0""
                                      Background=""Transparent""
                                      BorderThickness=""0""
                                      Padding=""2""
                                      HorizontalContentAlignment=""Stretch""
                                      IsChecked=""{Binding IsExpanded, RelativeSource={RelativeSource TemplatedParent}}"">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width=""18"" />
                                    <ColumnDefinition Width=""*"" />
                                </Grid.ColumnDefinitions>
                                <Path x:Name=""Arrow""
                                      Grid.Column=""0""
                                      Width=""9""
                                      Height=""9""
                                      Data=""M1,3 L4.5,6.5 L8,3""
                                      Stroke=""#7A879A""
                                      StrokeThickness=""1.5""
                                      StrokeStartLineCap=""Round""
                                      StrokeEndLineCap=""Round""
                                      RenderTransformOrigin=""0.5,0.5"">
                                    <Path.RenderTransform>
                                        <RotateTransform Angle=""-90"" />
                                    </Path.RenderTransform>
                                </Path>
                                <ContentPresenter Grid.Column=""1""
                                                  ContentSource=""Header""
                                                  VerticalAlignment=""Center"" />
                            </Grid>
                        </ToggleButton>
                        <ContentPresenter x:Name=""ExpandSite""
                                          Grid.Row=""1""
                                          Visibility=""Collapsed""
                                          ContentSource=""Content"" />
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsExpanded"" Value=""True"">
                            <Setter TargetName=""ExpandSite"" Property=""Visibility"" Value=""Visible"" />
                            <Setter TargetName=""Arrow"" Property=""RenderTransform"">
                                <Setter.Value>
                                    <RotateTransform Angle=""0"" />
                                </Setter.Value>
                            </Setter>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key=""FlowSwitchCheckBoxStyle"" TargetType=""{x:Type CheckBox}"">
        <Setter Property=""Foreground"" Value=""{StaticResource FlowText}"" />
        <Setter Property=""VerticalContentAlignment"" Value=""Center"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type CheckBox}"">
                    <DockPanel LastChildFill=""True"">
                        <Border x:Name=""Track""
                                Width=""36""
                                Height=""20""
                                Margin=""0,0,8,0""
                                Background=""#C7D0DB""
                                CornerRadius=""10""
                                DockPanel.Dock=""Left"">
                            <Ellipse x:Name=""Thumb""
                                     Width=""16""
                                     Height=""16""
                                     Margin=""2""
                                     HorizontalAlignment=""Left""
                                     Fill=""White"" />
                        </Border>
                        <ContentPresenter VerticalAlignment=""Center"" />
                    </DockPanel>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsChecked"" Value=""True"">
                            <Setter TargetName=""Track"" Property=""Background"" Value=""{StaticResource FlowAccent}"" />
                            <Setter TargetName=""Thumb"" Property=""HorizontalAlignment"" Value=""Right"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter TargetName=""Track"" Property=""Opacity"" Value=""0.48"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType=""{x:Type MenuItem}"">
        <Setter Property=""FontFamily"" Value=""{StaticResource FlowFontFamily}"" />
        <Setter Property=""Foreground"" Value=""{StaticResource FlowText}"" />
        <Setter Property=""Padding"" Value=""10,7"" />
    </Style>

    <Style TargetType=""{x:Type ToolTip}"">
        <Setter Property=""Background"" Value=""#243247"" />
        <Setter Property=""Foreground"" Value=""White"" />
        <Setter Property=""Padding"" Value=""8,5"" />
        <Setter Property=""FontFamily"" Value=""{StaticResource FlowFontFamily}"" />
    </Style>

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
                <Setter Property=""Background"" Value=""#0D8B61"" />
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
        }

        /// <summary>
        /// 将现代主题合并到指定元素，重复调用不会共享可变资源实例。
        /// </summary>
        public static void ApplyTo(FrameworkElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            element.Resources.MergedDictionaries.Add(CreateModern());
        }
    }
}
