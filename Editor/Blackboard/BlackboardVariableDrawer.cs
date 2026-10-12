using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace XNodeEditor
{
    [CustomPropertyDrawer(typeof(XNode.BlackboardVariable), true)]
    internal sealed class BlackboardVariableDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.AddToClassList("bb-row");

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            var nameProperty = property.FindPropertyRelative(XNode.BlackboardVariable.NameField);
            if (nameProperty != null)
            {
                var nameField = new PropertyField(nameProperty, "");
                nameField.style.flexGrow = 1;
                header.Add(nameField);
            }

            var variable = property.managedReferenceValue as XNode.BlackboardVariable;
            if (variable != null)
            {
                var typeLabel = new Label(BlackboardEditorPanel.TypeLabel(variable.ValueType));
                typeLabel.AddToClassList("toolbar-label");
                typeLabel.style.minWidth = 48;
                header.Add(typeLabel);
                var captured = variable;
                header.Add(new Button(() =>
                {
                    var window = NodeEditorWindow.Current;
                    if (window == null) return;
                    BlackboardEditorPanel.CreateGetter(window, captured);
                    window.RebuildUi();
                }) {text = "Get"});
            }

            root.Add(header);
            var defaultValue = property.FindPropertyRelative(XNode.BlackboardVariable<object>.DefaultValueField);
            if (defaultValue != null)
                root.Add(new PropertyField(defaultValue, "Default"));
            return root;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            try
            {
                var line = EditorGUIUtility.singleLineHeight;
                var y = position.y;
                var nameProperty = property.FindPropertyRelative(XNode.BlackboardVariable.NameField);
                var variable = property.managedReferenceValue as XNode.BlackboardVariable;
                var nameRect = new Rect(position.x, y, Mathf.Max(36f, position.width - 114f), line);
                var typeRect = new Rect(nameRect.xMax + 4f, y, 72f, line);
                var getRect = new Rect(typeRect.xMax + 4f, y, 34f, line);
                if (nameProperty != null)
                    EditorGUI.PropertyField(nameRect, nameProperty, GUIContent.none);
                if (variable != null)
                {
                    EditorGUI.LabelField(typeRect, NodeEditorUtilities.PrettyName(variable.ValueType),
                        EditorStyles.miniLabel);
                    if (GUI.Button(getRect, "Get") && NodeEditorWindow.Current != null)
                        BlackboardEditorPanel.CreateGetter(NodeEditorWindow.Current, variable);
                }

                var defaultValue = property.FindPropertyRelative(XNode.BlackboardVariable<object>.DefaultValueField);
                if (defaultValue == null) return;
                y += line + 2f;
                EditorGUI.PropertyField(
                    new Rect(position.x, y, position.width, EditorGUI.GetPropertyHeight(defaultValue, true)),
                    defaultValue,
                    new GUIContent("Default"),
                    true);
            }
            finally
            {
                EditorGUI.EndProperty();
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = EditorGUIUtility.singleLineHeight + 4f;
            var defaultValue = property.FindPropertyRelative(XNode.BlackboardVariable<object>.DefaultValueField);
            if (defaultValue != null)
                height += EditorGUI.GetPropertyHeight(defaultValue, true) + 2f;
            return height;
        }
    }
}