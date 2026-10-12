using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XNode;
using XNodeEditor.Internal;
using Object = UnityEngine.Object;

namespace XNodeEditor.UI
{
    public sealed class GraphUiController : IDisposable
    {
        public readonly NodeEditorWindow Window;
        public readonly Dictionary<NodePort, PortView> Ports = new Dictionary<NodePort, PortView>();
        public readonly Dictionary<Node, NodeView> Nodes = new Dictionary<Node, NodeView>();

        private readonly VisualElement _root;
        private readonly VisualElement _toolbar;
        private readonly VisualElement _viewport;
        private readonly VisualElement _content;
        private readonly VisualElement _nodesLayer;
        private readonly VisualElement _groupsLayer;
        private readonly VisualElement _groupEdges;
        private readonly VisualElement _blackboard;
        private readonly VisualElement _blackboardResize;
        private readonly VisualElement _overlay;
        private readonly VisualElement _selectionBox;
        private readonly GridElement _grid;
        private readonly NoodleLayer _noodles;
        private readonly Slider _zoomSlider;
        private readonly Label _zoomLabel;
        private readonly TextField _searchField;
        private readonly DropdownField _variableFilter;
        private readonly VisualElement _graphToolbarSlot;
        private readonly VisualElement _rerouteLayer;
        private readonly List<string> _variableIds = new List<string>();
        private RerouteReference _dragReroute;
        private bool _rerouteDrag;
        private Vector2 _rerouteOrigin;

        private NodePort _dragPort;
        private NodePort _autoConnectPort;
        private Button _blackboardButton;
        private Button _snapButton;
        private Button _indexButton;
        private Label _cursorLabel;
        private Vector2 _dragPointer;
        private bool _panning;
        private bool _boxing;
        private Vector2 _boxStart;
        private bool _draggingNodes;
        private bool _dragCollapseOnClick;
        private Node _dragClickedNode;
        private bool _resizingBlackboard;
        private bool _runtimeVisualsCleared;
        private readonly Dictionary<Node, Vector2> _dragOrigins = new Dictionary<Node, Vector2>();
        private Vector2 _dragPointerStart;
        private GroupNode _resizeGroup;
        private GroupEdge _resizeEdge;
        private Vector2 _resizePointer;
        private Vector2 _resizeOrigin;
        private int _resizeWidth;
        private int _resizeHeight;

        private enum GroupEdge
        {
            N,
            S,
            E,
            W,
            NE,
            NW,
            SE,
            SW
        }

        private sealed class GroupEdgeTag
        {
            public GroupNode Group;
            public GroupEdge Edge;
        }

        public GraphUiController(NodeEditorWindow window, VisualElement root)
        {
            Window = window;
            _root = root;
            _toolbar = root.Q("toolbar");
            _viewport = root.Q("viewport");
            _content = root.Q("content");
            _nodesLayer = root.Q("nodes");
            _groupsLayer = root.Q("groups");
            _groupEdges = root.Q("group-edges");
            if (_nodesLayer != null) _nodesLayer.pickingMode = PickingMode.Ignore;
            if (_groupEdges != null) _groupEdges.pickingMode = PickingMode.Ignore;
            _blackboard = root.Q("blackboard");
            _blackboardResize = root.Q("blackboard-resize");
            _overlay = root.Q("overlay");
            _selectionBox = root.Q("selection-box");

            var gridHost = root.Q("grid");
            _grid = new GridElement();
            _grid.style.flexGrow = 1;
            _viewport.Insert(0, _grid);
            gridHost?.RemoveFromHierarchy();

            var noodleHost = root.Q("noodles");
            _noodles = new NoodleLayer {Controller = this};
            _noodles.AddToClassList("graph-noodles");
            if (noodleHost != null)
            {
                var index = noodleHost.parent.IndexOf(noodleHost);
                noodleHost.parent.Insert(index, _noodles);
                noodleHost.RemoveFromHierarchy();
            }
            else
            {
                _content.Insert(0, _noodles);
            }

            _rerouteLayer = new VisualElement {name = "reroutes"};
            _rerouteLayer.AddToClassList("graph-reroutes");
            _rerouteLayer.pickingMode = PickingMode.Ignore;
            var nodeIndex = _nodesLayer != null ? _content.IndexOf(_nodesLayer) : -1;
            if (nodeIndex >= 0) _content.Insert(nodeIndex, _rerouteLayer);
            else _content.Add(_rerouteLayer);

            var shared = NodeEditorPreferences.GetShared();
            _zoomSlider = new Slider(shared.MinZoom, shared.MaxZoom) {value = window.Zoom};
            _zoomSlider.AddToClassList("toolbar-slider");
            _zoomLabel = new Label();
            _zoomLabel.AddToClassList("toolbar-label");
            _zoomLabel.AddToClassList("toolbar-zoom");
            _searchField = new TextField();
            _searchField.AddToClassList("toolbar-search");
            _variableFilter = new DropdownField();
            _variableFilter.AddToClassList("toolbar-filter");
            _graphToolbarSlot = ToolbarGroup();
            _graphToolbarSlot.AddToClassList("toolbar-group-extra");

            BuildToolbar();
            _root.focusable = true;
            RegisterViewport();
            RegisterBlackboardResize();
            _viewport.AddManipulator(new ContextualMenuManipulator(OnViewportContext));
            _nodesLayer.AddManipulator(new ContextualMenuManipulator(OnViewportContext));
            _groupsLayer?.AddManipulator(new ContextualMenuManipulator(OnViewportContext));
            Undo.undoRedoPerformed += Rebuild;
            Selection.selectionChanged += RefreshSelection;
            NodeEditorPreferences.Changed += ApplyVisualPrefs;
            ApplyView();
            Rebuild();
            EditorApplication.update += RefreshRuntime;
        }

        private void RefreshRuntime()
        {
            if (Window.Graph == null) return;
            if (!EditorApplication.isPlaying)
            {
                if (_runtimeVisualsCleared) return;
                _runtimeVisualsCleared = true;
            }
            else
            {
                _runtimeVisualsCleared = false;
            }

            foreach (var pair in Nodes)
            {
                var editor = NodeEditor.GetEditor(pair.Key, Window);
                editor.BuildHeader(pair.Value);
            }

            _noodles.MarkDirtyRepaint();
            Window.Repaint();
        }

        public void Dispose()
        {
            EditorApplication.update -= RefreshRuntime;
            Undo.undoRedoPerformed -= Rebuild;
            Selection.selectionChanged -= RefreshSelection;
            NodeEditorPreferences.Changed -= ApplyVisualPrefs;
        }

        public void Rebuild()
        {
            if (Window.Graph == null) return;
            Window.ValidateGraphEditor();
            Window.GraphEditor?.ApplyStyles(_root);
            RebuildToolbarExtras();
            RebuildBlackboard();
            RebuildOverlay();
            RebuildNodes();
            RebuildRerouteHandles();
            RefreshSelection();
            ApplyVisualPrefs();
        }

        public void RefreshNodes()
        {
            if (Window.Graph == null) return;
            RebuildNodes();
            RefreshSelection();
        }

        private void BuildToolbar()
        {
            _toolbar.Clear();

            var zoomGroup = ToolbarGroup();
            zoomGroup.Add(ToolbarLabel("Scale"));
            zoomGroup.Add(_zoomSlider);
            zoomGroup.Add(_zoomLabel);
            _toolbar.Add(zoomGroup);
            _zoomSlider.RegisterValueChangedCallback(evt =>
            {
                Window.Zoom = evt.newValue;
                ApplyView();
            });

            var frameGroup = ToolbarGroup();
            frameGroup.Add(ToolbarButton(FrameSelection, "Frame"));
            frameGroup.Add(ToolbarButton(FrameAll, "All"));
            frameGroup.Add(ToolbarButton(() => EditorGUIUtility.PingObject(Window.Graph), "Ping"));
            _toolbar.Add(frameGroup);

            var viewGroup = ToolbarGroup();
            _snapButton = new Button(ToggleGridSnap) {text = "Snap"};
            _snapButton.tooltip = "Snap nodes to the grid while dragging. Ctrl inverts.";
            _snapButton.AddToClassList("toolbar-toggle");
            _snapButton.EnableInClassList("active", NodeEditorPreferences.GetSettings().GridSnap);
            viewGroup.Add(_snapButton);
            _indexButton = new Button(ToggleNodeIndices) {text = "Idx"};
            _indexButton.tooltip = "Show compiled plan indices on node headers.";
            _indexButton.AddToClassList("toolbar-toggle");
            _indexButton.EnableInClassList("active", NodeEditorPreferences.GetSettings().ShowNodeIndices);
            viewGroup.Add(_indexButton);
            _toolbar.Add(viewGroup);

            var searchGroup = ToolbarGroup();
            searchGroup.Add(ToolbarLabel("Node"));
            _searchField.value = Window.NodeSearch;
            _searchField.RegisterValueChangedCallback(evt =>
            {
                Window.NodeSearch = evt.newValue;
                RefreshSelection();
                Window.RefreshHighlightHasMatches();
            });
            _searchField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                Window.FrameNodeSearchResult(evt.shiftKey ? -1 : 1);
                ApplyView();
            });
            searchGroup.Add(_searchField);
            _toolbar.Add(searchGroup);

            var usesGroup = ToolbarGroup();
            usesGroup.Add(ToolbarLabel("Uses"));
            usesGroup.Add(_variableFilter);
            _toolbar.Add(usesGroup);
            _variableFilter.RegisterValueChangedCallback(evt =>
            {
                var index = _variableFilter.choices != null
                    ? _variableFilter.choices.IndexOf(evt.newValue)
                    : -1;
                Window.HighlightedVariableId = index > 0 && index <= _variableIds.Count
                    ? _variableIds[index - 1]
                    : "";
                RefreshSelection();
            });
            _toolbar.Add(_graphToolbarSlot);
            var spacer = new VisualElement();
            spacer.AddToClassList("toolbar-spacer");
            _toolbar.Add(spacer);
            _blackboardButton = new Button(ToggleBlackboard) {text = "Blackboard"};
            _blackboardButton.AddToClassList("bb-toolbar-toggle");
            _blackboardButton.EnableInClassList("active", Window.ShowBlackboard);
            _toolbar.Add(_blackboardButton);
            _blackboard.EnableInClassList("hidden", !Window.ShowBlackboard);
            ApplyBlackboardVisibility();
            _cursorLabel = new Label();
            _cursorLabel.AddToClassList("graph-cursor");
            _cursorLabel.pickingMode = PickingMode.Ignore;
            _viewport.Add(_cursorLabel);
            UpdateCursorLabel(Vector2.zero);
        }

        private void ToggleGridSnap()
        {
            var next = !NodeEditorPreferences.GetSettings().GridSnap;
            NodeEditorPreferences.SetGridSnap(next);
            _snapButton?.EnableInClassList("active", next);
        }

        private void ToggleNodeIndices()
        {
            var next = !NodeEditorPreferences.GetSettings().ShowNodeIndices;
            NodeEditorPreferences.SetShowNodeIndices(next);
            _indexButton?.EnableInClassList("active", next);
            RefreshNodeIndices();
        }

        private void RefreshNodeIndices()
        {
            var show = NodeEditorPreferences.GetSettings().ShowNodeIndices;
            foreach (var pair in Nodes)
                ApplyNodeIndex(pair.Value, show);
        }

        private void ApplyNodeIndex(NodeView view, bool show)
        {
            if (view == null) return;
            if (!show || Window.GraphEditor == null ||
                !Window.GraphEditor.TryGetNodeIndex(view.Node, out var index))
            {
                view.SetIndex(null);
                return;
            }

            view.SetIndex(index);
        }

        private void ToggleBlackboard()
        {
            Window.ShowBlackboard = !Window.ShowBlackboard;
            _blackboardButton?.EnableInClassList("active", Window.ShowBlackboard);
            ApplyBlackboardVisibility();
        }

        private void ApplyBlackboardVisibility()
        {
            var hidden = !Window.ShowBlackboard;
            _blackboard.EnableInClassList("hidden", hidden);
            _blackboardResize?.EnableInClassList("hidden", hidden);
        }

        private void RebuildToolbarExtras()
        {
            _graphToolbarSlot.Clear();
            Window.GraphEditor?.BuildToolbar(_graphToolbarSlot);
            _graphToolbarSlot.EnableInClassList("toolbar-group-empty", _graphToolbarSlot.childCount == 0);
            RebuildVariableFilter();
        }

        private void RebuildVariableFilter()
        {
            var choices = new List<string> {"(None)"};
            _variableIds.Clear();
            if (Window.Graph != null)
            {
                var variables = Window.Graph.BlackboardDefinition.Variables;
                for (var i = 0; i < variables.Count; i++)
                {
                    if (variables[i] == null) continue;
                    choices.Add(variables[i].Name);
                    _variableIds.Add(variables[i].Id);
                }
            }

            _variableFilter.choices = choices;
            var selected = Mathf.Max(0, _variableIds.IndexOf(Window.HighlightedVariableId) + 1);
            _variableFilter.SetValueWithoutNotify(choices[Mathf.Clamp(selected, 0, choices.Count - 1)]);
        }

        private void RebuildBlackboard()
        {
            _blackboard.Clear();
            if (Window.Graph == null) return;
            ApplyBlackboardWidth();
            _blackboard.Add(BlackboardEditorPanel.Build(Window));
        }

        private void ApplyBlackboardWidth()
        {
            if (_blackboard == null) return;
            _blackboard.style.width = Window.BlackboardWidth;
        }

        private void RegisterBlackboardResize()
        {
            if (_blackboardResize == null) return;
            _blackboardResize.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _resizingBlackboard = true;
                _blackboardResize.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            _blackboardResize.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_resizingBlackboard || !_blackboardResize.HasPointerCapture(evt.pointerId)) return;
                var localX = _root.WorldToLocal(evt.position).x;
                Window.BlackboardWidth = _root.layout.width - localX;
                ApplyBlackboardWidth();
                evt.StopPropagation();
            });
            _blackboardResize.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!_blackboardResize.HasPointerCapture(evt.pointerId)) return;
                _blackboardResize.ReleasePointer(evt.pointerId);
                _resizingBlackboard = false;
                evt.StopPropagation();
            });
        }

        private void RebuildOverlay()
        {
            _overlay.Clear();
            Window.GraphEditor?.BuildOverlay(_overlay);
        }

        private void RebuildNodes()
        {
            _nodesLayer.Clear();
            _groupsLayer?.Clear();
            Nodes.Clear();
            Ports.Clear();
            if (Window.Graph?.Nodes == null) return;
            for (var i = 0; i < Window.Graph.Nodes.Count; i++)
            {
                var node = Window.Graph.Nodes[i];
                if (node == null) continue;
                var view = node is GroupNode group
                    ? new GroupView(group)
                    : new NodeView(node);
                var editor = NodeEditor.GetEditor(node, Window);
                view.style.width = editor.GetWidth();
                editor.BuildHeader(view);
                editor.BuildBody(view);
                ApplyNodeIndex(view, NodeEditorPreferences.GetSettings().ShowNodeIndices);
                foreach (var pair in view.Ports)
                {
                    Ports[pair.Key] = pair.Value;
                    pair.Value.ApplyColor(Window.GraphEditor.GetPortColor(pair.Key));
                    pair.Value.ApplyTooltip(PortTooltip(pair.Key));
                    BindPort(pair.Value);
                }

                BindNode(view);
                view.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    Window.NodeSizes[node] = new Vector2(
                        Mathf.Max(view.resolvedStyle.width, editor.GetWidth()),
                        Mathf.Max(view.resolvedStyle.height, 40f));
                    _noodles.MarkDirtyRepaint();
                });
                if (node is GroupNode && _groupsLayer != null) _groupsLayer.Add(view);
                else _nodesLayer.Add(view);
                Nodes[node] = view;
                Window.NodeSizes[node] = new Vector2(editor.GetWidth(), 80f);
            }

            RebuildRerouteHandles();
            CullNodes();
            SyncGroupEdges();
        }

        private void BindNode(NodeView view)
        {
            view.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                if (BlocksNodeDrag(evt.target)) return;
                var additive = evt.ctrlKey || evt.commandKey || evt.shiftKey;
                var already = Selection.Contains(view.Node);
                if (additive)
                {
                    if (already) Window.DeselectNode(view.Node);
                    else Window.SelectNode(view.Node, true);
                    _dragCollapseOnClick = false;
                }
                else if (!already)
                {
                    Window.SelectNode(view.Node, false);
                    _dragCollapseOnClick = false;
                }
                else
                {
                    _dragCollapseOnClick = Selection.objects.Length > 1;
                }

                _dragClickedNode = view.Node;
                BeginNodeDrag(evt.position);
                view.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            view.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_draggingNodes || !view.HasPointerCapture(evt.pointerId)) return;
                DragNodes(evt.position, evt.ctrlKey || evt.commandKey);
            });
            view.RegisterCallback<PointerUpEvent>(evt =>
            {
                var moved = ((Vector2) evt.position - _dragPointerStart).sqrMagnitude > 16f;
                if (view.HasPointerCapture(evt.pointerId))
                    view.ReleasePointer(evt.pointerId);
                var clicked = _dragClickedNode;
                var collapse = _dragCollapseOnClick && !moved && clicked != null;
                EndNodeDrag();
                if (collapse) Window.SelectNode(clicked, false);
            });
        }

        private static bool BlocksNodeDrag(IEventHandler target)
        {
            for (var ve = target as VisualElement; ve != null; ve = ve.parent)
            {
                if (ve is PortView) return true;
                if (ve is Button) return true;
                if (ve is IMGUIContainer) return true;
                if (ve.ClassListContains("unity-base-field")) return true;
                if (ve.ClassListContains("unity-button")) return true;
                if (ve.ClassListContains("unity-base-slider")) return true;
                if (ve.ClassListContains("group-resize")) return true;
            }

            return false;
        }

        private void BindPort(PortView portView)
        {
            portView.RegisterCallback<PointerEnterEvent>(_ =>
            {
                Window.HoveredPort = portView.Port;
                portView.SetHovered(true);
                _noodles.MarkDirtyRepaint();
            });
            portView.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (Window.HoveredPort == portView.Port) Window.HoveredPort = null;
                portView.SetHovered(false);
                _noodles.MarkDirtyRepaint();
            });
            portView.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                BeginPortDrag(portView.Port, evt.position);
                _viewport.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
        }

        private void BeginPortDrag(NodePort port, Vector2 panelPosition)
        {
            _dragPointer = panelPosition;
            _autoConnectPort = null;
            if (port.IsOutput)
            {
                _dragPort = port;
                _autoConnectPort = port;
                Window.DraggedOutput = port;
                Window.DraggedInput = null;
                return;
            }

            if (port.IsConnected)
            {
                var output = port.Connection;
                Undo.RecordObject(port.Node, "Disconnect");
                Undo.RecordObject(output.Node, "Disconnect");
                output.Disconnect(port);
                EditorUtility.SetDirty(port.Node);
                EditorUtility.SetDirty(output.Node);
                _dragPort = output;
                _autoConnectPort = output;
                Window.DraggedOutput = output;
                Window.DraggedInput = null;
                _noodles.MarkDirtyRepaint();
                return;
            }

            _dragPort = port;
            Window.DraggedOutput = null;
            Window.DraggedInput = port;
        }

        private void FinishPortDrag(Vector2 panelPosition)
        {
            var from = _dragPort;
            var autoConnect = _autoConnectPort;
            _dragPort = null;
            _autoConnectPort = null;
            Window.DraggedOutput = null;
            Window.DraggedInput = null;
            if (from == null)
            {
                _noodles.MarkDirtyRepaint();
                return;
            }

            var hit = FindPortAt(panelPosition, from);
            var target = hit?.Port;
            if (target == null)
            {
                var nodeHit = FindNodeAt(panelPosition);
                if (nodeHit != null)
                {
                    target = from.IsOutput
                        ? Window.GraphEditor.GetCompatibleInput(from, nodeHit.Node)
                        : Window.GraphEditor.GetCompatibleOutput(from, nodeHit.Node);
                }
            }

            if (target != null && target != from)
            {
                var output = from.IsOutput ? from : target;
                var input = from.IsOutput ? target : from;
                if (Window.GraphEditor.CanConnect(output, input) && output != input)
                {
                    Undo.RecordObject(output.Node, "Connect");
                    Undo.RecordObject(input.Node, "Connect");
                    output.Connect(input);
                    EditorUtility.SetDirty(output.Node);
                    EditorUtility.SetDirty(input.Node);
                    EditorUtility.SetDirty(Window.Graph);
                    Rebuild();
                    return;
                }
            }

            var idleOutput = autoConnect != null && autoConnect.IsOutput && !autoConnect.IsConnected;
            NodeView ownView = null;
            var onOwnNode = idleOutput &&
                            autoConnect.Node != null &&
                            Nodes.TryGetValue(autoConnect.Node, out ownView) &&
                            ownView.worldBound.Contains(panelPosition);
            var empty = FindNodeAt(panelPosition) == null;
            var showCreate = idleOutput &&
                             (onOwnNode || (empty && NodeEditorPreferences.GetSettings().DragToCreate));
            var menuPos = panelPosition;
            if (onOwnNode && ownView != null)
            {
                var bound = ownView.worldBound;
                menuPos = new Vector2(bound.center.x, bound.yMax + 8f);
            }

            Rebuild();
            if (showCreate)
                ShowCreateMenu(menuPos, autoConnect);
        }

        private PortView FindPortAt(Vector2 panelPosition, NodePort ignore = null)
        {
            PortView best = null;
            var bestDist = 22f * 22f;
            foreach (var pair in Ports)
            {
                if (pair.Value == null || pair.Key == ignore) continue;
                var center = pair.Value.HasValidLayout
                    ? pair.Value.worldBound.center
                    : pair.Value.NodeView.LocalToWorld(pair.Value.LocalOffsetOnNode);
                var dist = (center - panelPosition).sqrMagnitude;
                if (dist >= bestDist) continue;
                bestDist = dist;
                best = pair.Value;
            }

            return best;
        }

        private NodeView FindNodeAt(Vector2 panelPosition)
        {
            NodeView groupHit = null;
            foreach (var pair in Nodes)
            {
                if (pair.Value == null || !pair.Value.worldBound.Contains(panelPosition)) continue;
                if (pair.Key is GroupNode)
                {
                    if (groupHit == null) groupHit = pair.Value;
                    continue;
                }

                return pair.Value;
            }

            return groupHit;
        }

        private void RegisterViewport()
        {
            _viewport.RegisterCallback<WheelEvent>(evt =>
            {
                var local = _viewport.WorldToLocal(evt.mousePosition);
                var grid = ScreenToGrid(evt.mousePosition);
                var factor = evt.delta.y > 0 ? 1.1f : 0.9f;
                var shared = NodeEditorPreferences.GetShared();
                Window.Zoom = Mathf.Clamp(
                    Window.Zoom * factor,
                    shared.MinZoom,
                    shared.MaxZoom);
                if (shared.ZoomToMouse)
                {
                    var s = 1f / Window.Zoom;
                    Window.PanOffset = local - grid * s;
                }

                ApplyView();
                evt.StopPropagation();
            });
            _viewport.RegisterCallback<PointerDownEvent>(evt =>
            {
                _root.Focus();
                if (evt.button == 2 || (evt.button == 0 && evt.altKey && FindNodeAt(evt.position) == null &&
                                        FindPortAt(evt.position) == null))
                {
                    if (evt.altKey && evt.button == 0)
                    {
                        if (TryInsertReroute(ScreenToGrid(evt.position)))
                        {
                            evt.StopPropagation();
                            return;
                        }
                    }

                    _panning = true;
                    _viewport.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                    return;
                }

                if (evt.button == 0 && FindNodeAt(evt.position) == null && FindPortAt(evt.position) == null)
                {
                    _boxing = true;
                    _boxStart = _viewport.WorldToLocal(evt.position);
                    _viewport.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                }
            });
            _viewport.RegisterCallback<PointerMoveEvent>(evt =>
            {
                UpdateCursorLabel(evt.position);
                if (_panning && _viewport.HasPointerCapture(evt.pointerId))
                {
                    Window.PanOffset += (Vector2) evt.deltaPosition;
                    ApplyView();
                }

                if (_boxing && _viewport.HasPointerCapture(evt.pointerId))
                {
                    var now = _viewport.WorldToLocal(evt.position);
                    var box = Rect.MinMaxRect(
                        Mathf.Min(_boxStart.x, now.x),
                        Mathf.Min(_boxStart.y, now.y),
                        Mathf.Max(_boxStart.x, now.x),
                        Mathf.Max(_boxStart.y, now.y));
                    _selectionBox.style.display = DisplayStyle.Flex;
                    _selectionBox.style.left = box.x;
                    _selectionBox.style.top = box.y;
                    _selectionBox.style.width = box.width;
                    _selectionBox.style.height = box.height;
                }

                if (_dragPort != null)
                {
                    _dragPointer = evt.position;
                    _noodles.MarkDirtyRepaint();
                }
            });
            _viewport.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_viewport.HasPointerCapture(evt.pointerId))
                    _viewport.ReleasePointer(evt.pointerId);
                if (_dragPort != null)
                {
                    FinishPortDrag(evt.position);
                    _panning = false;
                    _boxing = false;
                    return;
                }

                if (_boxing)
                {
                    var now = _viewport.WorldToLocal(evt.position);
                    var box = Rect.MinMaxRect(
                        Mathf.Min(_boxStart.x, now.x),
                        Mathf.Min(_boxStart.y, now.y),
                        Mathf.Max(_boxStart.x, now.x),
                        Mathf.Max(_boxStart.y, now.y));
                    SelectNodesInBox(box);
                    _selectionBox.style.display = DisplayStyle.None;
                }

                _panning = false;
                _boxing = false;
            });
            _root.RegisterCallback<KeyDownEvent>(OnGraphKeyDown, TrickleDown.TrickleDown);
            _viewport.RegisterCallback<PointerMoveEvent>(
                evt => UpdateCursorLabel(evt.position),
                TrickleDown.TrickleDown);
            _viewport.RegisterCallback<GeometryChangedEvent>(_ => CullNodes());
        }

        private void OnGraphKeyDown(KeyDownEvent evt)
        {
            if (IsEditingText(evt.target) ||
                IsEditingText(_root.focusController?.focusedElement))
            {
                return;
            }

            if (evt.keyCode == KeyCode.F)
            {
                FrameSelection();
                evt.StopPropagation();
            }

            if (evt.keyCode == KeyCode.F2 ||
                (NodeEditorUtilities.IsMac() && evt.keyCode == KeyCode.Return))
            {
                Window.RenameSelectedNode();
                evt.StopPropagation();
            }

            if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
            {
                Window.RemoveSelectedNodes();
                Rebuild();
                evt.StopPropagation();
            }

            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.C)
            {
                Window.CopySelectedNodes();
                evt.StopPropagation();
            }

            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.V)
            {
                Window.PasteNodes(ScreenToGrid(_viewport.worldBound.center));
                Rebuild();
                evt.StopPropagation();
            }

            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.D)
            {
                Window.DuplicateSelectedNodes();
                Rebuild();
                evt.StopPropagation();
            }

            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.A)
            {
                if (Selection.objects.Any(x => Window.Graph.Nodes.Contains(x as Node)))
                {
                    Selection.objects = Array.Empty<Object>();
                }
                else
                {
                    Selection.objects = Window.Graph.Nodes.Where(n => n != null).ToArray();
                }

                RefreshSelection();
                evt.StopPropagation();
            }
        }

        private static bool IsEditingText(IEventHandler target)
        {
            for (var ve = target as VisualElement; ve != null; ve = ve.parent)
            {
                if (ve.ClassListContains("unity-base-text-field")) return true;
                if (ve.ClassListContains("unity-base-text-field__input")) return true;
            }

            return false;
        }

        private void SelectNodesInBox(Rect viewportBox)
        {
            var selected = new List<Object>();
            foreach (var pair in Nodes)
            {
                var world = pair.Value.worldBound;
                var local = _viewport.WorldToLocal(world.position);
                var nodeBox = new Rect(local, world.size);
                if (nodeBox.Overlaps(viewportBox)) selected.Add(pair.Key);
            }

            Selection.objects = selected.ToArray();
        }

        private Vector2 NodeSize(Node node)
        {
            if (node != null &&
                Window.NodeSizes.TryGetValue(node, out var size) &&
                size.x > 1f &&
                size.y > 1f)
            {
                return size;
            }

            var editor = NodeEditor.GetEditor(node, Window);
            var width = editor != null ? editor.GetWidth() : 208f;
            return new Vector2(width, 80f);
        }

        private void RememberDragOrigin(Node node)
        {
            if (node == null || node.Graph != Window.Graph) return;
            if (_dragOrigins.ContainsKey(node)) return;
            _dragOrigins[node] = node.Position;
        }

        private void BeginNodeDrag(Vector2 pointer)
        {
            _draggingNodes = true;
            _dragPointerStart = pointer;
            _dragOrigins.Clear();
            foreach (var obj in Selection.objects)
            {
                if (obj is Node node && node != null && node.Graph == Window.Graph)
                    RememberDragOrigin(node);
            }

            if (_dragClickedNode is GroupNode)
            {
                foreach (var obj in Selection.objects)
                {
                    if (obj is not GroupNode group) continue;
                    var inside = group.GetNodes(NodeSize);
                    for (var i = 0; i < inside.Count; i++)
                        RememberDragOrigin(inside[i]);
                }
            }
        }

        private void DragNodes(Vector2 pointer, bool invertSnap)
        {
            var delta = (pointer - _dragPointerStart) * Window.Zoom;
            var snap = NodeEditorPreferences.GetSettings().GridSnap;
            if (invertSnap) snap = !snap;
            foreach (var pair in _dragOrigins)
            {
                var next = pair.Value + delta;
                if (snap) next = GridElement.Snap(next, SnapStep());
                Window.SetNodePosition(pair.Key, next);
                if (Nodes.TryGetValue(pair.Key, out var view)) view.SyncPosition();
            }

            _noodles.MarkDirtyRepaint();
            UpdateCursorLabel(pointer);
            SyncGroupEdges();
        }

        private void EndNodeDrag()
        {
            if (_draggingNodes)
            {
                var moved = new List<Object>();
                var finals = new List<Vector2>();
                foreach (var pair in _dragOrigins)
                {
                    if (pair.Key == null || pair.Key.Position == pair.Value) continue;
                    moved.Add(pair.Key);
                    finals.Add(pair.Key.Position);
                    pair.Key.Position = pair.Value;
                }

                if (moved.Count > 0)
                {
                    var group = Undo.GetCurrentGroup();
                    Undo.RecordObjects(moved.ToArray(), "Moved Node");
                    for (var i = 0; i < moved.Count; i++)
                    {
                        ((Node) moved[i]).Position = finals[i];
                        EditorUtility.SetDirty(moved[i]);
                    }

                    Window.GraphEditor?.OnNodesMoved();
                    Undo.CollapseUndoOperations(group);
                }
                else
                {
                    Window.GraphEditor?.OnNodesMoved();
                }
            }

            _draggingNodes = false;
            _dragCollapseOnClick = false;
            _dragClickedNode = null;
            _dragOrigins.Clear();
        }

        private void RefreshSelection()
        {
            Window.RefreshHighlightHasMatches();
            var selected = new HashSet<Object>(Selection.objects);
            foreach (var pair in Nodes)
            {
                pair.Value.SetSelected(selected.Contains(pair.Key));
                pair.Value.SetDimmed(Window.IsHighlightFilterActive && !Window.IsNodeHighlighted(pair.Key));
            }

            SyncGroupEdges();
        }

        private void CullNodes()
        {
            var compact = !Window.ShouldDrawNodeFields;
            foreach (var pair in Nodes)
                pair.Value.EnableInClassList("compact", compact);
        }

        private const float GroupEdgeThickness = 8f;
        private const float GroupCornerSize = 14f;

        private void SyncGroupEdges()
        {
            if (_groupEdges == null) return;
            if (_resizeGroup != null)
            {
                PlaceGroupEdges();
                return;
            }

            _groupEdges.Clear();
            foreach (var obj in Selection.objects)
            {
                if (obj is GroupNode group && group != null && group.Graph == Window.Graph)
                    BuildGroupEdges(group);
            }
        }

        private void BuildGroupEdges(GroupNode group)
        {
            AddGroupEdge(group, GroupEdge.N);
            AddGroupEdge(group, GroupEdge.S);
            AddGroupEdge(group, GroupEdge.E);
            AddGroupEdge(group, GroupEdge.W);
            AddGroupEdge(group, GroupEdge.NE);
            AddGroupEdge(group, GroupEdge.NW);
            AddGroupEdge(group, GroupEdge.SE);
            AddGroupEdge(group, GroupEdge.SW);
            PlaceGroupEdges();
        }

        private void AddGroupEdge(GroupNode group, GroupEdge edge)
        {
            var handle = new VisualElement();
            handle.AddToClassList("group-edge");
            handle.AddToClassList(EdgeClass(edge));
            handle.pickingMode = PickingMode.Position;
            handle.userData = new GroupEdgeTag {Group = group, Edge = edge};
            handle.RegisterCallback<PointerDownEvent>(evt => BeginGroupResize(handle, evt));
            handle.RegisterCallback<PointerMoveEvent>(evt => DragGroupResize(handle, evt));
            handle.RegisterCallback<PointerUpEvent>(evt => EndGroupResize(handle, evt));
            _groupEdges.Add(handle);
        }

        private static string EdgeClass(GroupEdge edge)
        {
            switch (edge)
            {
                case GroupEdge.N: return "group-edge-n";
                case GroupEdge.S: return "group-edge-s";
                case GroupEdge.E: return "group-edge-e";
                case GroupEdge.W: return "group-edge-w";
                case GroupEdge.NE: return "group-edge-ne";
                case GroupEdge.NW: return "group-edge-nw";
                case GroupEdge.SE: return "group-edge-se";
                default: return "group-edge-sw";
            }
        }

        private void PlaceGroupEdges()
        {
            foreach (var handle in _groupEdges.Children())
            {
                if (handle.userData is not GroupEdgeTag tag || tag.Group == null) continue;
                var x = tag.Group.Position.x;
                var y = tag.Group.Position.y;
                float w = tag.Group.Width;
                var h = GroupView.HeaderHeight + tag.Group.Height;
                var t = GroupEdgeThickness;
                var c = GroupCornerSize;
                switch (tag.Edge)
                {
                    case GroupEdge.N:
                        SetEdge(handle, x + c, y, Mathf.Max(0f, w - c * 2f), t);
                        break;
                    case GroupEdge.S:
                        SetEdge(handle, x + c, y + h - t, Mathf.Max(0f, w - c * 2f), t);
                        break;
                    case GroupEdge.W:
                        SetEdge(handle, x, y + c, t, Mathf.Max(0f, h - c * 2f));
                        break;
                    case GroupEdge.E:
                        SetEdge(handle, x + w - t, y + c, t, Mathf.Max(0f, h - c * 2f));
                        break;
                    case GroupEdge.NW:
                        SetEdge(handle, x, y, c, c);
                        break;
                    case GroupEdge.NE:
                        SetEdge(handle, x + w - c, y, c, c);
                        break;
                    case GroupEdge.SW:
                        SetEdge(handle, x, y + h - c, c, c);
                        break;
                    default:
                        SetEdge(handle, x + w - c, y + h - c, c, c);
                        break;
                }
            }
        }

        private static void SetEdge(VisualElement handle, float x, float y, float w, float h)
        {
            handle.style.left = x;
            handle.style.top = y;
            handle.style.width = w;
            handle.style.height = h;
        }

        private void BeginGroupResize(VisualElement handle, PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            if (handle.userData is not GroupEdgeTag tag || tag.Group == null) return;
            _resizeGroup = tag.Group;
            _resizeEdge = tag.Edge;
            _resizePointer = ScreenToGrid(evt.position);
            _resizeOrigin = tag.Group.Position;
            _resizeWidth = tag.Group.Width;
            _resizeHeight = tag.Group.Height;
            Undo.RecordObject(tag.Group, "Resize Group");
            handle.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void DragGroupResize(VisualElement handle, PointerMoveEvent evt)
        {
            if (_resizeGroup == null || !handle.HasPointerCapture(evt.pointerId)) return;
            var delta = ScreenToGrid(evt.position) - _resizePointer;
            var west = _resizeEdge == GroupEdge.W || _resizeEdge == GroupEdge.NW || _resizeEdge == GroupEdge.SW;
            var east = _resizeEdge == GroupEdge.E || _resizeEdge == GroupEdge.NE || _resizeEdge == GroupEdge.SE;
            var north = _resizeEdge == GroupEdge.N || _resizeEdge == GroupEdge.NW || _resizeEdge == GroupEdge.NE;
            var south = _resizeEdge == GroupEdge.S || _resizeEdge == GroupEdge.SW || _resizeEdge == GroupEdge.SE;
            var right = _resizeOrigin.x + _resizeWidth;
            var bottom = _resizeOrigin.y + _resizeHeight;
            var x = _resizeOrigin.x;
            var y = _resizeOrigin.y;
            float w = _resizeWidth;
            float h = _resizeHeight;
            if (east) w = _resizeWidth + delta.x;
            if (west)
            {
                x = _resizeOrigin.x + delta.x;
                w = right - x;
            }

            if (south) h = _resizeHeight + delta.y;
            if (north)
            {
                y = _resizeOrigin.y + delta.y;
                h = bottom - y;
            }

            if (w < GroupView.MinWidth)
            {
                w = GroupView.MinWidth;
                if (west) x = right - w;
            }

            if (h < GroupView.MinHeight)
            {
                h = GroupView.MinHeight;
                if (north) y = bottom - h;
            }

            _resizeGroup.Position = new Vector2(Mathf.Round(x), Mathf.Round(y));
            _resizeGroup.Width = Mathf.RoundToInt(w);
            _resizeGroup.Height = Mathf.RoundToInt(h);
            if (Nodes.TryGetValue(_resizeGroup, out var view))
            {
                view.SyncPosition();
                if (view is GroupView groupView) groupView.ApplyGroupSize();
                Window.NodeSizes[_resizeGroup] =
                    new Vector2(_resizeGroup.Width, GroupView.HeaderHeight + _resizeGroup.Height);
            }

            PlaceGroupEdges();
            evt.StopPropagation();
        }

        private void EndGroupResize(VisualElement handle, PointerUpEvent evt)
        {
            if (handle.HasPointerCapture(evt.pointerId))
                handle.ReleasePointer(evt.pointerId);
            if (_resizeGroup != null) EditorUtility.SetDirty(_resizeGroup);
            _resizeGroup = null;
            evt.StopPropagation();
        }

        private void OnViewportContext(ContextualMenuPopulateEvent evt)
        {
            evt.menu.ClearItems();
            var panelPos = evt.mousePosition;
            if (evt.currentTarget is VisualElement current)
                panelPos = current.LocalToWorld(evt.localMousePosition);
            var port = FindPortAt(panelPos);
            if (port != null)
            {
                FillPortMenu(evt.menu, port.Port);
                evt.StopPropagation();
                return;
            }

            var node = FindNodeAt(panelPos);
            if (node != null)
            {
                FillNodeMenu(evt.menu, node);
                evt.StopPropagation();
                return;
            }

            FillCreateMenu(evt.menu, panelPos);
            evt.StopPropagation();
        }

        private void FillCreateMenu(DropdownMenu menu, Vector2 panelPos)
        {
            if (Window.GraphEditor == null) return;
            var grid = ScreenToGrid(panelPos);
            SplitCreateEntries(GetCreateEntries(null), out var groups, out var rest);
            foreach (var entry in groups)
            {
                var captured = entry.type;
                menu.AppendAction(entry.path, _ =>
                {
                    Window.GraphEditor.CreateNode(captured, grid);
                    Rebuild();
                });
            }

            if (groups.Count > 0 && rest.Count > 0) menu.AppendSeparator();
            var any = groups.Count > 0;
            foreach (var entry in rest)
            {
                var captured = entry.type;
                menu.AppendAction(entry.path, _ =>
                {
                    Window.GraphEditor.CreateNode(captured, grid);
                    Rebuild();
                });
                any = true;
            }

            if (any) menu.AppendSeparator();
            var canPaste = NodeEditorWindow.CopyBuffer != null && NodeEditorWindow.CopyBuffer.Length > 0;
            menu.AppendAction("Paste", _ =>
            {
                Window.PasteNodes(grid);
                Rebuild();
            }, canPaste ? DropdownMenuAction.AlwaysEnabled : DropdownMenuAction.AlwaysDisabled);
            menu.AppendAction("Preferences", _ => NodeEditorReflection.OpenPreferences());
        }

        private List<(string path, Type type)> GetCreateEntries(Type compatibleType)
        {
            var result = new List<(string path, Type type)>();
            if (Window.GraphEditor == null) return result;
            IEnumerable<Type> types = NodeEditorReflection.NodeTypes;
            if (compatibleType != null && NodeEditorPreferences.GetSettings().CreateFilter)
            {
                types = NodeEditorUtilities.GetCompatibleNodesTypes(
                    NodeEditorReflection.NodeTypes,
                    compatibleType,
                    NodePort.IO.Input);
            }

            foreach (var type in types.OrderBy(t => Window.GraphEditor.GetNodeMenuOrder(t)))
            {
                var path = Window.GraphEditor.GetNodeMenuName(type);
                if (string.IsNullOrEmpty(path)) continue;
                result.Add((path, type));
            }

            return result;
        }

        private static void SplitCreateEntries(
            List<(string path, Type type)> entries,
            out List<(string path, Type type)> groups,
            out List<(string path, Type type)> rest)
        {
            groups = new List<(string path, Type type)>();
            rest = new List<(string path, Type type)>();
            for (var i = 0; i < entries.Count; i++)
            {
                if (typeof(GroupNode).IsAssignableFrom(entries[i].type)) groups.Add(entries[i]);
                else rest.Add(entries[i]);
            }
        }

        private void ShowCreateMenu(Vector2 panelPos, NodePort output)
        {
            var menu = new GenericMenu();
            var grid = ScreenToGrid(panelPos);
            var capturedOutput = output;
            SplitCreateEntries(
                GetCreateEntries(output != null ? output.ValueType : null),
                out var groups,
                out var rest);
            var any = false;

            void AddEntry((string path, Type type) entry)
            {
                var capturedType = entry.type;
                menu.AddItem(new GUIContent(entry.path), false, () =>
                {
                    var node = Window.GraphEditor.CreateNode(capturedType, grid);
                    if (node != null && capturedOutput != null)
                    {
                        var input = Window.GraphEditor.GetCompatibleInput(capturedOutput, node);
                        if (input != null) capturedOutput.Connect(input);
                    }

                    Rebuild();
                });
                any = true;
            }

            for (var i = 0; i < groups.Count; i++) AddEntry(groups[i]);
            if (groups.Count > 0 && rest.Count > 0) menu.AddSeparator("");
            for (var i = 0; i < rest.Count; i++) AddEntry(rest[i]);
            if (!any) return;
            menu.AddSeparator("");
            var canPaste = NodeEditorWindow.CopyBuffer != null && NodeEditorWindow.CopyBuffer.Length > 0;
            if (canPaste)
            {
                menu.AddItem(new GUIContent("Paste"), false, () =>
                {
                    Window.PasteNodes(grid);
                    Rebuild();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Paste"));
            }

            menu.AddItem(new GUIContent("Preferences"), false, () => NodeEditorReflection.OpenPreferences());
            _root.schedule.Execute(() => menu.DropDown(new Rect(panelPos.x, panelPos.y, 0, 0)));
        }

        private void FillNodeMenu(DropdownMenu menu, NodeView view)
        {
            if (!Selection.Contains(view.Node)) Window.SelectNode(view.Node, false);
            menu.AppendAction("Rename", _ => Window.RenameSelectedNode());
            menu.AppendAction("Copy", _ => Window.CopySelectedNodes());
            menu.AppendAction("Duplicate", _ =>
            {
                Window.DuplicateSelectedNodes();
                Rebuild();
            });
            Window.GraphEditor?.AddItemsToNodeMenu(menu, view.Node);
            if (view.Node is GroupNode group)
            {
                var captured = group;
                menu.AppendAction("Select Contents", _ =>
                {
                    var list = new List<Object> {captured};
                    var inside = captured.GetNodes(NodeSize);
                    for (var i = 0; i < inside.Count; i++)
                        if (inside[i] != null)
                            list.Add(inside[i]);
                    Selection.objects = list.ToArray();
                });
            }

            menu.AppendAction("Remove", _ =>
            {
                Window.RemoveSelectedNodes();
                Rebuild();
            });
            menu.AppendSeparator();
            var script = view.Node != null ? MonoScript.FromScriptableObject(view.Node) : null;
            if (script != null)
            {
                var captured = script;
                menu.AppendAction("Edit Script", _ => AssetDatabase.OpenAsset(captured));
            }
            else
            {
                menu.AppendAction("Edit Script", _ => { }, DropdownMenuAction.Status.Disabled);
            }
        }

        private void FillPortMenu(DropdownMenu menu, NodePort port)
        {
            if (port == null) return;
            foreach (var other in port.GetConnections())
            {
                var captured = other;
                menu.AppendAction($"Disconnect ({other.Node.name})", _ =>
                {
                    port.Disconnect(captured);
                    EditorUtility.SetDirty(Window.Graph);
                    Rebuild();
                });
            }

            menu.AppendAction("Clear Connections", _ =>
            {
                port.ClearConnections();
                EditorUtility.SetDirty(Window.Graph);
                Rebuild();
            });
        }

        private void FrameSelection()
        {
            var nodes = Selection.objects.OfType<Node>().Where(n => n != null).ToList();
            if (nodes.Count == 0) FrameAll();
            else FrameNodes(nodes);
        }

        private void FrameAll()
        {
            if (Window.Graph == null) return;
            FrameNodes(Window.Graph.Nodes.Where(n => n != null).ToList());
        }

        private void FrameNodes(List<Node> nodes)
        {
            if (nodes == null || nodes.Count == 0)
            {
                Window.Zoom = 1f;
                Window.PanOffset = Vector2.zero;
                ApplyView();
                return;
            }

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var size = Window.NodeSizes.TryGetValue(node, out var cached)
                    ? cached
                    : new Vector2(208f, 80f);
                min = Vector2.Min(min, node.Position);
                max = Vector2.Max(max, node.Position + size);
            }

            var bounds = Vector2.Max(max - min, new Vector2(80f, 80f));
            var viewSize = _viewport.contentRect.size;
            if (viewSize.x < 8f) viewSize = Window.position.size;
            var padding = 80f;
            var fitScale = Mathf.Min(
                (viewSize.x - padding) / bounds.x,
                (viewSize.y - padding) / bounds.y);
            var zoom = 1f / Mathf.Max(0.05f, fitScale);
            var settings = NodeEditorPreferences.GetShared();
            Window.Zoom = Mathf.Clamp(zoom, settings.MinZoom, settings.MaxZoom);
            var s = 1f / Window.Zoom;
            var center = (min + max) * 0.5f;
            Window.PanOffset = viewSize * 0.5f - center * s;
            ApplyView();
        }

        private void RebuildRerouteHandles()
        {
            _rerouteLayer.Clear();
            if (Window.Graph?.Nodes == null) return;
            foreach (var node in Window.Graph.Nodes)
            {
                if (node == null) continue;
                foreach (var output in node.Outputs)
                {
                    for (var i = 0; i < output.ConnectionCount; i++)
                    {
                        var points = output.GetReroutePoints(i);
                        if (points == null) continue;
                        for (var r = 0; r < points.Count; r++)
                        {
                            var handle = CreateRerouteHandle(output, i, r, points[r]);
                            _rerouteLayer.Add(handle);
                        }
                    }
                }
            }
        }

        private VisualElement CreateRerouteHandle(NodePort port, int connectionIndex, int pointIndex, Vector2 grid)
        {
            var handle = new VisualElement();
            handle.AddToClassList("reroute-handle");
            handle.pickingMode = PickingMode.Position;
            handle.style.left = grid.x - 5f;
            handle.style.top = grid.y - 5f;
            var reference = new RerouteReference(port, connectionIndex, pointIndex);
            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _dragReroute = reference;
                _rerouteDrag = true;
                _rerouteOrigin = reference.GetPoint();
                handle.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_rerouteDrag || !handle.HasPointerCapture(evt.pointerId)) return;
                var next = ScreenToGrid(evt.position);
                var gridSnap = NodeEditorPreferences.GetSettings().GridSnap;
                if (evt.ctrlKey) gridSnap = !gridSnap;
                if (gridSnap) next = GridElement.Snap(next, SnapStep());
                reference.SetPoint(next);
                handle.style.left = next.x - 5f;
                handle.style.top = next.y - 5f;
                _noodles.MarkDirtyRepaint();
            });
            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (handle.HasPointerCapture(evt.pointerId))
                    handle.ReleasePointer(evt.pointerId);
                if (_rerouteDrag) EditorUtility.SetDirty(port.Node);
                _rerouteDrag = false;
            });
            handle.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Remove Reroute"), false, () =>
                {
                    reference.RemovePoint();
                    EditorUtility.SetDirty(port.Node);
                    Rebuild();
                });
                menu.ShowAsContext();
                evt.StopPropagation();
            });
            return handle;
        }

        private bool TryInsertReroute(Vector2 grid)
        {
            const float maxDist = 18f;
            NodePort bestPort = null;
            var bestConnection = -1;
            var bestInsert = -1;
            var bestDist = maxDist;
            foreach (var node in Window.Graph.Nodes)
            {
                if (node == null) continue;
                foreach (var output in node.Outputs)
                {
                    if (!Ports.TryGetValue(output, out var fromView)) continue;
                    for (var i = 0; i < output.ConnectionCount; i++)
                    {
                        var input = output.GetConnection(i);
                        if (input == null || !Ports.TryGetValue(input, out var toView)) continue;
                        var points = new List<Vector2> {fromView.NodeView.Node.Position};
                        points[0] = ScreenToGrid(fromView.worldBound.center);
                        var reroute = output.GetReroutePoints(i);
                        if (reroute != null) points.AddRange(reroute);
                        points.Add(ScreenToGrid(toView.worldBound.center));
                        for (var s = 0; s < points.Count - 1; s++)
                        {
                            var dist = DistanceToSegment(grid, points[s], points[s + 1]);
                            if (dist >= bestDist) continue;
                            bestDist = dist;
                            bestPort = output;
                            bestConnection = i;
                            bestInsert = s;
                        }
                    }
                }
            }

            if (bestPort == null) return false;
            Undo.RecordObject(bestPort.Node, "Add Reroute");
            bestPort.GetReroutePoints(bestConnection).Insert(bestInsert, grid);
            EditorUtility.SetDirty(bestPort.Node);
            RebuildRerouteHandles();
            _noodles.MarkDirtyRepaint();
            return true;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSq = ab.sqrMagnitude;
            if (lengthSq < 0.0001f) return Vector2.Distance(point, a);
            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
            return Vector2.Distance(point, a + ab * t);
        }

        public void ApplyView()
        {
            var s = 1f / Mathf.Max(0.01f, Window.Zoom);
            _content.style.translate = new Translate(Window.PanOffset.x, Window.PanOffset.y);
            _content.style.scale = new Scale(new Vector3(s, s, 1f));
            _zoomSlider.SetValueWithoutNotify(Window.Zoom);
            _zoomLabel.text = Window.Zoom.ToString("0.00", CultureInfo.InvariantCulture) + "x";
            var shared = NodeEditorPreferences.GetShared();
            _snapButton?.EnableInClassList("active", shared.GridSnap);
            _indexButton?.EnableInClassList("active", shared.ShowNodeIndices);
            ApplyGridColors(shared);
            _grid.Pan = Window.PanOffset;
            _grid.Scale = s;
            _grid.MarkDirtyRepaint();
            _noodles.MarkDirtyRepaint();
            CullNodes();
        }

        private void ApplyVisualPrefs()
        {
            if (_grid == null) return;
            var shared = NodeEditorPreferences.GetShared();
            _zoomSlider.lowValue = shared.MinZoom;
            _zoomSlider.highValue = shared.MaxZoom;
            Window.Zoom = Mathf.Clamp(Window.Zoom, shared.MinZoom, shared.MaxZoom);
            Window.GraphEditor?.ApplyStyles(_root);
            Color selection = shared.SelectionColor;
            foreach (var pair in Nodes)
                pair.Value.ApplySelectionColor(selection);
            if (Window.GraphEditor != null)
            {
                foreach (var pair in Ports)
                {
                    pair.Value.ApplyColor(Window.GraphEditor.GetPortColor(pair.Key));
                    pair.Value.ApplyTooltip(PortTooltip(pair.Key));
                }
            }

            RefreshNodeIndices();
            ApplyView();
        }

        private void ApplyGridColors(NodeEditorPreferences.SharedSettings shared)
        {
            if (_grid == null || shared == null) return;
            _grid.BackgroundColor = shared.GridBgColor;
            _grid.MinorColor = shared.GridMinorColor;
            _grid.MajorColor = shared.GridMajorColor;
            _grid.OriginColor = shared.GridOriginColor;
            _grid.MinorStep = shared.GridMinorStep;
            _grid.MajorStep = shared.GridMajorStep;
            _grid.style.backgroundColor = (Color) shared.GridBgColor;
        }

        private string PortTooltip(NodePort port)
        {
            if (port == null || !NodeEditorPreferences.GetShared().PortTooltips) return "";
            return Window.GraphEditor != null ? Window.GraphEditor.GetPortTooltip(port) : "";
        }

        private static float SnapStep()
        {
            return Mathf.Max(1f, NodeEditorPreferences.GetShared().GridMinorStep);
        }

        private void UpdateCursorLabel(Vector2 panelPosition)
        {
            if (_cursorLabel == null) return;
            var grid = ScreenToGrid(panelPosition);
            _cursorLabel.text = $"{Mathf.RoundToInt(grid.x)}, {Mathf.RoundToInt(grid.y)}";
        }

        private static VisualElement ToolbarGroup()
        {
            var group = new VisualElement();
            group.AddToClassList("toolbar-group");
            return group;
        }

        private static Button ToolbarButton(Action action, string text)
        {
            var button = new Button(action) {text = text};
            button.AddToClassList("toolbar-btn");
            return button;
        }

        private static Label ToolbarLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("toolbar-label");
            return label;
        }

        public Vector2 ScreenToGrid(Vector2 panelPosition)
        {
            var local = _viewport.WorldToLocal(panelPosition);
            var s = 1f / Mathf.Max(0.01f, Window.Zoom);
            return (local - Window.PanOffset) / s;
        }

        public void PaintNoodles(MeshGenerationContext ctx)
        {
            if (Window.Graph == null || Window.GraphEditor == null) return;
            var painter = ctx.painter2D;
            foreach (var node in Window.Graph.Nodes)
            {
                if (node == null) continue;
                foreach (var output in node.Outputs)
                {
                    if (!Ports.TryGetValue(output, out var fromView)) continue;
                    for (var i = 0; i < output.ConnectionCount; i++)
                    {
                        var input = output.GetConnection(i);
                        if (input == null || !Ports.TryGetValue(input, out var toView)) continue;
                        var gradient = Window.GraphEditor.GetNoodleGradient(output, input);
                        var thickness = Window.GraphEditor.GetNoodleThickness(output, input);
                        var path = Window.GraphEditor.GetNoodlePath(output, input);
                        var stroke = Window.GraphEditor.GetNoodleStroke(output, input);
                        var start = fromView.CenterIn(_noodles);
                        var end = toView.CenterIn(_noodles);
                        var reroute = output.GetReroutePoints(i);
                        var points = new List<Vector2> {start};
                        if (reroute != null)
                        {
                            for (var r = 0; r < reroute.Count; r++)
                                points.Add(GridToNoodle(reroute[r]));
                        }

                        points.Add(end);
                        DrawPath(
                            painter,
                            points,
                            gradient,
                            thickness,
                            path,
                            stroke,
                            Window.GraphEditor.GetNoodleTension(output, input),
                            Window.GraphEditor.GetNoodlePortDirection(output),
                            Window.GraphEditor.GetNoodlePortDirection(input));
                    }
                }
            }

            if (_dragPort != null && Ports.TryGetValue(_dragPort, out var dragView))
            {
                var start = dragView.CenterIn(_noodles);
                var end = _noodles.WorldToLocal(_dragPointer);
                var color = Window.GraphEditor.GetPortColor(_dragPort);
                color.a = 0.7f;
                var dragGrad = new Gradient();
                dragGrad.SetKeys(
                    new[] {new GradientColorKey(color, 0f)},
                    new[] {new GradientAlphaKey(color.a, 0f)});
                DrawPath(
                    painter,
                    new List<Vector2> {start, end},
                    dragGrad,
                    Window.GraphEditor.GetNoodleThickness(_dragPort, null),
                    Window.GraphEditor.GetNoodlePath(_dragPort, null),
                    NoodleStroke.Full,
                    Window.GraphEditor.GetNoodleTension(_dragPort, null),
                    Window.GraphEditor.GetNoodlePortDirection(_dragPort),
                    -Window.GraphEditor.GetNoodlePortDirection(_dragPort));
            }
        }

        private Vector2 GridToNoodle(Vector2 grid)
        {
            var s = 1f / Mathf.Max(0.01f, Window.Zoom);
            var viewport = grid * s + Window.PanOffset;
            return _noodles.WorldToLocal(_viewport.LocalToWorld(viewport));
        }

        private static void DrawPath(
            Painter2D painter,
            List<Vector2> points,
            Gradient gradient,
            float thickness,
            NoodlePath path,
            NoodleStroke stroke,
            float tension,
            Vector2 startDir,
            Vector2 endDir)
        {
            if (points == null || points.Count < 2) return;
            painter.lineWidth = Mathf.Max(1f, thickness);
            painter.lineCap = LineCap.Round;
            if (stroke == NoodleStroke.Dashed) painter.SetDashPattern(new[] {6f, 4f});
            else painter.SetDashPattern(Array.Empty<float>());
            var pull = Mathf.Clamp(tension, 0.05f, 1f);

            var startColor = gradient != null ? gradient.Evaluate(0f) : Color.white;
            var endColor = gradient != null ? gradient.Evaluate(1f) : startColor;
            var solid = gradient == null || Approximately(startColor, endColor);

            if (solid)
            {
                painter.strokeColor = startColor;
                painter.BeginPath();
                painter.MoveTo(points[0]);
                TracePath(painter, points, path, startDir, endDir, pull);
                painter.Stroke();
                return;
            }

            var samples = new List<Vector2>(32);
            SamplePath(points, path, startDir, endDir, pull, samples);
            if (samples.Count < 2) return;
            var inv = 1f / (samples.Count - 1);
            for (var i = 0; i < samples.Count - 1; i++)
            {
                painter.strokeColor = gradient.Evaluate((i + 0.5f) * inv);
                painter.BeginPath();
                painter.MoveTo(samples[i]);
                painter.LineTo(samples[i + 1]);
                painter.Stroke();
            }
        }

        private static void TracePath(
            Painter2D painter,
            List<Vector2> points,
            NoodlePath path,
            Vector2 startDir,
            Vector2 endDir,
            float tension)
        {
            if (path == NoodlePath.Straight || points.Count > 2)
            {
                for (var i = 1; i < points.Count; i++) painter.LineTo(points[i]);
                return;
            }

            var a = points[0];
            var b = points[1];
            if (path == NoodlePath.Angled)
            {
                var mid = new Vector2((a.x + b.x) * 0.5f, a.y);
                painter.LineTo(mid);
                painter.LineTo(new Vector2(mid.x, b.y));
                painter.LineTo(b);
                return;
            }

            var dist = Vector2.Distance(a, b);
            var c1 = a + startDir.normalized * dist * tension;
            var c2 = b + endDir.normalized * dist * tension;
            painter.BezierCurveTo(c1, c2, b);
        }

        private static void SamplePath(
            List<Vector2> points,
            NoodlePath path,
            Vector2 startDir,
            Vector2 endDir,
            float tension,
            List<Vector2> samples)
        {
            samples.Clear();
            if (path == NoodlePath.Straight || points.Count > 2)
            {
                samples.AddRange(points);
                return;
            }

            var a = points[0];
            var b = points[1];
            if (path == NoodlePath.Angled)
            {
                samples.Add(a);
                samples.Add(new Vector2((a.x + b.x) * 0.5f, a.y));
                samples.Add(new Vector2((a.x + b.x) * 0.5f, b.y));
                samples.Add(b);
                return;
            }

            var dist = Vector2.Distance(a, b);
            var c1 = a + startDir.normalized * dist * tension;
            var c2 = b + endDir.normalized * dist * tension;
            var steps = Mathf.Clamp(Mathf.RoundToInt(dist / 14f), 8, 28);
            for (var i = 0; i <= steps; i++)
                samples.Add(EvaluateBezier(a, c1, c2, b, i / (float) steps));
        }

        private static Vector2 EvaluateBezier(Vector2 a, Vector2 c1, Vector2 c2, Vector2 b, float t)
        {
            var u = 1f - t;
            return (u * u * u * a) + (3f * u * u * t * c1) + (3f * u * t * t * c2) + (t * t * t * b);
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.004f
                   && Mathf.Abs(a.g - b.g) < 0.004f
                   && Mathf.Abs(a.b - b.b) < 0.004f
                   && Mathf.Abs(a.a - b.a) < 0.004f;
        }
    }
}