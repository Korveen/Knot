using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Knot;

namespace Knot.Editor
{
    public partial class NodeEditorWindow
    {
        private const float FramePadding = 80f;
        private static readonly Vector2 DefaultNodeSize = new Vector2(208f, 80f);
        private static readonly Vector2 DuplicateOffset = new Vector2(30f, 30f);

        public static Node[] CopyBuffer { get; private set; }

        public NodePort HoveredPort { get; internal set; }
        public NodePort DraggedOutput { get; internal set; }
        public NodePort DraggedInput { get; internal set; }

        public bool IsHoveringPort => HoveredPort != null;
        public bool IsDraggingPort => DraggedOutput != null || DraggedInput != null;

        public void Home()
        {
            FrameSelection();
        }

        public void FrameSelection()
        {
            var nodes = Selection.objects.OfType<Node>().Where(node => node != null).ToList();
            if (nodes.Count == 0) FrameAll();
            else FrameNodes(nodes);
        }

        public void FrameAll()
        {
            if (Graph == null) return;
            FrameNodes(Graph.Nodes.Where(node => node != null).ToList());
        }

        private void FrameNodes(List<Node> nodes)
        {
            if (nodes == null || nodes.Count == 0)
            {
                Zoom = 1f;
                PanOffset = Vector2.zero;
                return;
            }

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var node in nodes)
            {
                var size = NodeSizes.TryGetValue(node, out var cachedSize) ? cachedSize : DefaultNodeSize;
                min = Vector2.Min(min, node.Position);
                max = Vector2.Max(max, node.Position + size);
            }

            var center = (min + max) * 0.5f;
            var bounds = max - min;
            var blackboard = ShowBlackboard ? BlackboardWidth : 0f;
            var toolbar = GetToolbarRect().height;
            var viewWidth = Mathf.Max(120f, position.width - blackboard - FramePadding);
            var viewHeight = Mathf.Max(120f, position.height - toolbar - FramePadding);
            var fitZoom = Mathf.Max(bounds.x / viewWidth, bounds.y / viewHeight, 0.01f);
            var settings = NodeEditorPreferences.GetShared();
            Zoom = Mathf.Clamp(fitZoom, settings.MinZoom, settings.MaxZoom);

            PanOffset = new Vector2(
                -center.x - blackboard * 0.5f * Zoom,
                -center.y + toolbar * 0.5f * Zoom);
        }

        public void RemoveSelectedNodes()
        {
            foreach (var node in Selection.objects.OfType<Node>().ToArray())
            {
                GraphEditor.RemoveNode(node);
            }
        }

        public void RenameSelectedNode()
        {
            if (Selection.objects.Length != 1 || Selection.activeObject is not Node node) return;
            if (GraphUi == null || !GraphUi.Nodes.TryGetValue(node, out var view)) return;

            view.BeginRename(newName =>
            {
                NodeEditor.GetEditor(node, this).Rename(newName);
                var path = AssetDatabase.GetAssetPath(node);
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.SetMainObject(node.Graph, path);
                    AssetDatabase.ImportAsset(path);
                }

                node.TriggerOnValidate();
                view.SetTitle(node.name);
            });
        }

        public void DuplicateSelectedNodes()
        {
            var selectedNodes = GetSelectedGraphNodes();
            if (selectedNodes.Length == 0) return;
            InsertDuplicateNodes(selectedNodes, GetTopLeft(selectedNodes) + DuplicateOffset);
        }

        public void CopySelectedNodes()
        {
            CopyBuffer = GetSelectedGraphNodes();
        }

        public void PasteNodes(Vector2 gridPosition)
        {
            InsertDuplicateNodes(CopyBuffer, gridPosition);
        }

        private Node[] GetSelectedGraphNodes()
        {
            return Selection.objects.OfType<Node>().Where(node => node.Graph == Graph).ToArray();
        }

        private static Vector2 GetTopLeft(IEnumerable<Node> nodes)
        {
            return nodes.Select(node => node.Position).Aggregate(Vector2.Min);
        }

        private void InsertDuplicateNodes(Node[] nodes, Vector2 topLeft)
        {
            nodes = nodes?.Where(node => node != null).ToArray();
            if (nodes == null || nodes.Length == 0) return;

            var offset = topLeft - GetTopLeft(nodes);
            var newNodes = new List<Object>(nodes.Length);
            var substitutes = new Dictionary<Node, Node>();
            foreach (var source in nodes)
            {
                if (IsNodeLimitReached(source.GetType())) continue;

                var copy = GraphEditor.CopyNode(source);
                copy.Position = source.Position + offset;
                substitutes.Add(source, copy);
                newNodes.Add(copy);
            }

            foreach (var source in nodes)
            {
                foreach (var port in source.Ports)
                {
                    for (var i = 0; i < port.ConnectionCount; i++)
                    {
                        var connection = port.GetConnection(i);
                        if (connection == null) continue;
                        var input = port.IsInput ? port : connection;
                        var output = port.IsOutput ? port : connection;

                        if (substitutes.TryGetValue(input.Node, out var inputCopy) &&
                            substitutes.TryGetValue(output.Node, out var outputCopy))
                        {
                            inputCopy.UpdatePorts();
                            outputCopy.UpdatePorts();
                            input = inputCopy.GetInputPort(input.FieldName);
                            output = outputCopy.GetOutputPort(output.FieldName);
                        }

                        if (!input.IsConnectedTo(output)) input.Connect(output);
                    }
                }
            }

            EditorUtility.SetDirty(Graph);
            Selection.objects = newNodes.ToArray();
        }

        private bool IsNodeLimitReached(System.Type nodeType)
        {
            if (!NodeEditorUtilities.GetAttrib(nodeType, out Node.DisallowMultipleNodesAttribute disallow))
                return false;
            return Graph.Nodes.Count(node => node != null && node.GetType() == nodeType) >= disallow.Max;
        }
    }
}