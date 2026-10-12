using UnityEditor;
using UnityEngine;


namespace Knot.Editor
{
    /// <summary> Override graph inspector to show an 'Open Graph' button at the top </summary>
    [CustomEditor(typeof(Knot.NodeGraph), true)]
    [CanEditMultipleObjects]
    public class GlobalGraphEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (GUILayout.Button("Edit graph", GUILayout.Height(40)))
            {
                NodeEditorWindow.Open(serializedObject.targetObject as Knot.NodeGraph);
            }

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            GUILayout.Label("Raw data", "BoldLabel");

            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(Knot.Node), true)]
    [CanEditMultipleObjects]
    public class GlobalNodeEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (GUILayout.Button("Edit graph", GUILayout.Height(40)))
            {
                var w = NodeEditorWindow.Open(((Knot.Node) target).Graph);
                w.Home(); // Focus selected node
            }

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            GUILayout.Label("Raw data", "BoldLabel");

            // Now draw the node itself.
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }
    }
}