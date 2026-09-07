using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Designer.Wpf.Controls;

namespace Vision.Flow.Tests
{
    internal static class DesignerRenderingTests
    {
        /// <summary>千边图仅修改被移动节点的两条关联边，并复用曲线、菜单及选择事件。</summary>
        public static Task LargeGraphUpdatesOnlyAdjacentEdges()
        {
            OnSta(delegate
            {
                var document = CreateDocument(1001);
                var layer = new EdgeLayerControl();
                layer.Render(document, null, null);
                var groups = layer.Children.Cast<Canvas>().ToArray();
                var paths = groups.Select(x => (System.Windows.Shapes.Path)x.Children[1]).ToArray();
                var geometries = paths.Select(x => (PathGeometry)x.Data).ToArray();
                var changed = new HashSet<int>();
                for (int index = 0; index < geometries.Length; index++)
                {
                    int capturedIndex = index;
                    geometries[index].Changed += delegate { changed.Add(capturedIndex); };
                }
                document.View.Nodes["node500"].X += 40;
                layer.UpdateNodes(document, new[] { "node500" }, null);
                AssertEx.True(changed.SetEquals(new[] { 499, 500 }), "Dragging one node in a 1000-edge chain must update exactly its two adjacent geometries.");
                layer.Render(document, document.Runtime.Edges[0], null);
                AssertEx.True(groups.SequenceEqual(layer.Children.Cast<Canvas>()), "Full synchronization must preserve existing visual identities.");
                AssertEx.True(geometries.SequenceEqual(paths.Select(x => (PathGeometry)x.Data)), "Selection must reuse existing path geometries.");
                AssertEx.Equal(2.4, paths[0].StrokeThickness, "Selection highlighting must still apply.");
                EdgeDefinition deleted = null;
                layer.EdgeDeleteRequested += edge => deleted = edge;
                var menu = (MenuItem)groups[0].ContextMenu.Items[0];
                layer.SetReadOnly(true);
                AssertEx.False(menu.IsEnabled, "Reused menus must follow read-only changes.");
                layer.SetReadOnly(false);
                menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                AssertEx.True(ReferenceEquals(document.Runtime.Edges[0], deleted), "The reused delete menu must still identify its document edge.");
                document.Runtime.Edges.RemoveAt(0);
                layer.Render(document, null, null);
                AssertEx.Equal(999, layer.Children.Count, "Removing an edge must remove only its visual.");
                layer.SetPreview(new Point(1, 2), new Point(3, 4));
                var preview = layer.Children[layer.Children.Count - 1];
                layer.SetPreview(new Point(5, 6), new Point(7, 8));
                AssertEx.True(ReferenceEquals(preview, layer.Children[layer.Children.Count - 1]), "Preview updates must reuse their path.");
                layer.ClearPreview();
                AssertEx.Equal(999, layer.Children.Count, "Clearing a preview must preserve every document edge.");
            });
            return Task.CompletedTask;
        }

        /// <summary>高频节点拖动在合成帧统一更新，吸附未变化时不排队，全图扩展保持边位置一致。</summary>
        public static Task DragFramesCoalesceAndPreserveExpansion()
        {
            OnSta(delegate
            {
                var control = new FlowDesignerControl(null, new FlowDesignerOptions { LoadSampleOnStartup = false });
                control.LoadDocumentAsync(CreateDocument(3)).GetAwaiter().GetResult();
                control.Measure(new Size(1400, 900));
                control.Arrange(new Rect(0, 0, 1400, 900));
                control.UpdateLayout();
                var cards = Field<Dictionary<string, NodeCardControl>>(control, "_nodeCards");
                var card = cards["node1"];
                typeof(FlowDesignerControl).GetField("_dragCard", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(control, card);
                var layer = Field<EdgeLayerControl>(control, "_edges");
                var geometry = (PathGeometry)((System.Windows.Shapes.Path)((Canvas)layer.Children[0]).Children[1]).Data;
                int changes = 0;
                geometry.Changed += delegate { changes++; };
                var original = new Point(Canvas.GetLeft(card), Canvas.GetTop(card));
                Invoke(control, "MoveDraggedNode", original);
                AssertEx.False(Field<bool>(control, "_isCanvasFrameScheduled"), "An unchanged snapped coordinate must not schedule a frame.");
                for (int index = 1; index <= 100; index++)
                    Invoke(control, "MoveDraggedNode", new Point(original.X + index, original.Y));
                AssertEx.Equal(0, changes, "Mouse events must not rebuild or update edge geometry before the render frame.");
                AssertEx.Equal(1, Field<HashSet<string>>(control, "_pendingEdgeNodes").Count, "A frame must deduplicate the moved node.");
                Invoke(control, "OnCanvasInteractionFrame", null, EventArgs.Empty);
                AssertEx.True(changes > 0, "The render frame must apply the final node position.");
                AssertEx.Equal(0, Field<HashSet<string>>(control, "_pendingEdgeNodes").Count, "Applied work must not linger into the next frame.");
                var expected = card.InputPortControls.First().GetAnchorPoint(card);
                expected.Offset(Canvas.GetLeft(card), Canvas.GetTop(card));
                AssertEx.Equal(expected, ((BezierSegment)geometry.Figures[0].Segments[0]).Point3, "Frame geometry must use the latest logical coordinate without forcing the parent layout.");
                Invoke(control, "MoveDraggedNode", new Point(8, 8));
                AssertEx.True(Field<bool>(control, "_requiresFullEdgeRefresh"), "Left/top canvas expansion must mark the translated graph for full synchronization.");
                Invoke(control, "OnCanvasInteractionFrame", null, EventArgs.Empty);
                AssertEx.False(Field<bool>(control, "_requiresFullEdgeRefresh"), "Expanded canvas synchronization must finish in the frame.");
                Invoke(control, "CancelCanvasInteractionFrame");
            });
            return Task.CompletedTask;
        }

        private static FlowDesignDocument CreateDocument(int count)
        {
            var document = new FlowDesignDocument { FlowId = "render-test", FlowName = "渲染测试" };
            document.Runtime.FlowId = document.FlowId;
            document.Runtime.FlowName = document.FlowName;
            for (int index = 0; index < count; index++)
            {
                string id = "node" + index;
                document.Runtime.Nodes.Add(new NodeDefinition { Id = id, Name = id, Type = "delay.wait", Version = "1.0.0" });
                document.View.Nodes[id] = new NodeViewState { X = 320 + index * 320, Y = 320 };
                if (index > 0)
                    document.Runtime.Edges.Add(new EdgeDefinition { FromNodeId = "node" + (index - 1), FromPort = FlowPortNames.Next, ToNodeId = id, ToPort = FlowPortNames.In });
            }
            return document;
        }

        private static T Field<T>(object target, string name)
        {
            return (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        }

        private static void Invoke(object target, string name, params object[] arguments)
        {
            target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, arguments);
        }

        private static void OnSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() => { try { action(); } catch (Exception error) { failure = error; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null)
                throw failure;
        }
    }
}
