using System;
using System.Collections.Generic;
using UnityEngine;

namespace XNode
{
    /// <summary> Base class for all node graphs </summary>
    [Serializable]
    public abstract class NodeGraph : ScriptableObject
    {
        /// <summary> All nodes in the graph. <para/>
        /// See: <see cref="AddNode{T}"/> </summary>
        [SerializeField] public List<Node> Nodes = new List<Node>();

        [SerializeField] private BlackboardDefinition _blackboardDefinition = new BlackboardDefinition();

        public const string BlackboardDefinitionField = nameof(_blackboardDefinition);

        public BlackboardDefinition BlackboardDefinition
        {
            get
            {
                if (_blackboardDefinition == null) _blackboardDefinition = new BlackboardDefinition();
                return _blackboardDefinition;
            }
        }

        /// <summary> Add a node to the graph by type (convenience method - will call the System.Type version) </summary>
        public T AddNode<T>() where T : Node
        {
            return AddNode(typeof(T)) as T;
        }

        /// <summary> Add a node to the graph by type </summary>
        public virtual Node AddNode(Type type)
        {
            Node.GraphHotfix = this;
            var node = CreateInstance(type) as Node;
            node.Graph = this;
            Nodes.Add(node);
            return node;
        }

        /// <summary> Creates a copy of the original node in the graph </summary>
        public virtual Node CopyNode(Node original)
        {
            Node.GraphHotfix = this;
            var node = Instantiate(original);
            node.Graph = this;
            node.RegenerateNodeId();
            node.ClearConnections();
            Nodes.Add(node);
            return node;
        }

        /// <summary> Safely remove a node and all its connections </summary>
        /// <param name="node"> The node to remove </param>
        public virtual void RemoveNode(Node node)
        {
            node.ClearConnections();
            Nodes.Remove(node);
            if (Application.isPlaying) Destroy(node);
        }

        /// <summary> Remove all nodes and connections from the graph </summary>
        public virtual void Clear()
        {
            if (Application.isPlaying)
            {
                for (var i = 0; i < Nodes.Count; i++)
                {
                    if (Nodes[i] != null) Destroy(Nodes[i]);
                }
            }

            Nodes.Clear();
        }

        /// <summary> Create a new deep copy of this graph </summary>
        public virtual NodeGraph Copy()
        {
            // Instantiate a new nodegraph instance
            var graph = Instantiate(this);
            // Instantiate all nodes inside the graph
            for (var i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i] == null) continue;
                Node.GraphHotfix = graph;
                var node = Instantiate(Nodes[i]) as Node;
                node.Graph = graph;
                graph.Nodes[i] = node;
            }

            // Redirect all connections
            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                if (graph.Nodes[i] == null) continue;
                foreach (var port in graph.Nodes[i].Ports)
                {
                    port.Redirect(Nodes, graph.Nodes);
                }
            }

            return graph;
        }

        protected virtual void OnDestroy()
        {
            // Remove all nodes prior to graph destruction
            Clear();
        }

        #region Attributes

        /// <summary> Automatically ensures the existance of a certain node type, and prevents it from being deleted. </summary>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
        public class RequireNodeAttribute : Attribute
        {
            public readonly Type Type0;
            public readonly Type Type1;
            public readonly Type Type2;

            /// <summary> Automatically ensures the existance of a certain node type, and prevents it from being deleted </summary>
            public RequireNodeAttribute(Type type)
            {
                Type0 = type;
                Type1 = null;
                Type2 = null;
            }

            /// <summary> Automatically ensures the existance of a certain node type, and prevents it from being deleted </summary>
            public RequireNodeAttribute(Type type, Type type2)
            {
                Type0 = type;
                Type1 = type2;
                Type2 = null;
            }

            /// <summary> Automatically ensures the existance of a certain node type, and prevents it from being deleted </summary>
            public RequireNodeAttribute(Type type, Type type2, Type type3)
            {
                Type0 = type;
                Type1 = type2;
                Type2 = type3;
            }

            public bool Requires(Type type)
            {
                if (type == null) return false;
                if (type == Type0) return true;
                else if (type == Type1) return true;
                else if (type == Type2) return true;
                return false;
            }
        }

        #endregion
    }
}