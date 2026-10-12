using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using Object = UnityEngine.Object;

namespace Knot.Editor
{
    [InitializeOnLoad]
    public partial class NodeEditorWindow : EditorWindow, IHasCustomMenu
    {
        public static NodeEditorWindow Current;

        /// <summary> Stores port handle rects in node-local space. Add node.position when drawing. </summary>
        private Dictionary<NodePort, Rect> PortConnectionPoints { get; } = new Dictionary<NodePort, Rect>();

        [SerializeField] private bool _portRectsLocal;

        public void SetNodePosition(Node node, Vector2 pos)
        {
            if (node == null) return;
            node.Position = pos;
        }

        public bool TryGetPortGridRect(NodePort port, out Rect gridRect)
        {
            gridRect = default;
            if (port == null || !PortConnectionPoints.TryGetValue(port, out gridRect)) return false;
            if (port.Node != null) gridRect.position += port.Node.Position;
            return true;
        }

        [SerializeField] private NodePortReference[] _references = Array.Empty<NodePortReference>();
        [SerializeField] private Rect[] _rects = Array.Empty<Rect>();

        private Func<bool> IsDocked
        {
            get
            {
                if (_isDocked == null) _isDocked = this.GetIsDockedDelegate();
                return _isDocked;
            }
        }

        private Func<bool> _isDocked;

        [Serializable]
        private class NodePortReference
        {
            [SerializeField] private Node _node;
            [SerializeField] private string _name;

            public NodePortReference(NodePort nodePort)
            {
                _node = nodePort.Node;
                _name = nodePort.FieldName;
            }

            public NodePort GetNodePort()
            {
                if (_node == null)
                {
                    return null;
                }

                return _node.GetPort(_name);
            }
        }

        internal UI.GraphUiController GraphUi { get; private set; }

        private void CreateGUI()
        {
            var uxml = UI.UIAssets.LoadUxml("GraphWindow");
            if (uxml == null) return;
            rootVisualElement.Clear();
            var shell = new UI.GraphWindow(this);
            shell.style.flexGrow = 1;
            rootVisualElement.Add(shell);
            GraphUi = shell.Controller;
        }

        internal void RebuildUi()
        {
            GraphUi?.Rebuild();
        }

        internal void RefreshNodes()
        {
            GraphUi?.RefreshNodes();
        }

        private void OnDestroy()
        {
            GraphUi?.Dispose();
            GraphUi = null;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            _portRectsLocal = true;
            // Cache portConnectionPoints before serialization starts
            var count = PortConnectionPoints.Count;
            _references = new NodePortReference[count];
            _rects = new Rect[count];
            var index = 0;
            foreach (var portConnectionPoint in PortConnectionPoints)
            {
                _references[index] = new NodePortReference(portConnectionPoint.Key);
                _rects[index] = portConnectionPoint.Value;
                index++;
            }
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            // Reload portConnectionPoints if there are any
            var length = _references.Length;
            if (length == _rects.Length)
            {
                for (var i = 0; i < length; i++)
                {
                    var nodePort = _references[i].GetNodePort();
                    if (nodePort != null)
                        PortConnectionPoints.Add(nodePort, _rects[i]);
                }
            }

            if (!_portRectsLocal)
            {
                ConvertPortRectsToLocal();
                _portRectsLocal = true;
            }

            ApplyTitle();
        }

        private void OnUndoRedo()
        {
            Repaint();
        }

        private void ConvertPortRectsToLocal()
        {
            var ports = new List<NodePort>(PortConnectionPoints.Keys);
            for (var i = 0; i < ports.Count; i++)
            {
                var port = ports[i];
                if (port == null || port.Node == null) continue;
                var rect = PortConnectionPoints[port];
                rect.position -= port.Node.Position;
                PortConnectionPoints[port] = rect;
            }
        }

        public Dictionary<Node, Vector2> NodeSizes { get; } = new Dictionary<Node, Vector2>();

        public NodeGraph Graph;

        public Vector2 PanOffset
        {
            get => _panOffset;
            set
            {
                _panOffset = value;
                Repaint();
            }
        }

        private Vector2 _panOffset;

        public float Zoom
        {
            get => _zoom;
            set
            {
                _zoom = Mathf.Clamp(value, NodeEditorPreferences.GetSettings().MinZoom,
                    NodeEditorPreferences.GetSettings().MaxZoom);
                Repaint();
            }
        }

        private float _zoom = 1;
        [SerializeField] private bool _showBlackboard;
        [SerializeField] private float _blackboardWidth = 300f;
        [SerializeField] private string _nodeSearch = "";
        [SerializeField] private string _highlightedVariableId = "";

        internal bool ShowBlackboard
        {
            get => _showBlackboard;
            set
            {
                _showBlackboard = value;
                Repaint();
            }
        }

        internal float BlackboardWidth
        {
            get => Mathf.Clamp(_blackboardWidth, 300f, 520f);
            set => _blackboardWidth = Mathf.Clamp(value, 300f, 520f);
        }

        public const float OverlayToolbarHeight = 28f;

        public int TabPadding => IsDocked() ? 19 : 22;

        internal Rect GetToolbarHitRect()
        {
            return new Rect(0f, 0f, position.width, OverlayToolbarHeight);
        }

        internal Rect GetBlackboardHitRect()
        {
            return new Rect(
                Mathf.Max(0f, position.width - BlackboardWidth),
                OverlayToolbarHeight,
                BlackboardWidth,
                Mathf.Max(0f, position.height - OverlayToolbarHeight));
        }

        internal Rect GetToolbarRect(float originY = 0f)
        {
            return new Rect(0f, originY, position.width, OverlayToolbarHeight);
        }

        internal Rect GetBlackboardRect(float originY = 0f)
        {
            var y = originY + OverlayToolbarHeight;
            return new Rect(
                Mathf.Max(0f, position.width - BlackboardWidth),
                y,
                BlackboardWidth,
                Mathf.Max(0f, position.height - y));
        }

        internal bool IsPointerOverOverlay()
        {
            var mouse = Event.current.mousePosition;
            if (GetToolbarHitRect().Contains(mouse)) return true;
            return ShowBlackboard && GetBlackboardHitRect().Contains(mouse);
        }

        internal static bool IsOverlayBlockingEvent(Event e)
        {
            return e.isMouse ||
                   e.type == EventType.ScrollWheel ||
                   e.type == EventType.ContextClick ||
                   e.type == EventType.DragUpdated ||
                   e.type == EventType.DragPerform;
        }

        internal bool ShouldDrawNodeFields
        {
            get
            {
                var compactZoom = NodeEditorPreferences.GetSettings().CompactNodeZoom;
                return compactZoom <= 0f || Zoom < compactZoom;
            }
        }

        internal string HighlightedVariableId
        {
            get => _highlightedVariableId ?? "";
            set => _highlightedVariableId = value ?? "";
        }

        internal string NodeSearch
        {
            get => _nodeSearch ?? "";
            set
            {
                var normalized = value ?? "";
                if (_nodeSearch == normalized) return;
                _nodeSearch = normalized;
                Repaint();
            }
        }

        internal bool IsNodeSearchActive => !string.IsNullOrWhiteSpace(NodeSearch);

        internal bool IsHighlightFilterActive =>
            IsNodeSearchActive || !string.IsNullOrEmpty(_highlightedVariableId);

        internal bool HighlightHasMatches { get; private set; }

        internal void RefreshHighlightHasMatches()
        {
            HighlightHasMatches = false;
            if (!IsHighlightFilterActive || Graph?.Nodes == null) return;
            for (var i = 0; i < Graph.Nodes.Count; i++)
            {
                var node = Graph.Nodes[i];
                if (node != null && IsNodeHighlighted(node))
                {
                    HighlightHasMatches = true;
                    return;
                }
            }
        }

        internal bool IsNodeHighlighted(Node node)
        {
            if (node == null || !IsHighlightFilterActive) return true;

            if (IsNodeSearchActive)
            {
                var query = NodeSearch.Trim();
                if (!NodeMatchesSearch(node, query))
                {
                    return false;
                }
            }

            if (string.IsNullOrEmpty(_highlightedVariableId)) return true;
            if (NodeReferencesVariable(node, _highlightedVariableId)) return true;

            foreach (var input in node.Inputs)
            {
                if (input == null) continue;
                for (var i = 0; i < input.ConnectionCount; i++)
                {
                    var connection = input.GetConnection(i);
                    if (connection != null && NodeReferencesVariable(connection.Node, _highlightedVariableId))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal Node[] GetNodeSearchMatches()
        {
            if (!IsNodeSearchActive || Graph?.Nodes == null) return Array.Empty<Node>();
            var query = NodeSearch.Trim();
            return Graph.Nodes
                .Where(node =>
                    NodeMatchesSearch(node, query))
                .ToArray();
        }

        public static bool NodeMatchesSearch(Node node, string query)
        {
            if (node == null || string.IsNullOrWhiteSpace(query)) return false;
            var normalized = query.Trim();
            return node.name.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   node.GetType().Name.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal void FrameNodeSearchResult(int direction)
        {
            var matches = GetNodeSearchMatches();
            if (matches.Length == 0) return;

            var index = Array.IndexOf(matches, Selection.activeObject as Node);
            if (index < 0)
            {
                index = direction < 0 ? matches.Length - 1 : 0;
            }
            else
            {
                index = (index + direction + matches.Length) % matches.Length;
            }

            Selection.activeObject = matches[index];
            FrameSelection();
        }

        internal void ToggleHighlightedVariable(string variableId)
        {
            _highlightedVariableId = _highlightedVariableId == variableId ? "" : variableId ?? "";
            Repaint();
        }

        private static bool NodeReferencesVariable(Node node, string variableId)
        {
            if (node == null || string.IsNullOrEmpty(variableId)) return false;

            var serializedNode = new SerializedObject(node);
            var iterator = serializedNode.GetIterator();
            var enterChildren = true;
            while (iterator.Next(enterChildren))
            {
                enterChildren = iterator.propertyType == SerializedPropertyType.Generic ||
                                iterator.propertyType == SerializedPropertyType.ManagedReference;
                if (iterator.propertyType == SerializedPropertyType.String &&
                    iterator.name == BlackboardVariableReference.VariableIdField &&
                    iterator.stringValue == variableId)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnFocus()
        {
            Current = this;
            ApplyTitle();
            ValidateGraphEditor();
            GraphEditor?.OnWindowFocus();
        }

        private void OnLostFocus()
        {
            GraphEditor?.OnWindowFocusLost();
        }

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
        }

        /// <summary> Handle Selection Change events</summary>
        private static void OnSelectionChanged()
        {
            var nodeGraph = Selection.activeObject as NodeGraph;
            if (nodeGraph && !AssetDatabase.Contains(nodeGraph))
            {
                if (NodeEditorPreferences.GetSettings().OpenOnCreate) Open(nodeGraph);
            }
        }

        /// <summary> Make sure the graph editor is assigned and to the right object </summary>
        internal void ValidateGraphEditor()
        {
            var editor = NodeGraphEditor.GetEditor(Graph, this);
            if (editor == null || GraphEditor == editor) return;
            GraphEditor = editor;
            editor.OnOpen();
        }

        /// <summary> Create editor window </summary>
        public static NodeEditorWindow Init()
        {
            var w = CreateInstance<NodeEditorWindow>();
            w.titleContent = new GUIContent("Knot");
            w.wantsMouseMove = true;
            w.Show();
            return w;
        }

        public void Save()
        {
            if (AssetDatabase.Contains(Graph))
            {
                EditorUtility.SetDirty(Graph);
                AssetDatabase.SaveAssets();
            }
            else SaveAs();
        }

        public void SaveAs()
        {
            var path = EditorUtility.SaveFilePanelInProject("Save NodeGraph", "NewNodeGraph", "asset", "");
            if (string.IsNullOrEmpty(path)) return;
            else
            {
                var existingGraph = AssetDatabase.LoadAssetAtPath<NodeGraph>(path);
                if (existingGraph != null) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(Graph, path);
                EditorUtility.SetDirty(Graph);
                AssetDatabase.SaveAssets();
            }
        }

        private void DraggableWindow(int windowID)
        {
            GUI.DragWindow();
        }

        public Vector2 WindowToGridPosition(Vector2 windowPosition)
        {
            return (windowPosition - (position.size * 0.5f) - (PanOffset / Zoom)) * Zoom;
        }

        public Vector2 WindowToGridPositionNoClipped(Vector2 zoomedPosition)
        {
            var center = position.size * 0.5f;
            return zoomedPosition - (center * Zoom) - PanOffset;
        }

        public Vector2 GridToWindowPosition(Vector2 gridPosition)
        {
            return (position.size * 0.5f) + (PanOffset / Zoom) + (gridPosition / Zoom);
        }

        public Rect GridToWindowRectNoClipped(Rect gridRect)
        {
            gridRect.position = GridToWindowPositionNoClipped(gridRect.position);
            return gridRect;
        }

        public Rect GridToWindowRect(Rect gridRect)
        {
            gridRect.position = GridToWindowPosition(gridRect.position);
            gridRect.size /= Zoom;
            return gridRect;
        }

        public Vector2 GridToWindowPositionNoClipped(Vector2 gridPosition)
        {
            var center = position.size * 0.5f;
            // UI Sharpness complete fix - Round final offset not panOffset
            var xOffset = Mathf.Round(center.x * Zoom + (PanOffset.x + gridPosition.x));
            var yOffset = Mathf.Round(center.y * Zoom + (PanOffset.y + gridPosition.y));
            return new Vector2(xOffset, yOffset);
        }

        public void SelectNode(Node node, bool add)
        {
            if (add)
            {
                var selection = new List<Object>(Selection.objects);
                selection.Add(node);
                Selection.objects = selection.ToArray();
            }
            else Selection.objects = new Object[] {node};
        }

        public void DeselectNode(Node node)
        {
            var selection = new List<Object>(Selection.objects);
            selection.Remove(node);
            Selection.objects = selection.ToArray();
        }

        // Unity 6.4: OnOpenAsset still takes int; EntityIdToObject exists.
        // Unity 6.5+: OnOpenAsset takes EntityId. Unity 6.6 deprecates int→EntityId conversion.
#if UNITY_6000_5_OR_NEWER
        [OnOpenAsset(0)]
        public static bool OnOpen(EntityId entityId, int line)
        {
            var nodeGraph = EditorUtility.EntityIdToObject(entityId) as NodeGraph;
#else
        [OnOpenAsset(0)]
        public static bool OnOpen(int instanceID, int line)
        {
#if UNITY_6000_4_OR_NEWER
            var nodeGraph = EditorUtility.EntityIdToObject(instanceID) as Knot.NodeGraph;
#else
            var nodeGraph = EditorUtility.InstanceIDToObject(instanceID) as Knot.NodeGraph;
#endif
#endif
            if (nodeGraph != null)
            {
                Open(nodeGraph);
                return true;
            }

            return false;
        }

        internal void ApplyTitle()
        {
            var name = Graph != null ? Graph.name : null;
            if (string.IsNullOrEmpty(name)) name = "Knot";
            if (titleContent != null && titleContent.text == name) return;
            titleContent = new GUIContent(name);
        }

        /// <summary>Open the provided graph in the NodeEditor</summary>
        public static NodeEditorWindow Open(NodeGraph graph)
        {
            if (!graph) return null;

            var w = FindOrCreate(graph);
            w.wantsMouseMove = true;
            w.Graph = graph;
            w.ApplyTitle();
            w.RebuildUi();
            w.Focus();
            return w;
        }

        private static NodeEditorWindow FindOrCreate(NodeGraph graph)
        {
            var windows = Resources.FindObjectsOfTypeAll<NodeEditorWindow>();
            for (var i = 0; i < windows.Length; i++)
            {
                var open = windows[i];
                if (open != null && open.Graph == graph) return open;
            }

            if (!NodeEditorPreferences.GetSettings().AllowMultipleWindows)
            {
                var focused = focusedWindow as NodeEditorWindow;
                if (focused != null) return focused;
                if (Current != null) return Current;
                for (var i = 0; i < windows.Length; i++)
                {
                    if (windows[i] != null) return windows[i];
                }
            }

            var created = CreateInstance<NodeEditorWindow>();
            created.wantsMouseMove = true;
            created.Show();
            return created;
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            GraphEditor?.AddItemsToMenu(menu);
        }

        /// <summary> Repaint all open NodeEditorWindows. </summary>
        public static void RepaintAll()
        {
            var windows = Resources.FindObjectsOfTypeAll<NodeEditorWindow>();
            for (var i = 0; i < windows.Length; i++)
            {
                windows[i].Repaint();
            }
        }
    }
}