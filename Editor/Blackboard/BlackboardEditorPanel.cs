using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Knot.Editor
{
    internal static class BlackboardEditorPanel
    {
        private static Type[] _variableTypes;
        private static Dictionary<Type, Type> _getterTypes;

        private static readonly Type[] TypeOrder =
        {
            typeof(bool),
            typeof(int),
            typeof(float),
            typeof(string),
            typeof(Vector2),
            typeof(Vector3),
            typeof(Quaternion),
            typeof(Color),
            typeof(UnityEngine.Object)
        };

        private static readonly Dictionary<Type, string> TypeLabels = new Dictionary<Type, string>
        {
            {typeof(bool), "bool"},
            {typeof(int), "int"},
            {typeof(float), "float"},
            {typeof(string), "string"},
            {typeof(Vector2), "Vector2"},
            {typeof(Vector3), "Vector3"},
            {typeof(Quaternion), "Quaternion"},
            {typeof(Color), "Color"},
            {typeof(UnityEngine.Object), "Object"}
        };

        private static Type[] VariableTypes
        {
            get
            {
                if (_variableTypes == null) CacheTypes();
                return _variableTypes;
            }
        }

        private static Dictionary<Type, Type> GetterTypes
        {
            get
            {
                if (_getterTypes == null) CacheTypes();
                return _getterTypes;
            }
        }

        private static void CacheTypes()
        {
            var variables = new List<Type>();
            foreach (var type in typeof(Knot.BlackboardVariable).GetDerivedTypes())
            {
                if (type.GetConstructor(Type.EmptyTypes) == null) continue;
                if (ValueTypeOf(type) == null) continue;
                variables.Add(type);
            }

            variables.Sort(CompareVariableTypes);
            _variableTypes = variables.ToArray();

            _getterTypes = new Dictionary<Type, Type>();
            var nodes = NodeEditorReflection.NodeTypes;
            for (var i = 0; i < nodes.Length; i++)
            {
                var valueType = GetterValueType(nodes[i]);
                if (valueType != null) _getterTypes[valueType] = nodes[i];
            }
        }

        private static int CompareVariableTypes(Type a, Type b)
        {
            var orderA = TypeOrderIndex(ValueTypeOf(a));
            var orderB = TypeOrderIndex(ValueTypeOf(b));
            if (orderA != orderB) return orderA.CompareTo(orderB);
            return string.Compare(TypeLabel(ValueTypeOf(a)), TypeLabel(ValueTypeOf(b)), StringComparison.Ordinal);
        }

        private static int TypeOrderIndex(Type valueType)
        {
            if (valueType == null) return TypeOrder.Length + 100;
            for (var i = 0; i < TypeOrder.Length; i++)
            {
                if (TypeOrder[i] == valueType) return i;
                if (TypeOrder[i] == typeof(UnityEngine.Object) &&
                    typeof(UnityEngine.Object).IsAssignableFrom(valueType))
                {
                    return i;
                }
            }

            return TypeOrder.Length + 1;
        }

        private static Type ValueTypeOf(Type variableType)
        {
            var current = variableType;
            while (current != null)
            {
                if (current.IsGenericType &&
                    current.GetGenericTypeDefinition() == typeof(Knot.BlackboardVariable<>))
                {
                    return current.GetGenericArguments()[0];
                }

                current = current.BaseType;
            }

            return null;
        }

        private static Type GetterValueType(Type nodeType)
        {
            var current = nodeType;
            while (current != null)
            {
                if (current.IsGenericType &&
                    current.GetGenericTypeDefinition() == typeof(Knot.GetBlackboardVariableNode<>))
                {
                    return current.GetGenericArguments()[0];
                }

                current = current.BaseType;
            }

            return null;
        }

        internal static VisualElement Build(NodeEditorWindow window)
        {
            return new Panel(window).Root;
        }

        internal static string TypeLabel(Type type)
        {
            if (type == null) return "?";
            if (TypeLabels.TryGetValue(type, out var label)) return label;
            return type.Name;
        }

        internal static void CreateGetter(NodeEditorWindow window, Knot.BlackboardVariable variable)
        {
            if (!GetterTypes.TryGetValue(variable.ValueType, out var getterType))
            {
                Debug.LogWarning($"No Blackboard getter node is registered for {variable.ValueType}.");
                return;
            }

            var windowPosition = new Vector2(
                Mathf.Max(0f, window.position.width - window.BlackboardWidth) * 0.5f,
                window.position.height * 0.5f);
            var node = window.GraphEditor.CreateNode(
                getterType,
                window.WindowToGridPosition(windowPosition));

            var referenceType = typeof(Knot.BlackboardVariableReference<>)
                .MakeGenericType(variable.ValueType);
            var reference = Activator.CreateInstance(referenceType, variable.Id);
            var variableProperty = node.GetType().GetProperty(nameof(Knot.GetBlackboardVariableNode<object>.Variable));
            variableProperty?.SetValue(node, reference);
            EditorUtility.SetDirty(node);
        }

        private sealed class Panel
        {
            public readonly VisualElement Root;
            private readonly NodeEditorWindow _window;
            private readonly SerializedObject _serializedGraph;
            private readonly SerializedProperty _variables;
            private readonly ListView _list;
            private readonly HelpBox _duplicate;
            private readonly List<int> _indices = new List<int>();
            private readonly bool _allowEdits;
            private IVisualElementScheduledItem _nodeRefresh;

            public Panel(NodeEditorWindow window)
            {
                _window = window;
                _allowEdits = !Application.isPlaying;
                _serializedGraph = new SerializedObject(window.Graph);
                _variables = _serializedGraph.FindProperty(Knot.NodeGraph.BlackboardDefinitionField)
                    ?.FindPropertyRelative(Knot.BlackboardDefinition.VariablesField);

                Root = new VisualElement();
                Root.AddToClassList("bb-panel");

                var header = new VisualElement();
                header.AddToClassList("bb-header");
                var title = new Label("Blackboard");
                title.AddToClassList("bb-title");
                title.tooltip = _allowEdits ? "Drag the handle to reorder." : "";
                header.Add(title);
                var addButton = new Button {text = "+"};
                addButton.tooltip = "Add variable";
                addButton.clicked += () => ShowAddMenu(addButton);
                addButton.SetEnabled(_allowEdits);
                header.Add(addButton);
                Root.Add(header);

                if (_variables == null)
                {
                    Root.Add(new HelpBox("Blackboard data could not be serialized.", HelpBoxMessageType.Error));
                    return;
                }

                _list = new ListView
                {
                    reorderable = _allowEdits,
                    reorderMode = ListViewReorderMode.Animated,
                    virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                    selectionType = SelectionType.None,
                    showBoundCollectionSize = false,
                    showAddRemoveFooter = false,
                    showFoldoutHeader = false,
                    horizontalScrollingEnabled = false,
                    fixedItemHeight = 72
                };
                _list.AddToClassList("bb-list");
                _list.makeItem = MakeRow;
                _list.bindItem = BindRow;
                _list.unbindItem = UnbindRow;
                _list.itemIndexChanged += OnReordered;
                RefreshSource();
                Root.Add(_list);
                _list.schedule.Execute(() =>
                {
                    var scroll = _list.Q<ScrollView>();
                    if (scroll != null)
                        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                });

                _duplicate = new HelpBox("", HelpBoxMessageType.Error);
                Root.Add(_duplicate);
                RefreshDuplicate();
                Root.TrackSerializedObjectValue(_serializedGraph, _ => RefreshDuplicate());
            }

            private VisualElement MakeRow()
            {
                var card = new VisualElement();
                card.AddToClassList("bb-card");

                var top = new VisualElement();
                top.AddToClassList("bb-card-header");

                var nameField = new TextField {name = "name"};
                nameField.AddToClassList("bb-name");
                top.Add(nameField);

                var typeLabel = new Label {name = "type"};
                typeLabel.AddToClassList("bb-type");
                top.Add(typeLabel);

                var getButton = new Button {name = "get", text = "Get"};
                getButton.AddToClassList("bb-get");
                top.Add(getButton);

                var remove = new Button {name = "remove", text = "×"};
                remove.AddToClassList("bb-remove");
                remove.SetEnabled(_allowEdits);
                top.Add(remove);

                card.Add(top);

                var valueHost = new VisualElement {name = "value"};
                valueHost.AddToClassList("bb-value-host");
                card.Add(valueHost);
                return card;
            }

            private void BindRow(VisualElement card, int index)
            {
                if (index < 0 || index >= _variables.arraySize) return;
                _serializedGraph.Update();
                var element = _variables.GetArrayElementAtIndex(index);
                var variable = element.managedReferenceValue as Knot.BlackboardVariable;
                card.EnableInClassList("highlighted",
                    variable != null && _window.HighlightedVariableId == variable.Id);
                card.userData = index;

                var nameField = card.Q<TextField>("name");
                var nameProperty = element.FindPropertyRelative(Knot.BlackboardVariable.NameField);
                nameField.UnregisterValueChangedCallback(OnNameChanged);
                nameField.Unbind();
                if (nameProperty != null) nameField.BindProperty(nameProperty);
                nameField.SetEnabled(_allowEdits);
                nameField.RegisterValueChangedCallback(OnNameChanged);

                var typeLabel = card.Q<Label>("type");
                typeLabel.text = TypeLabel(variable?.ValueType);
                typeLabel.tooltip = variable != null
                    ? NodeEditorUtilities.PrettyName(variable.ValueType)
                    : "";

                var getButton = card.Q<Button>("get");
                getButton.userData = variable;
                getButton.UnregisterCallback<ClickEvent>(OnGetClicked);
                getButton.RegisterCallback<ClickEvent>(OnGetClicked);

                var remove = card.Q<Button>("remove");
                remove.userData = index;
                remove.UnregisterCallback<ClickEvent>(OnRemoveClicked);
                remove.RegisterCallback<ClickEvent>(OnRemoveClicked);

                BindValue(card.Q("value"), element.FindPropertyRelative(Knot.BlackboardVariable<object>.DefaultValueField), variable?.ValueType);
            }

            private void UnbindRow(VisualElement card, int _)
            {
                var nameField = card.Q<TextField>("name");
                if (nameField != null)
                {
                    nameField.UnregisterValueChangedCallback(OnNameChanged);
                    nameField.Unbind();
                }

                card.Q("value")?.Clear();
                card.Q<Button>("get")?.UnregisterCallback<ClickEvent>(OnGetClicked);
                card.Q<Button>("remove")?.UnregisterCallback<ClickEvent>(OnRemoveClicked);
            }

            private void BindValue(VisualElement host, SerializedProperty defaultValue, Type type)
            {
                if (host == null) return;
                host.Clear();
                if (defaultValue == null) return;

                VisualElement field;
                if (type == typeof(Quaternion))
                {
                    var euler = new Vector3Field();
                    euler.SetValueWithoutNotify(defaultValue.quaternionValue.eulerAngles);
                    euler.RegisterValueChangedCallback(evt =>
                    {
                        defaultValue.quaternionValue = Quaternion.Euler(evt.newValue);
                        defaultValue.serializedObject.ApplyModifiedProperties();
                    });
                    field = euler;
                }
                else
                {
                    var property = new PropertyField(defaultValue, "");
                    property.BindProperty(defaultValue);
                    field = property;
                }

                field.AddToClassList("bb-value");
                host.Add(field);
            }

            private void OnNameChanged(ChangeEvent<string> _)
            {
                _serializedGraph.ApplyModifiedProperties();
                RefreshDuplicate();
                RequestNodeRefresh();
            }

            private void OnGetClicked(ClickEvent evt)
            {
                if (evt.currentTarget is not Button button) return;
                if (button.userData is not Knot.BlackboardVariable variable) return;
                CreateGetter(_window, variable);
                _window.RebuildUi();
            }

            private void OnRemoveClicked(ClickEvent evt)
            {
                if (evt.currentTarget is not Button button) return;
                if (button.userData is not int index) return;
                RemoveVariable(index);
            }

            private void OnReordered(int from, int to)
            {
                if (from == to) return;
                Undo.RecordObject(_window.Graph, "Reorder Blackboard Variable");
                _serializedGraph.Update();
                _variables.MoveArrayElement(from, to);
                _serializedGraph.ApplyModifiedProperties();
                EditorUtility.SetDirty(_window.Graph);
                RefreshSource();
                RefreshDuplicate();
                _window.RefreshNodes();
            }

            private void ShowAddMenu(VisualElement anchor)
            {
                var menu = new GenericDropdownMenu();
                var types = VariableTypes;
                for (var i = 0; i < types.Length; i++)
                {
                    var type = types[i];
                    var label = TypeLabel(ValueTypeOf(type)) ?? type.Name;
                    menu.AddItem(label, false, () => AddVariable(type, label));
                }

                menu.DropDown(anchor.worldBound, anchor, false);
            }

            private void AddVariable(Type variableType, string label)
            {
                Undo.RecordObject(_window.Graph, "Add Blackboard Variable");
                _serializedGraph.Update();
                var index = _variables.arraySize;
                _variables.InsertArrayElementAtIndex(index);
                _variables.GetArrayElementAtIndex(index).managedReferenceValue =
                    Activator.CreateInstance(variableType);
                _serializedGraph.ApplyModifiedProperties();

                var variable = _window.Graph.BlackboardDefinition.Variables[index];
                _window.Graph.BlackboardDefinition.Rename(
                    variable.Id,
                    _window.Graph.BlackboardDefinition.GetUniqueName(label));
                EditorUtility.SetDirty(_window.Graph);
                RefreshSource();
                RefreshDuplicate();
                _window.RefreshNodes();
            }

            private void RemoveVariable(int index)
            {
                if (index < 0 || index >= _variables.arraySize) return;
                Undo.RecordObject(_window.Graph, "Remove Blackboard Variable");
                _serializedGraph.Update();
                _variables.DeleteArrayElementAtIndex(index);
                _serializedGraph.ApplyModifiedProperties();
                EditorUtility.SetDirty(_window.Graph);
                RefreshSource();
                RefreshDuplicate();
                _window.RefreshNodes();
            }

            private void RequestNodeRefresh()
            {
                _nodeRefresh?.Pause();
                _nodeRefresh = Root.schedule.Execute(() => _window.RefreshNodes());
                _nodeRefresh.ExecuteLater(200);
            }

            private void RefreshSource()
            {
                _serializedGraph.Update();
                _indices.Clear();
                for (var i = 0; i < _variables.arraySize; i++)
                    _indices.Add(i);
                _list.itemsSource = _indices;
                _list.RefreshItems();
            }

            private void RefreshDuplicate()
            {
                _serializedGraph.Update();
                var duplicate = FindDuplicateName(_window.Graph.BlackboardDefinition.Variables);
                var show = !string.IsNullOrEmpty(duplicate);
                _duplicate.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (show) _duplicate.text = $"Variable name '{duplicate}' is duplicated.";
            }
        }

        private static string FindDuplicateName(IReadOnlyList<Knot.BlackboardVariable> variables)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < variables.Count; i++)
            {
                var variable = variables[i];
                if (variable != null && !names.Add(variable.Name)) return variable.Name;
            }

            return null;
        }
    }
}