using UnityEngine;

namespace Knot
{
    /// <summary> Lets you instantiate a node graph in the scene. This allows you to reference in-scene objects. </summary>
    public class SceneGraph : MonoBehaviour
    {
        public NodeGraph Graph;
    }

    /// <summary> Derive from this class to create a SceneGraph with a specific graph type. </summary>
    /// <example>
    /// <code>
    /// public class MySceneGraph : SceneGraph<MyGraph> {
    ///
    /// }
    /// </code>
    /// </example>
    public class SceneGraph<T> : SceneGraph where T : NodeGraph
    {
        public new T Graph
        {
            get => base.Graph as T;
            set => base.Graph = value;
        }
    }
}