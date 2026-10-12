using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace XNode
{
    [Serializable]
    public class NodePort
    {
        public enum IO
        {
            Input,
            Output
        }

        public int ConnectionCount => _connections.Count;

        /// <summary> Return the first non-null connection </summary>
        public NodePort Connection
        {
            get
            {
                for (var i = 0; i < _connections.Count; i++)
                {
                    if (_connections[i] != null) return _connections[i].Port;
                }

                return null;
            }
        }

        public IO Direction
        {
            get => _direction;
            internal set => _direction = value;
        }

        public Node.ConnectionType ConnectionType
        {
            get => _connectionType;
            internal set => _connectionType = value;
        }

        public Node.TypeConstraint TypeConstraint
        {
            get => _typeConstraint;
            internal set => _typeConstraint = value;
        }

        /// <summary> Is this port connected to anytihng? </summary>
        public bool IsConnected => _connections.Count != 0;

        public bool IsInput => Direction == IO.Input;

        public bool IsOutput => Direction == IO.Output;

        public string FieldName => _fieldName;

        public Node Node => _node;

        public bool IsDynamic => _dynamic;

        public bool IsStatic => !_dynamic;

        public Type ValueType
        {
            get
            {
                if (_valueType == null && !string.IsNullOrEmpty(_typeQualifiedName))
                    _valueType = Type.GetType(_typeQualifiedName, false);
                return _valueType;
            }
            set
            {
                if (_valueType == value) return;
                _valueType = value;
                if (value != null) _typeQualifiedName = NodeDataCache.GetTypeQualifiedName(value);
            }
        }

        private Type _valueType;

        [SerializeField] private string _fieldName;
        [SerializeField] private Node _node;
        [SerializeField] private string _typeQualifiedName;
        [SerializeField] private List<PortConnection> _connections = new List<PortConnection>();
        [SerializeField] private IO _direction;
        [SerializeField] private Node.ConnectionType _connectionType;
        [SerializeField] private Node.TypeConstraint _typeConstraint;
        [SerializeField] private bool _dynamic;

        /// <summary> Construct a static targetless nodeport. Used as a template. </summary>
        public NodePort(FieldInfo fieldInfo)
        {
            _fieldName = fieldInfo.Name;
            ValueType = fieldInfo.FieldType;
            _dynamic = false;
            var attribs = fieldInfo.GetCustomAttributes(false);
            for (var i = 0; i < attribs.Length; i++)
            {
                if (attribs[i] is Node.InputAttribute)
                {
                    _direction = IO.Input;
                    _connectionType = ((Node.InputAttribute) attribs[i]).ConnectionType;
                    _typeConstraint = ((Node.InputAttribute) attribs[i]).TypeConstraint;
                }
                else if (attribs[i] is Node.OutputAttribute)
                {
                    _direction = IO.Output;
                    _connectionType = ((Node.OutputAttribute) attribs[i]).ConnectionType;
                    _typeConstraint = ((Node.OutputAttribute) attribs[i]).TypeConstraint;
                }

                // Override ValueType of the Port
                if (attribs[i] is PortTypeOverrideAttribute)
                {
                    ValueType = (attribs[i] as PortTypeOverrideAttribute)?.Type;
                }
            }
        }

        /// <summary> Copy a nodePort but assign it to another node. </summary>
        public NodePort(NodePort nodePort, Node node)
        {
            _fieldName = nodePort._fieldName;
            ValueType = nodePort._valueType;
            _direction = nodePort.Direction;
            _dynamic = nodePort._dynamic;
            _connectionType = nodePort._connectionType;
            _typeConstraint = nodePort._typeConstraint;
            _node = node;
        }

        /// <summary> Construct a dynamic port. Dynamic ports are not forgotten on reimport, and is ideal for runtime-created ports. </summary>
        public NodePort(string fieldName, Type type, IO direction, Node.ConnectionType connectionType,
            Node.TypeConstraint typeConstraint, Node node)
        {
            _fieldName = fieldName;
            ValueType = type;
            _direction = direction;
            _node = node;
            _dynamic = true;
            _connectionType = connectionType;
            _typeConstraint = typeConstraint;
        }

        /// <summary> Checks all connections for invalid references, and removes them. </summary>
        public void VerifyConnections()
        {
            for (var i = _connections.Count - 1; i >= 0; i--)
            {
                if (_connections[i].Node != null &&
                    !string.IsNullOrEmpty(_connections[i].FieldName) &&
                    _connections[i].Node.GetPort(_connections[i].FieldName) != null)
                    continue;
                _connections.RemoveAt(i);
            }
        }

        /// <summary> Return the output value of this node through its parent nodes GetValue override method. </summary>
        /// <returns> <see cref="Node.GetValue(NodePort)"/> </returns>
        public object GetOutputValue()
        {
            if (Direction == IO.Input) return null;
            return Node.GetValue(this);
        }

        /// <summary> Return this output value for one graph execution context. </summary>
        public object GetOutputValue(GraphExecutionContext context)
        {
            if (Direction == IO.Input) return null;
            return Node.GetValue(this, context);
        }

        /// <summary> Return the output value of the first connected port. Returns null if none found or invalid.</summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public object GetInputValue()
        {
            var connectedPort = Connection;
            if (connectedPort == null) return null;
            return connectedPort.GetOutputValue();
        }

        /// <summary> Return the first connected value for one graph execution context. </summary>
        public object GetInputValue(GraphExecutionContext context)
        {
            var connectedPort = Connection;
            if (connectedPort == null) return null;
            return connectedPort.GetOutputValue(context);
        }

        /// <summary> Return the output values of all connected ports. </summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public object[] GetInputValues()
        {
            var objs = new object[ConnectionCount];
            for (var i = 0; i < ConnectionCount; i++)
            {
                var connectedPort = _connections[i].Port;
                if (connectedPort == null)
                {
                    // if we happen to find a null port, remove it and look again
                    _connections.RemoveAt(i);
                    i--;
                    continue;
                }

                objs[i] = connectedPort.GetOutputValue();
            }

            return objs;
        }

        /// <summary> Return the output value of the first connected port. Returns null if none found or invalid. </summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public T GetInputValue<T>()
        {
            var obj = GetInputValue();
            return obj is T ? (T) obj : default(T);
        }

        public T GetInputValue<T>(GraphExecutionContext context)
        {
            var obj = GetInputValue(context);
            return obj is T ? (T) obj : default(T);
        }

        /// <summary> Return the output values of all connected ports. </summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public T[] GetInputValues<T>()
        {
            var objs = GetInputValues();
            var ts = new T[objs.Length];
            for (var i = 0; i < objs.Length; i++)
            {
                if (objs[i] is T) ts[i] = (T) objs[i];
            }

            return ts;
        }

        /// <summary> Return true if port is connected and has a valid input. </summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public bool TryGetInputValue<T>(out T value)
        {
            var obj = GetInputValue();
            if (obj is T)
            {
                value = (T) obj;
                return true;
            }
            else
            {
                value = default(T);
                return false;
            }
        }

        /// <summary> Return the sum of all inputs. </summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public float GetInputSum(float fallback)
        {
            var objs = GetInputValues();
            if (objs.Length == 0) return fallback;
            float result = 0;
            for (var i = 0; i < objs.Length; i++)
            {
                if (objs[i] is float) result += (float) objs[i];
            }

            return result;
        }

        /// <summary> Return the sum of all inputs. </summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public int GetInputSum(int fallback)
        {
            var objs = GetInputValues();
            if (objs.Length == 0) return fallback;
            var result = 0;
            for (var i = 0; i < objs.Length; i++)
            {
                if (objs[i] is int) result += (int) objs[i];
            }

            return result;
        }

        /// <summary> Connect this <see cref="NodePort"/> to another </summary>
        /// <param name="port">The <see cref="NodePort"/> to connect to</param>
        public void Connect(NodePort port)
        {
            if (_connections == null) _connections = new List<PortConnection>();
            if (port == null)
            {
                Debug.LogWarning("Cannot connect to null port");
                return;
            }

            if (port == this)
            {
                Debug.LogWarning("Cannot connect port to self.");
                return;
            }

            if (IsConnectedTo(port))
            {
                Debug.LogWarning("Port already connected. ");
                return;
            }

            if (Direction == port.Direction)
            {
                Debug.LogWarning("Cannot connect two " + (Direction == IO.Input ? "input" : "output") + " connections");
                return;
            }
            #if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(Node, "Connect Port");
            UnityEditor.Undo.RecordObject(port.Node, "Connect Port");
            #endif
            if (port.ConnectionType == Node.ConnectionType.Override && port.ConnectionCount != 0)
            {
                port.ClearConnections();
            }

            if (ConnectionType == Node.ConnectionType.Override && ConnectionCount != 0)
            {
                ClearConnections();
            }

            _connections.Add(new PortConnection(port));
            if (port._connections == null) port._connections = new List<PortConnection>();
            if (!port.IsConnectedTo(this)) port._connections.Add(new PortConnection(this));
            Node.OnCreateConnection(this, port);
            port.Node.OnCreateConnection(this, port);
        }

        public List<NodePort> GetConnections()
        {
            var result = new List<NodePort>();
            for (var i = 0; i < _connections.Count; i++)
            {
                var port = GetConnection(i);
                if (port != null) result.Add(port);
            }

            return result;
        }

        public NodePort GetConnection(int i)
        {
            //If the connection is broken for some reason, remove it.
            if (_connections[i].Node is null || string.IsNullOrEmpty(_connections[i].FieldName))
            {
                _connections.RemoveAt(i);
                return null;
            }

            var port = _connections[i].Node.GetPort(_connections[i].FieldName);
            if (port == null)
            {
                _connections.RemoveAt(i);
                return null;
            }

            return port;
        }

        /// <summary> Get index of the connection connecting this and specified ports </summary>
        public int GetConnectionIndex(NodePort port)
        {
            for (var i = 0; i < ConnectionCount; i++)
            {
                if (_connections[i].Port == port) return i;
            }

            return -1;
        }

        public bool IsConnectedTo(NodePort port)
        {
            for (var i = 0; i < _connections.Count; i++)
            {
                if (_connections[i].Port == port) return true;
            }

            return false;
        }

        /// <summary> Returns true if this port can connect to specified port </summary>
        public bool CanConnectTo(NodePort port)
        {
            // Figure out which is input and which is output
            NodePort input = null, output = null;
            if (IsInput) input = this;
            else output = this;
            if (port.IsInput) input = port;
            else output = port;
            // If there isn't one of each, they can't connect
            if (input == null || output == null) return false;
            // Check input type constraints
            if (input.TypeConstraint == Node.TypeConstraint.Inherited &&
                !input.ValueType.IsAssignableFrom(output.ValueType)) return false;
            if (input.TypeConstraint == Node.TypeConstraint.Strict && input.ValueType != output.ValueType)
                return false;
            if (input.TypeConstraint == Node.TypeConstraint.InheritedInverse &&
                !output.ValueType.IsAssignableFrom(input.ValueType)) return false;
            if (input.TypeConstraint == Node.TypeConstraint.InheritedAny &&
                !input.ValueType.IsAssignableFrom(output.ValueType) &&
                !output.ValueType.IsAssignableFrom(input.ValueType)) return false;
            // Check output type constraints
            if (output.TypeConstraint == Node.TypeConstraint.Inherited &&
                !input.ValueType.IsAssignableFrom(output.ValueType)) return false;
            if (output.TypeConstraint == Node.TypeConstraint.Strict &&
                input.ValueType != output.ValueType) return false;
            if (output.TypeConstraint == Node.TypeConstraint.InheritedInverse &&
                !output.ValueType.IsAssignableFrom(input.ValueType)) return false;
            if (output.TypeConstraint == Node.TypeConstraint.InheritedAny &&
                !input.ValueType.IsAssignableFrom(output.ValueType) &&
                !output.ValueType.IsAssignableFrom(input.ValueType)) return false;
            // Success
            return true;
        }

        /// <summary> Disconnect this port from another port </summary>
        public void Disconnect(NodePort port)
        {
            // Remove this ports connection to the other
            for (var i = _connections.Count - 1; i >= 0; i--)
            {
                if (_connections[i].Port == port)
                {
                    _connections.RemoveAt(i);
                }
            }

            if (port != null)
            {
                // Remove the other ports connection to this port
                for (var i = 0; i < port._connections.Count; i++)
                {
                    if (port._connections[i].Port == this)
                    {
                        port._connections.RemoveAt(i);
                        // Trigger OnRemoveConnection from this side port
                        port.Node.OnRemoveConnection(port);
                    }
                }
            }

            // Trigger OnRemoveConnection
            Node.OnRemoveConnection(this);
        }

        /// <summary> Disconnect this port from another port </summary>
        public void Disconnect(int i)
        {
            // Remove the other ports connection to this port
            var otherPort = _connections[i].Port;
            if (otherPort != null)
            {
                otherPort._connections.RemoveAll(it => { return it.Port == this; });
            }

            // Remove this ports connection to the other
            _connections.RemoveAt(i);

            // Trigger OnRemoveConnection
            Node.OnRemoveConnection(this);
            if (otherPort != null) otherPort.Node.OnRemoveConnection(otherPort);
        }

        public void ClearConnections()
        {
            while (_connections.Count > 0)
            {
                Disconnect(_connections[0].Port);
            }
        }

        /// <summary> Get reroute points for a given connection. This is used for organization </summary>
        public List<Vector2> GetReroutePoints(int index)
        {
            return _connections[index].ReroutePoints;
        }

        /// <summary> Swap connections with another node </summary>
        public void SwapConnections(NodePort targetPort)
        {
            var aConnectionCount = _connections.Count;
            var bConnectionCount = targetPort._connections.Count;

            var portConnections = new List<NodePort>();
            var targetPortConnections = new List<NodePort>();

            // Cache port connections
            for (var i = 0; i < aConnectionCount; i++)
                portConnections.Add(_connections[i].Port);

            // Cache target port connections
            for (var i = 0; i < bConnectionCount; i++)
                targetPortConnections.Add(targetPort._connections[i].Port);

            ClearConnections();
            targetPort.ClearConnections();

            // Add port connections to targetPort
            for (var i = 0; i < portConnections.Count; i++)
                targetPort.Connect(portConnections[i]);

            // Add target port connections to this one
            for (var i = 0; i < targetPortConnections.Count; i++)
                Connect(targetPortConnections[i]);
        }

        /// <summary> Copy all connections pointing to a node and add them to this one </summary>
        public void AddConnections(NodePort targetPort)
        {
            var connectionCount = targetPort.ConnectionCount;
            for (var i = 0; i < connectionCount; i++)
            {
                var connection = targetPort._connections[i];
                var otherPort = connection.Port;
                Connect(otherPort);
            }
        }

        /// <summary> Move all connections pointing to this node, to another node </summary>
        public void MoveConnections(NodePort targetPort)
        {
            var connectionCount = _connections.Count;

            // Add connections to target port
            for (var i = 0; i < connectionCount; i++)
            {
                var connection = targetPort._connections[i];
                var otherPort = connection.Port;
                Connect(otherPort);
            }

            ClearConnections();
        }

        /// <summary> Swap connected nodes from the old list with nodes from the new list </summary>
        public void Redirect(List<Node> oldNodes, List<Node> newNodes)
        {
            foreach (var connection in _connections)
            {
                var index = oldNodes.IndexOf(connection.Node);
                if (index >= 0) connection.Node = newNodes[index];
            }
        }

        [Serializable]
        private class PortConnection
        {
            [SerializeField] public string FieldName;
            [SerializeField] public Node Node;

            public NodePort Port => _port != null ? _port : _port = GetPort();

            [NonSerialized] private NodePort _port;

            /// <summary> Extra connection path points for organization </summary>
            [SerializeField] public List<Vector2> ReroutePoints = new List<Vector2>();

            public PortConnection(NodePort port)
            {
                _port = port;
                Node = port.Node;
                FieldName = port.FieldName;
            }

            /// <summary> Returns the port that this <see cref="PortConnection"/> points to </summary>
            private NodePort GetPort()
            {
                if (Node == null || string.IsNullOrEmpty(FieldName)) return null;
                return Node.GetPort(FieldName);
            }
        }
    }
}