using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Knot;

namespace Knot.Editor
{
    [CustomEditor(typeof(SceneGraph), true)]
    public class SceneGraphEditor : Editor
    {
        private SceneGraph _sceneGraph;
        private bool _removeSafely;
        private Type _graphType;

        public override void OnInspectorGUI()
        {
            if (_sceneGraph.Graph == null)
            {
                if (GUILayout.Button("New graph", GUILayout.Height(40)))
                {
                    if (_graphType == null)
                    {
                        var graphTypes = NodeEditorReflection.GetDerivedTypes(typeof(NodeGraph));
                        var menu = new GenericMenu();
                        for (var i = 0; i < graphTypes.Length; i++)
                        {
                            var graphType = graphTypes[i];
                            menu.AddItem(new GUIContent(graphType.Name), false, () => CreateGraph(graphType));
                        }

                        menu.ShowAsContext();
                    }
                    else
                    {
                        CreateGraph(_graphType);
                    }
                }
            }
            else
            {
                if (GUILayout.Button("Open graph", GUILayout.Height(40)))
                {
                    NodeEditorWindow.Open(_sceneGraph.Graph);
                }

                if (_removeSafely)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Really remove graph?");
                    GUI.color = new Color(1, 0.8f, 0.8f);
                    if (GUILayout.Button("Remove"))
                    {
                        _removeSafely = false;
                        Undo.RecordObject(_sceneGraph, "Removed graph");
                        _sceneGraph.Graph = null;
                    }

                    GUI.color = Color.white;
                    if (GUILayout.Button("Cancel"))
                    {
                        _removeSafely = false;
                    }

                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUI.color = new Color(1, 0.8f, 0.8f);
                    if (GUILayout.Button("Remove graph"))
                    {
                        _removeSafely = true;
                    }

                    GUI.color = Color.white;
                }
            }

            DrawDefaultInspector();
        }

        private void OnEnable()
        {
            _sceneGraph = target as SceneGraph;
            var sceneGraphType = _sceneGraph.GetType();
            if (sceneGraphType == typeof(SceneGraph))
            {
                _graphType = null;
            }
            else
            {
                var baseType = sceneGraphType.BaseType;
                if (baseType.IsGenericType)
                {
                    _graphType = sceneGraphType = baseType.GetGenericArguments()[0];
                }
            }
        }

        public void CreateGraph(Type type)
        {
            Undo.RecordObject(_sceneGraph, "Create graph");
            _sceneGraph.Graph = CreateInstance(type) as NodeGraph;
            _sceneGraph.Graph.name = _sceneGraph.name + "-graph";
        }
    }
}