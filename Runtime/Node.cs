using System;
using System.Collections.Generic;
using UnityEngine;

namespace XNode
{
    /// <summary>
    /// Base class for all nodes
    /// </summary>
    /// <example>
    /// Classes extending this class will be considered as valid nodes by xNode.
    /// <code>
    /// [System.Serializable]
    /// public class Adder : Node {
    ///     [Input] public float a;
    ///     [Input] public float b;
    ///     [Output] public float result;
    ///
    ///     // GetValue should be overridden to return a value for any specified output port
    ///     public override object GetValue(NodePort port) {
    ///         return a + b;
    ///     }
    /// }
    /// </code>
    /// </example>
    [Serializable]
    public abstract class Node : ScriptableObject
    {
        /// <summary> Used by <see cref="InputAttribute"/> and <see cref="OutputAttribute"/> to determine when to display the field value associated with a <see cref="NodePort"/> </summary>
        public enum ShowBackingValue
        {
            /// <summary> Never show the backing value </summary>
            Never,

            /// <summary> Show the backing value only when the port does not have any active connections </summary>
            Unconnected,

            /// <summary> Always show the backing value </summary>
            Always
        }

        public enum ConnectionType
        {
            /// <summary> Allow multiple connections</summary>
            Multiple,

            /// <summary> always override the current connection </summary>
            Override,
        }

        /// <summary> Tells which types of input to allow </summary>
        public enum TypeConstraint
        {
            /// <summary> Allow all types of input</summary>
            None,

            /// <summary> Allow connections where input value type is assignable from output value type (eg. ScriptableObject --> Object)</summary>
            Inherited,

            /// <summary> Allow only similar types </summary>
            Strict,

            /// <summary> Allow connections where output value type is assignable from input value type (eg. Object --> ScriptableObject)</summary>
            InheritedInverse,

            /// <summary> Allow connections where output value type is assignable from input value or input value type is assignable from output value type</summary>
            InheritedAny
        }

        #region Obsolete

        [Obsolete("Use DynamicPorts instead")] public IEnumerable<NodePort> InstancePorts => DynamicPorts;

        [Obsolete("Use DynamicOutputs instead")]
        public IEnumerable<NodePort> InstanceOutputs => DynamicOutputs;

        [Obsolete("Use DynamicInputs instead")]
        public IEnumerable<NodePort> InstanceInputs => DynamicInputs;

        [Obsolete("Use AddDynamicInput instead")]
        public NodePort AddInstanceInput(Type type, ConnectionType connectionType = ConnectionType.Multiple,
            TypeConstraint typeConstraint = TypeConstraint.None, string fieldName = null)
        {
            return AddDynamicInput(type, connectionType, typeConstraint, fieldName);
        }

        [Obsolete("Use AddDynamicOutput instead")]
        public NodePort AddInstanceOutput(Type type, ConnectionType connectionType = ConnectionType.Multiple,
            TypeConstraint typeConstraint = TypeConstraint.None, string fieldName = null)
        {
            return AddDynamicOutput(type, connectionType, typeConstraint, fieldName);
        }

        [Obsolete("Use AddDynamicPort instead")]
        private NodePort AddInstancePort(Type type, NodePort.IO direction,
            ConnectionType connectionType = ConnectionType.Multiple,
            TypeConstraint typeConstraint = TypeConstraint.None, string fieldName = null)
        {
            return AddDynamicPort(type, direction, connectionType, typeConstraint, fieldName);
        }

        [Obsolete("Use RemoveDynamicPort instead")]
        public void RemoveInstancePort(string fieldName)
        {
            RemoveDynamicPort(fieldName);
        }

        [Obsolete("Use RemoveDynamicPort instead")]
        public void RemoveInstancePort(NodePort port)
        {
            RemoveDynamicPort(port);
        }

        [Obsolete("Use ClearDynamicPorts instead")]
        public void ClearInstancePorts()
        {
            ClearDynamicPorts();
        }

        #endregion

        /// <summary> Iterate over all ports on this node. </summary>
        public IEnumerable<NodePort> Ports
        {
            get
            {
                foreach (var port in _ports.Values) yield return port;
            }
        }

        /// <summary> Iterate over all outputs on this node. </summary>
        public IEnumerable<NodePort> Outputs
        {
            get
            {
                foreach (var port in Ports)
                {
                    if (port.IsOutput) yield return port;
                }
            }
        }

        /// <summary> Iterate over all inputs on this node. </summary>
        public IEnumerable<NodePort> Inputs
        {
            get
            {
                foreach (var port in Ports)
                {
                    if (port.IsInput) yield return port;
                }
            }
        }

        /// <summary> Iterate over all dynamic ports on this node. </summary>
        public IEnumerable<NodePort> DynamicPorts
        {
            get
            {
                foreach (var port in Ports)
                {
                    if (port.IsDynamic) yield return port;
                }
            }
        }

        /// <summary> Iterate over all dynamic outputs on this node. </summary>
        public IEnumerable<NodePort> DynamicOutputs
        {
            get
            {
                foreach (var port in Ports)
                {
                    if (port.IsDynamic && port.IsOutput) yield return port;
                }
            }
        }

        /// <summary> Iterate over all dynamic inputs on this node. </summary>
        public IEnumerable<NodePort> DynamicInputs
        {
            get
            {
                foreach (var port in Ports)
                {
                    if (port.IsDynamic && port.IsInput) yield return port;
                }
            }
        }

        /// <summary> Parent <see cref="NodeGraph"/> </summary>
        [SerializeField] public NodeGraph Graph;

        /// <summary> Stable identity used by compiled runtime contexts. </summary>
        [SerializeField, HideInInspector] private string _nodeId;

        /// <summary> Position on the <see cref="NodeGraph"/> </summary>
        [SerializeField] public Vector2 Position;

        /// <summary> It is recommended not to modify these at hand. Instead, see <see cref="InputAttribute"/> and <see cref="OutputAttribute"/> </summary>
        [SerializeField] private NodePortDictionary _ports = new NodePortDictionary();

        public const string NodeIdField = nameof(_nodeId);
        public const string PortsField = nameof(_ports);

        /// <summary> Used during node instantiation to fix null/misconfigured graph during OnEnable/Init. Set it before instantiating a node. Will automatically be unset during OnEnable </summary>
        public static NodeGraph GraphHotfix;

        protected void OnEnable()
        {
            if (GraphHotfix != null) Graph = GraphHotfix;
            GraphHotfix = null;
            EnsureNodeId();
            UpdatePorts();
            Init();
        }

        public string NodeId
        {
            get
            {
                EnsureNodeId();
                return _nodeId;
            }
        }

        internal void RegenerateNodeId()
        {
            _nodeId = Guid.NewGuid().ToString("N");
        }

        private void EnsureNodeId()
        {
            if (string.IsNullOrEmpty(_nodeId)) RegenerateNodeId();
        }

        /// <summary> Update static ports and dynamic ports managed by DynamicPortLists to reflect class fields. This happens automatically on enable or on redrawing a dynamic port list. </summary>
        public void UpdatePorts()
        {
            NodeDataCache.UpdatePorts(this, _ports);
        }

        /// <summary> Initialize node. Called on enable. </summary>
        protected virtual void Init()
        {
        }

        /// <summary> Checks all connections for invalid references, and removes them. </summary>
        public void VerifyConnections()
        {
            foreach (var port in Ports) port.VerifyConnections();
        }

        #region Dynamic Ports

        /// <summary> Convenience function. </summary>
        /// <seealso cref="AddInstancePort"/>
        /// <seealso cref="AddInstanceOutput"/>
        public NodePort AddDynamicInput(Type type, ConnectionType connectionType = ConnectionType.Multiple,
            TypeConstraint typeConstraint = TypeConstraint.None, string fieldName = null)
        {
            return AddDynamicPort(type, NodePort.IO.Input, connectionType, typeConstraint, fieldName);
        }

        /// <summary> Convenience function. </summary>
        /// <seealso cref="AddInstancePort"/>
        /// <seealso cref="AddInstanceInput"/>
        public NodePort AddDynamicOutput(Type type, ConnectionType connectionType = ConnectionType.Multiple,
            TypeConstraint typeConstraint = TypeConstraint.None, string fieldName = null)
        {
            return AddDynamicPort(type, NodePort.IO.Output, connectionType, typeConstraint, fieldName);
        }

        /// <summary> Add a dynamic, serialized port to this node. </summary>
        /// <seealso cref="AddDynamicInput"/>
        /// <seealso cref="AddDynamicOutput"/>
        private NodePort AddDynamicPort(Type type, NodePort.IO direction,
            ConnectionType connectionType = ConnectionType.Multiple,
            TypeConstraint typeConstraint = TypeConstraint.None, string fieldName = null)
        {
            if (fieldName == null)
            {
                fieldName = "dynamicInput_0";
                var i = 0;
                while (HasPort(fieldName)) fieldName = "dynamicInput_" + (++i);
            }
            else if (HasPort(fieldName))
            {
                Debug.LogWarning("Port '" + fieldName + "' already exists in " + name, this);
                return _ports[fieldName];
            }

            var port = new NodePort(fieldName, type, direction, connectionType, typeConstraint, this);
            _ports.Add(fieldName, port);
            return port;
        }

        /// <summary> Remove an dynamic port from the node </summary>
        public void RemoveDynamicPort(string fieldName)
        {
            var dynamicPort = GetPort(fieldName);
            if (dynamicPort == null) throw new ArgumentException("port " + fieldName + " doesn't exist");
            RemoveDynamicPort(GetPort(fieldName));
        }

        /// <summary> Remove an dynamic port from the node </summary>
        public void RemoveDynamicPort(NodePort port)
        {
            if (port == null) throw new ArgumentNullException("port");
            else if (port.IsStatic) throw new ArgumentException("cannot remove static port");
            port.ClearConnections();
            _ports.Remove(port.FieldName);
        }

        /// <summary> Removes all dynamic ports from the node </summary>
        [ContextMenu("Clear Dynamic Ports")]
        public void ClearDynamicPorts()
        {
            var dynamicPorts = new List<NodePort>(DynamicPorts);
            foreach (var port in dynamicPorts)
            {
                RemoveDynamicPort(port);
            }
        }

        #endregion

        #region Ports

        /// <summary> Returns output port which matches fieldName </summary>
        public NodePort GetOutputPort(string fieldName)
        {
            var port = GetPort(fieldName);
            if (port == null || port.Direction != NodePort.IO.Output) return null;
            else return port;
        }

        /// <summary> Returns input port which matches fieldName </summary>
        public NodePort GetInputPort(string fieldName)
        {
            var port = GetPort(fieldName);
            if (port == null || port.Direction != NodePort.IO.Input) return null;
            else return port;
        }

        /// <summary> Returns port which matches fieldName </summary>
        public NodePort GetPort(string fieldName)
        {
            NodePort port;
            if (_ports.TryGetValue(fieldName, out port)) return port;
            else return null;
        }

        public bool HasPort(string fieldName)
        {
            return _ports.ContainsKey(fieldName);
        }

        #endregion

        #region Inputs/Outputs

        /// <summary> Return input value for a specified port. Returns fallback value if no ports are connected </summary>
        /// <param name="fieldName">Field name of requested input port</param>
        /// <param name="fallback">If no ports are connected, this value will be returned</param>
        public T GetInputValue<T>(string fieldName, T fallback = default(T))
        {
            var port = GetPort(fieldName);
            if (port != null && port.IsConnected) return port.GetInputValue<T>();
            else return fallback;
        }

        /// <summary> Return an input value for one graph execution context. </summary>
        public T GetInputValue<T>(
            string fieldName,
            GraphExecutionContext context,
            T fallback = default(T))
        {
            var port = GetPort(fieldName);
            if (port != null && port.IsConnected) return port.GetInputValue<T>(context);
            return fallback;
        }

        /// <summary> Return all input values for a specified port. Returns fallback value if no ports are connected </summary>
        /// <param name="fieldName">Field name of requested input port</param>
        /// <param name="fallback">If no ports are connected, this value will be returned</param>
        public T[] GetInputValues<T>(string fieldName, params T[] fallback)
        {
            var port = GetPort(fieldName);
            if (port != null && port.IsConnected) return port.GetInputValues<T>();
            else return fallback;
        }

        /// <summary> Returns a value based on requested port output. Should be overridden in all derived nodes with outputs. </summary>
        /// <param name="port">The requested port.</param>
        public virtual object GetValue(NodePort port)
        {
            Debug.LogWarning("No GetValue(NodePort port) override defined for " + GetType());
            return null;
        }

        /// <summary>
        /// Returns a value using one execution context. Override this for context-aware providers.
        /// Existing nodes remain compatible through the context-free overload.
        /// </summary>
        public virtual object GetValue(NodePort port, GraphExecutionContext context)
        {
            return GetValue(port);
        }

        #endregion

        /// <summary> Called after a connection between two <see cref="NodePort"/>s is created </summary>
        /// <param name="from">Output</param> <param name="to">Input</param>
        public virtual void OnCreateConnection(NodePort from, NodePort to)
        {
        }

        /// <summary> Called after a connection is removed from this port </summary>
        /// <param name="port">Output or Input</param>
        public virtual void OnRemoveConnection(NodePort port)
        {
        }

        /// <summary> Disconnect everything from this node </summary>
        public void ClearConnections()
        {
            foreach (var port in Ports) port.ClearConnections();
        }

        #region Attributes

        /// <summary> Mark a serializable field as an input port. You can access this through <see cref="GetInputPort(string)"/> </summary>
        [AttributeUsage(AttributeTargets.Field)]
        public class InputAttribute : Attribute
        {
            public ShowBackingValue BackingValue;
            public ConnectionType ConnectionType;

            [Obsolete("Use dynamicPortList instead")]
            public bool InstancePortList
            {
                get => DynamicPortList;
                set => DynamicPortList = value;
            }

            public bool DynamicPortList;
            public TypeConstraint TypeConstraint;

            /// <summary> Mark a serializable field as an input port. You can access this through <see cref="GetInputPort(string)"/> </summary>
            /// <param name="backingValue">Should we display the backing value for this port as an editor field? </param>
            /// <param name="connectionType">Should we allow multiple connections? </param>
            /// <param name="typeConstraint">Constrains which input connections can be made to this port </param>
            /// <param name="dynamicPortList">If true, will display a reorderable list of inputs instead of a single port. Will automatically add and display values for lists and arrays </param>
            public InputAttribute(ShowBackingValue backingValue = ShowBackingValue.Unconnected,
                ConnectionType connectionType = ConnectionType.Multiple,
                TypeConstraint typeConstraint = TypeConstraint.None, bool dynamicPortList = false)
            {
                BackingValue = backingValue;
                ConnectionType = connectionType;
                DynamicPortList = dynamicPortList;
                TypeConstraint = typeConstraint;
            }
        }

        /// <summary> Mark a serializable field as an output port. You can access this through <see cref="GetOutputPort(string)"/> </summary>
        [AttributeUsage(AttributeTargets.Field)]
        public class OutputAttribute : Attribute
        {
            public ShowBackingValue BackingValue;
            public ConnectionType ConnectionType;

            [Obsolete("Use dynamicPortList instead")]
            public bool InstancePortList
            {
                get => DynamicPortList;
                set => DynamicPortList = value;
            }

            public bool DynamicPortList;
            public TypeConstraint TypeConstraint;

            /// <summary> Mark a serializable field as an output port. You can access this through <see cref="GetOutputPort(string)"/> </summary>
            /// <param name="backingValue">Should we display the backing value for this port as an editor field? </param>
            /// <param name="connectionType">Should we allow multiple connections? </param>
            /// <param name="typeConstraint">Constrains which input connections can be made from this port </param>
            /// <param name="dynamicPortList">If true, will display a reorderable list of outputs instead of a single port. Will automatically add and display values for lists and arrays </param>
            public OutputAttribute(ShowBackingValue backingValue = ShowBackingValue.Never,
                ConnectionType connectionType = ConnectionType.Multiple,
                TypeConstraint typeConstraint = TypeConstraint.None, bool dynamicPortList = false)
            {
                BackingValue = backingValue;
                ConnectionType = connectionType;
                DynamicPortList = dynamicPortList;
                TypeConstraint = typeConstraint;
            }

            /// <summary> Mark a serializable field as an output port. You can access this through <see cref="GetOutputPort(string)"/> </summary>
            /// <param name="backingValue">Should we display the backing value for this port as an editor field? </param>
            /// <param name="connectionType">Should we allow multiple connections? </param>
            /// <param name="dynamicPortList">If true, will display a reorderable list of outputs instead of a single port. Will automatically add and display values for lists and arrays </param>
            [Obsolete("Use constructor with TypeConstraint")]
            public OutputAttribute(ShowBackingValue backingValue, ConnectionType connectionType, bool dynamicPortList) :
                this(backingValue, connectionType, TypeConstraint.None, dynamicPortList)
            {
            }
        }

        /// <summary> Manually supply node class with a context menu path </summary>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
        public class CreateNodeMenuAttribute : Attribute
        {
            public string MenuName;
            public int Order;

            /// <summary> Manually supply node class with a context menu path </summary>
            /// <param name="menuName"> Path to this node in the context menu. Null or empty hides it. </param>
            public CreateNodeMenuAttribute(string menuName)
            {
                MenuName = menuName;
                Order = 0;
            }

            /// <summary> Manually supply node class with a context menu path </summary>
            /// <param name="menuName"> Path to this node in the context menu. Null or empty hides it. </param>
            /// <param name="order"> The order by which the menu items are displayed. </param>
            public CreateNodeMenuAttribute(string menuName, int order)
            {
                MenuName = menuName;
                Order = order;
            }
        }

        /// <summary> Prevents Node of the same type to be added more than once (configurable) to a NodeGraph </summary>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
        public class DisallowMultipleNodesAttribute : Attribute
        {
            // TODO: Make inheritance work in such a way that applying [DisallowMultipleNodes(1)] to type NodeBar : Node
            //       while type NodeFoo : NodeBar exists, will let you add *either one* of these nodes, but not both.
            public int Max;

            /// <summary> Prevents Node of the same type to be added more than once (configurable) to a NodeGraph </summary>
            /// <param name="max"> How many nodes to allow. Defaults to 1. </param>
            public DisallowMultipleNodesAttribute(int max = 1)
            {
                Max = max;
            }
        }

        /// <summary> Color for a port type or a specific in/out field. Field wins over type. Type wins over Preference slots. </summary>
        [AttributeUsage(
            AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Field,
            Inherited = true, AllowMultiple = false)]
        public class GraphPortColorAttribute : Attribute
        {
            public Color Color;

            public GraphPortColorAttribute(float r, float g, float b)
            {
                Color = new Color(r, g, b);
            }

            public GraphPortColorAttribute(string hex)
            {
                ColorUtility.TryParseHtmlString(hex, out Color);
            }

            public GraphPortColorAttribute(byte r, byte g, byte b)
            {
                Color = new Color32(r, g, b, byte.MaxValue);
            }
        }

        /// <summary> Specify a color for this node type </summary>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
        public class NodeTintAttribute : Attribute
        {
            public Color Color;

            /// <summary> Specify a color for this node type </summary>
            /// <param name="r"> Red [0.0f .. 1.0f] </param>
            /// <param name="g"> Green [0.0f .. 1.0f] </param>
            /// <param name="b"> Blue [0.0f .. 1.0f] </param>
            public NodeTintAttribute(float r, float g, float b)
            {
                Color = new Color(r, g, b);
            }

            /// <summary> Specify a color for this node type </summary>
            /// <param name="hex"> HEX color value </param>
            public NodeTintAttribute(string hex)
            {
                ColorUtility.TryParseHtmlString(hex, out Color);
            }

            /// <summary> Specify a color for this node type </summary>
            /// <param name="r"> Red [0 .. 255] </param>
            /// <param name="g"> Green [0 .. 255] </param>
            /// <param name="b"> Blue [0 .. 255] </param>
            public NodeTintAttribute(byte r, byte g, byte b)
            {
                Color = new Color32(r, g, b, byte.MaxValue);
            }
        }

        /// <summary>
        /// Icon file name without extension. Resolved from a folder named NodeIcons.
        /// The nearest attribute on the type hierarchy wins. An empty name clears an inherited icon.
        /// </summary>
        [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
        public class NodeIconAttribute : Attribute
        {
            public string Icon;

            /// <param name="icon">File name without extension, or empty to clear.</param>
            public NodeIconAttribute(string icon)
            {
                Icon = icon;
            }
        }

        /// <summary> Specify a width for this node type </summary>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
        public class NodeWidthAttribute : Attribute
        {
            public int Width;

            /// <summary> Specify a width for this node type </summary>
            /// <param name="width"> Width </param>
            public NodeWidthAttribute(int width)
            {
                Width = width;
            }
        }

        #endregion

        [Serializable]
        private class NodePortDictionary : Dictionary<string, NodePort>, ISerializationCallbackReceiver
        {
            [SerializeField] private List<string> _keys = new List<string>();
            [SerializeField] private List<NodePort> _values = new List<NodePort>();

            public void OnBeforeSerialize()
            {
                _keys.Clear();
                _values.Clear();
                _keys.Capacity = Count;
                _values.Capacity = Count;
                foreach (var pair in this)
                {
                    _keys.Add(pair.Key);
                    _values.Add(pair.Value);
                }
            }

            public void OnAfterDeserialize()
            {
                Clear();
                #if UNITY_2021_3_OR_NEWER
                EnsureCapacity(_keys.Count);
                #endif

                if (_keys.Count != _values.Count)
                    throw new Exception("there are " + _keys.Count + " keys and " + _values.Count +
                                        " values after deserialization. Make sure that both key and value types are serializable.");

                for (var i = 0; i < _keys.Count; i++)
                    Add(_keys[i], _values[i]);
            }
        }
    }
}