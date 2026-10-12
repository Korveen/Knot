using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Knot;

namespace Knot.Editor.UI
{
    public static class NodeUiUtility
    {
        private static readonly HashSet<string> DefaultExcludes = new HashSet<string>
        {
            "m_Script", nameof(Node.Graph), nameof(Node.Position), Node.PortsField, Node.NodeIdField
        };

        public static void BindSerializedFields(
            VisualElement parent,
            SerializedObject serializedObject,
            Func<SerializedProperty, bool> include = null,
            params string[] extraExcludes)
        {
            if (parent == null || serializedObject == null) return;
            serializedObject.Update();
            var skip = MakeSkip(extraExcludes);
            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (skip.Contains(iterator.name)) continue;
                if (include != null && !include(iterator)) continue;
                var field = new PropertyField(iterator.Copy());
                field.Bind(serializedObject);
                field.style.minWidth = 0;
                field.AddToClassList("xn-field");
                parent.Add(field);
            }
        }

        public static void BindNodeFields(
            NodeView view,
            SerializedObject serializedObject,
            Func<SerializedProperty, bool> include = null,
            params string[] extraExcludes)
        {
            if (view == null || serializedObject == null || view.Node == null) return;
            serializedObject.Update();
            var skip = MakeSkip(extraExcludes);
            var drawnPorts = new HashSet<string>();
            var node = view.Node;

            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (skip.Contains(iterator.name)) continue;
                if (include != null && !include(iterator)) continue;
                var port = node.GetPort(iterator.name);
                if (port != null)
                {
                    AddPortPropertyRow(view, serializedObject, iterator, port);
                    drawnPorts.Add(port.FieldName);
                    continue;
                }

                var field = new PropertyField(iterator.Copy());
                field.Bind(serializedObject);
                field.style.minWidth = 0;
                field.AddToClassList("xn-field");
                view.Body.Add(field);
            }

            foreach (var dynamicPort in node.DynamicPorts)
            {
                if (drawnPorts.Contains(dynamicPort.FieldName)) continue;
                if (IsIndexedDynamicListPort(dynamicPort)) continue;
                view.AddPortRow(
                    dynamicPort,
                    ObjectNames.NicifyVariableName(dynamicPort.FieldName),
                    dynamicPort.IsOutput);
            }
        }

        public static void BuildDynamicPortList(
            NodeView view,
            string fieldName,
            Type portType,
            SerializedObject serializedObject,
            NodePort.IO io,
            Node.ConnectionType connectionType,
            Node.TypeConstraint typeConstraint,
            Action onChanged = null)
        {
            if (view == null || serializedObject == null || view.Node == null) return;
            var node = view.Node;
            var arrayData = serializedObject.FindProperty(fieldName);
            if (arrayData == null || !arrayData.isArray) return;
            serializedObject.Update();
            SyncDynamicListPorts(node, arrayData, portType, io, connectionType, typeConstraint);
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();

            var selected = -1;
            var list = new VisualElement();
            list.AddToClassList("xn-dynamic-list");
            var rows = new List<VisualElement>();

            void Select(int index)
            {
                selected = index;
                for (var r = 0; r < rows.Count; r++)
                    rows[r].EnableInClassList("selected", r == index);
            }

            for (var i = 0; i < arrayData.arraySize; i++)
            {
                var index = i;
                var row = new VisualElement();
                row.AddToClassList("node-port-row");
                if (io == NodePort.IO.Output) row.AddToClassList("output");
                var item = arrayData.GetArrayElementAtIndex(i);
                var field = new PropertyField(item, "");
                field.Bind(serializedObject);
                field.style.flexGrow = 1;
                field.style.minWidth = 0;
                var port = node.GetPort(fieldName + " " + i);
                if (io == NodePort.IO.Output)
                {
                    row.Add(field);
                    if (port != null) row.Add(view.CreatePort(port));
                }
                else
                {
                    if (port != null) row.Add(view.CreatePort(port));
                    row.Add(field);
                }

                row.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    Select(index);
                }, TrickleDown.TrickleDown);
                rows.Add(row);
                list.Add(row);
            }

            var footer = new VisualElement();
            footer.AddToClassList("xn-list-footer");
            var canEdit = !Application.isPlaying;
            var add = MiniButton("+", () =>
            {
                Undo.RecordObject(node, "Add List Item");
                serializedObject.Update();
                arrayData.InsertArrayElementAtIndex(arrayData.arraySize);
                serializedObject.ApplyModifiedProperties();
                var newName = fieldName + " 0";
                var n = 0;
                while (node.HasPort(newName)) newName = fieldName + " " + (++n);
                if (io == NodePort.IO.Output)
                    node.AddDynamicOutput(portType, connectionType, typeConstraint, newName);
                else
                    node.AddDynamicInput(portType, connectionType, typeConstraint, newName);
                EditorUtility.SetDirty(node);
                onChanged?.Invoke();
                RebuildWindow();
            });
            add.SetEnabled(canEdit);
            var remove = MiniButton("-", () =>
            {
                serializedObject.Update();
                var listProp = serializedObject.FindProperty(fieldName);
                var size = listProp != null && listProp.isArray
                    ? listProp.arraySize
                    : CountIndexedPorts(node, fieldName);
                if (size <= 0) return;
                var index = selected >= 0 && selected < size ? selected : size - 1;
                Undo.RecordObject(node, "Remove List Item");
                RemoveDynamicListItem(node, fieldName, index);
                RemoveArrayItem(node, fieldName, index);
                EditorUtility.SetDirty(node);
                serializedObject.Update();
                onChanged?.Invoke();
                RebuildWindowImmediate();
            });
            remove.SetEnabled(canEdit);
            footer.Add(add);
            footer.Add(remove);
            list.Add(footer);
            view.Body.Add(list);
        }

        public static Button MiniButton(string text, Action onClick)
        {
            var button = new Button(onClick) {text = text};
            button.style.width = 18;
            button.style.minWidth = 18;
            button.style.height = 18;
            button.style.marginRight = 1;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            return button;
        }

        public static PropertyField BindProperty(SerializedObject serializedObject, SerializedProperty property,
            string label = null)
        {
            var field = label != null
                ? new PropertyField(property, label)
                : new PropertyField(property);
            field.Bind(serializedObject);
            field.style.minWidth = 0;
            field.AddToClassList("xn-field");
            return field;
        }

        public static void OnUserPropertyChange(PropertyField field, Action onChange)
        {
            if (field == null || onChange == null) return;
            var ready = false;
            field.schedule.Execute(() => ready = true);
            field.RegisterValueChangeCallback(_ =>
            {
                if (ready) onChange();
            });
        }

        private static bool _rebuilding;

        public static void RebuildWindow()
        {
            QueueRebuild(false);
        }

        public static void RebuildWindowImmediate()
        {
            QueueRebuild(true);
        }

        private static void QueueRebuild(bool immediate)
        {
            if (_rebuilding) return;
            _rebuilding = true;

            void Run()
            {
                try
                {
                    NodeEditorWindow.Current?.RebuildUi();
                }
                finally
                {
                    EditorApplication.delayCall += () => _rebuilding = false;
                }
            }

            if (immediate) Run();
            else EditorApplication.delayCall += Run;
        }

        private static HashSet<string> MakeSkip(string[] extraExcludes)
        {
            var skip = extraExcludes != null && extraExcludes.Length > 0
                ? new HashSet<string>(DefaultExcludes)
                : DefaultExcludes;
            if (extraExcludes == null) return skip;
            for (var i = 0; i < extraExcludes.Length; i++) skip.Add(extraExcludes[i]);
            return skip;
        }

        private static void AddPortPropertyRow(
            NodeView view,
            SerializedObject serializedObject,
            SerializedProperty property,
            NodePort port)
        {
            var row = new VisualElement();
            row.AddToClassList("node-port-row");
            if (port.IsOutput) row.AddToClassList("output");
            var portView = view.CreatePort(port);
            var caption = ObjectNames.NicifyVariableName(property.name);
            if (ShouldShowBacking(port))
            {
                var field = new PropertyField(property.Copy(), caption);
                field.Bind(serializedObject);
                field.AddToClassList("xn-field");
                field.style.flexGrow = 1;
                field.style.minWidth = 0;
                if (port.IsOutput)
                {
                    row.Add(field);
                    row.Add(portView);
                }
                else
                {
                    row.Add(portView);
                    row.Add(field);
                }
            }
            else
            {
                var label = new Label(caption);
                label.AddToClassList("port-label");
                if (port.IsOutput)
                {
                    row.Add(label);
                    row.Add(portView);
                }
                else
                {
                    row.Add(portView);
                    row.Add(label);
                }
            }

            view.Body.Add(row);
        }

        private static bool ShouldShowBacking(NodePort port)
        {
            if (port.IsOutput)
            {
                if (NodeEditorUtilities.GetCachedAttrib(port.Node.GetType(), port.FieldName,
                        out Node.OutputAttribute output))
                {
                    if (output.BackingValue == Node.ShowBackingValue.Never) return false;
                    if (output.BackingValue == Node.ShowBackingValue.Always) return true;
                }

                return !port.IsConnected;
            }

            if (NodeEditorUtilities.GetCachedAttrib(port.Node.GetType(), port.FieldName, out Node.InputAttribute input))
            {
                if (input.BackingValue == Node.ShowBackingValue.Never) return false;
                if (input.BackingValue == Node.ShowBackingValue.Always) return true;
            }

            return !port.IsConnected;
        }

        private static bool IsIndexedDynamicListPort(NodePort port)
        {
            if (port == null || port.Node == null) return false;
            var parts = port.FieldName.Split(' ');
            if (parts.Length != 2 || !int.TryParse(parts[1], out _)) return false;
            var field = port.Node.GetType().GetField(
                parts[0],
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            if (field == null) return false;
            var output = field.GetCustomAttribute<Node.OutputAttribute>();
            if (output != null) return output.DynamicPortList;
            var input = field.GetCustomAttribute<Node.InputAttribute>();
            return input != null && input.DynamicPortList;
        }

        private static void SyncDynamicListPorts(
            Node node,
            SerializedProperty arrayData,
            Type portType,
            NodePort.IO io,
            Node.ConnectionType connectionType,
            Node.TypeConstraint typeConstraint)
        {
            var portCount = CountIndexedPorts(node, arrayData.name);
            while (portCount < arrayData.arraySize)
            {
                var newName = arrayData.name + " 0";
                var i = 0;
                while (node.HasPort(newName)) newName = arrayData.name + " " + (++i);
                if (io == NodePort.IO.Output)
                    node.AddDynamicOutput(portType, connectionType, typeConstraint, newName);
                else
                    node.AddDynamicInput(portType, connectionType, typeConstraint, newName);
                portCount++;
            }

            while (arrayData.arraySize < portCount)
            {
                arrayData.InsertArrayElementAtIndex(arrayData.arraySize);
            }
        }

        private static int CountIndexedPorts(Node node, string fieldName)
        {
            var count = 0;
            foreach (var port in node.DynamicPorts)
            {
                var parts = port.FieldName.Split(' ');
                if (parts.Length == 2 && parts[0] == fieldName && int.TryParse(parts[1], out _))
                    count++;
            }

            return count;
        }

        private static void RemoveArrayItem(Node node, string fieldName, int index)
        {
            if (node == null || index < 0) return;
            var field = node.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return;
            var value = field.GetValue(node);
            if (value is Array array)
            {
                if (index >= array.Length) return;
                var elementType = array.GetType().GetElementType();
                if (elementType == null) return;
                var next = Array.CreateInstance(elementType, array.Length - 1);
                if (index > 0) Array.Copy(array, 0, next, 0, index);
                var tail = array.Length - index - 1;
                if (tail > 0) Array.Copy(array, index + 1, next, index, tail);
                field.SetValue(node, next);
                return;
            }

            if (value is System.Collections.IList list && index < list.Count)
                list.RemoveAt(index);
        }

        private static void RemoveDynamicListItem(Node node, string fieldName, int index)
        {
            var ports = new List<NodePort>();
            foreach (var port in node.DynamicPorts)
            {
                var parts = port.FieldName.Split(' ');
                if (parts.Length == 2 && parts[0] == fieldName && int.TryParse(parts[1], out var i))
                    ports.Add(port);
            }

            ports.Sort((a, b) =>
            {
                int.TryParse(a.FieldName.Split(' ')[1], out var ia);
                int.TryParse(b.FieldName.Split(' ')[1], out var ib);
                return ia.CompareTo(ib);
            });
            if (index < 0 || index >= ports.Count) return;
            ports[index].ClearConnections();
            for (var k = index + 1; k < ports.Count; k++)
            {
                for (var j = ports[k].ConnectionCount - 1; j >= 0; j--)
                {
                    var other = ports[k].GetConnection(j);
                    ports[k].Disconnect(other);
                    ports[k - 1].Connect(other);
                }
            }

            node.RemoveDynamicPort(ports[ports.Count - 1].FieldName);
        }
    }
}