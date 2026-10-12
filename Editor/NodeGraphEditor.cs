using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Knot.Editor
{
    /// <summary> Base class to derive custom Node Graph editors from. Use this to override how graphs are drawn in the editor. </summary>
    [CustomNodeGraphEditor(typeof(Knot.NodeGraph))]
    public class NodeGraphEditor : Internal.NodeEditorBase<NodeGraphEditor,
        NodeGraphEditor.CustomNodeGraphEditorAttribute, Knot.NodeGraph>
    {
        [Obsolete("Use window.position instead")]
        public Rect Position
        {
            get => Window.position;
            set => Window.position = value;
        }

        /// <summary> Are we currently renaming a node? </summary>
        protected bool IsRenaming;

        public virtual void BuildToolbar(VisualElement slot)
        {
        }

        public virtual void AddItemsToMenu(GenericMenu menu)
        {
        }

        public virtual void AddItemsToNodeMenu(DropdownMenu menu, Knot.Node node)
        {
        }

        public virtual void BuildOverlay(VisualElement overlay)
        {
        }

        public virtual void ApplyStyles(VisualElement root)
        {
        }

        public virtual void OnNodesMoved()
        {
        }

        public virtual bool TryGetNodeIndex(Knot.Node node, out int index)
        {
            index = -1;
            return false;
        }

        /// <summary> Called when opened by NodeEditorWindow </summary>
        public virtual void OnOpen()
        {
        }

        /// <summary> Called when NodeEditorWindow gains focus </summary>
        public virtual void OnWindowFocus()
        {
        }

        /// <summary> Called when NodeEditorWindow loses focus </summary>
        public virtual void OnWindowFocusLost()
        {
        }

        public virtual Texture2D GetGridTexture()
        {
            return NodeEditorPreferences.GetGridTexture();
        }

        public virtual Texture2D GetSecondaryGridTexture()
        {
            return NodeEditorPreferences.GetCrossTexture();
        }

        /// <summary> Return default settings for this graph type. This is the settings the user will load if no previous settings have been saved. </summary>
        public virtual NodeEditorPreferences.GraphTypeSettings GetDefaultPreferences()
        {
            return new NodeEditorPreferences.GraphTypeSettings();
        }

        /// <summary> Returns context node menu path. Null or empty strings for hidden nodes. </summary>
        public virtual string GetNodeMenuName(Type type)
        {
            //Check if type has the CreateNodeMenuAttribute
            if (NodeEditorUtilities.GetAttrib(type,
                    out Knot.Node.CreateNodeMenuAttribute attrib)) // Return custom path
                return attrib.MenuName;
            else // Return generated path
                return NodeEditorUtilities.NodeDefaultPath(type);
        }

        /// <summary> The order by which the menu items are displayed. </summary>
        public virtual int GetNodeMenuOrder(Type type)
        {
            //Check if type has the CreateNodeMenuAttribute
            if (NodeEditorUtilities.GetAttrib(type,
                    out Knot.Node.CreateNodeMenuAttribute attrib)) // Return custom path
                return attrib.Order;
            else
                return 0;
        }

        /// <summary>
        /// Called before connecting two ports in the graph view to see if the output port is compatible with the input port
        /// </summary>
        public virtual bool CanConnect(Knot.NodePort output, Knot.NodePort input)
        {
            return output.CanConnectTo(input);
        }

        /// <summary>
        /// Prefer this input when a connection is dropped on the node body instead of a specific port.
        /// </summary>
        public virtual Knot.NodePort GetCompatibleInput(Knot.NodePort output, Knot.Node node)
        {
            if (output == null || node == null || output.Node == node) return null;
            foreach (var input in node.Inputs)
            {
                if (CanConnect(output, input) && !output.IsConnectedTo(input)) return input;
            }

            return null;
        }

        /// <summary>
        /// Prefer this output when a reverse-dragged input is dropped on the node body instead of a specific port.
        /// </summary>
        public virtual Knot.NodePort GetCompatibleOutput(Knot.NodePort input, Knot.Node node)
        {
            if (input == null || node == null || input.Node == node) return null;
            foreach (var output in node.Outputs)
            {
                if (CanConnect(output, input) && !output.IsConnectedTo(input)) return output;
            }

            return null;
        }

        /// <summary> Returned gradient is used to color noodles </summary>
        /// <param name="output"> The output this noodle comes from. Never null. </param>
        /// <param name="input"> The output this noodle comes from. Can be null if we are dragging the noodle. </param>
        public virtual Gradient GetNoodleGradient(Knot.NodePort output, Knot.NodePort input)
        {
            var grad = new Gradient();

            // If dragging the noodle, draw solid, slightly transparent
            if (input == null)
            {
                var a = GetPortColor(output);
                grad.SetKeys(
                    new GradientColorKey[] {new GradientColorKey(a, 0f)},
                    new GradientAlphaKey[] {new GradientAlphaKey(0.6f, 0f)}
                );
            }
            // If normal, draw gradient fading from one input color to the other
            else
            {
                var a = GetPortColor(output);
                var b = GetPortColor(input);
                // If any port is hovered, tint white
                if (Window.HoveredPort == output || Window.HoveredPort == input)
                {
                    a = Color.Lerp(a, Color.white, 0.8f);
                    b = Color.Lerp(b, Color.white, 0.8f);
                }

                grad.SetKeys(
                    new GradientColorKey[] {new GradientColorKey(a, 0f), new GradientColorKey(b, 1f)},
                    new GradientAlphaKey[] {new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f)}
                );
            }

            return grad;
        }

        /// <summary> Returned float is used for noodle thickness </summary>
        /// <param name="output"> The output this noodle comes from. Never null. </param>
        /// <param name="input"> The output this noodle comes from. Can be null if we are dragging the noodle. </param>
        public virtual float GetNoodleThickness(Knot.NodePort output, Knot.NodePort input)
        {
            return NodeEditorPreferences.GetFor(this).NoodleThickness;
        }

        /// <summary>
        /// Direction the noodle leaves a port, in GUI space (y+ is down).
        /// Default is left-to-right. Override for vertical graphs.
        /// </summary>
        public virtual Vector2 GetNoodlePortDirection(Knot.NodePort port)
        {
            if (port == null || port.IsOutput) return Vector2.right;
            return Vector2.left;
        }

        public virtual NoodlePath GetNoodlePath(Knot.NodePort output, Knot.NodePort input)
        {
            return NodeEditorPreferences.GetFor(this).NoodlePath;
        }

        public virtual NoodleStroke GetNoodleStroke(Knot.NodePort output, Knot.NodePort input)
        {
            return NodeEditorPreferences.GetFor(this).NoodleStroke;
        }

        public virtual float GetNoodleTension(Knot.NodePort output, Knot.NodePort input)
        {
            var tension = NodeEditorPreferences.GetFor(this).NoodleTension;
            return tension > 0.01f ? tension : 0.45f;
        }

        /// <summary> Returned color is used to color ports </summary>
        public virtual Color GetPortColor(Knot.NodePort port)
        {
            return NodeEditorPreferences.ResolvePortColor(port, NodeEditorPreferences.GetFor(this), false);
        }

        /// <summary>
        /// The returned Style is used to configure the paddings and icon texture of the ports.
        /// Use these properties to customize your port style.
        ///
        /// The properties used is:
        /// <see cref="GUIStyle.padding"/>[Left and Right], <see cref="GUIStyle.normal"/> [Background] = border texture,
        /// and <seealso cref="GUIStyle.active"/> [Background] = dot texture;
        /// </summary>
        /// <param name="port">the owner of the style</param>
        /// <returns></returns>
        public virtual GUIStyle GetPortStyle(Knot.NodePort port)
        {
            if (port.Direction == Knot.NodePort.IO.Input)
                return NodeEditorResources.styles.inputPort;

            return NodeEditorResources.styles.outputPort;
        }

        /// <summary> The returned color is used to color the background of the door.
        /// Usually used for outer edge effect </summary>
        public virtual Color GetPortBackgroundColor(Knot.NodePort port)
        {
            return Color.gray;
        }

        /// <summary> Returns generated color for a type. This color is editable in preferences </summary>
        public virtual Color GetTypeColor(Type type)
        {
            return NodeEditorPreferences.GetTypeColor(type);
        }

        /// <summary> Override to display custom tooltips </summary>
        public virtual string GetPortTooltip(Knot.NodePort port)
        {
            var portType = port.ValueType;
            var tooltip = "";
            tooltip = portType.PrettyName();
            if (port.IsOutput)
            {
                var obj = port.Node.GetValue(port);
                tooltip += " = " + (obj != null ? obj.ToString() : "null");
            }

            return tooltip;
        }

        /// <summary> Deal with objects dropped into the graph through DragAndDrop </summary>
        public virtual void OnDropObjects(UnityEngine.Object[] objects)
        {
            if (GetType() != typeof(NodeGraphEditor)) Debug.Log("No OnDropObjects override defined for " + GetType());
        }

        /// <summary> Create a node and save it in the graph asset </summary>
        public virtual Knot.Node CreateNode(Type type, Vector2 position)
        {
            Undo.RecordObject(Target, "Create Node");
            var node = Target.AddNode(type);
            if (node == null) return null; // handle null nodes to avoid nullref exceptions
            Undo.RegisterCreatedObjectUndo(node, "Create Node");
            node.Position = position;
            if (node.name == null || node.name.Trim() == "") node.name = NodeEditorUtilities.NodeDefaultName(type);
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(Target))) AssetDatabase.AddObjectToAsset(node, Target);
            AssetDatabase.SaveAssets();
            NodeEditorWindow.RepaintAll();
            return node;
        }

        /// <summary> Creates a copy of the original node in the graph </summary>
        public virtual Knot.Node CopyNode(Knot.Node original)
        {
            Undo.RecordObject(Target, "Duplicate Node");
            var node = Target.CopyNode(original);
            Undo.RegisterCreatedObjectUndo(node, "Duplicate Node");
            node.name = original.name;
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(Target))) AssetDatabase.AddObjectToAsset(node, Target);
            AssetDatabase.SaveAssets();
            return node;
        }

        /// <summary> Return false for nodes that can't be removed </summary>
        public virtual bool CanRemove(Knot.Node node)
        {
            // Check graph attributes to see if this node is required
            var graphType = Target.GetType();
            var attribs = Array.ConvertAll(
                graphType.GetCustomAttributes(typeof(Knot.NodeGraph.RequireNodeAttribute), true),
                x => x as Knot.NodeGraph.RequireNodeAttribute);
            if (attribs.Any(x => x.Requires(node.GetType())))
            {
                if (Target.Nodes.Count(x => x.GetType() == node.GetType()) <= 1)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary> Safely remove a node and all its connections. </summary>
        public virtual void RemoveNode(Knot.Node node)
        {
            if (!CanRemove(node)) return;

            // Remove the node
            Undo.RecordObject(node, "Delete Node");
            Undo.RecordObject(Target, "Delete Node");
            foreach (var port in node.Ports)
            foreach (var conn in port.GetConnections())
                Undo.RecordObject(conn.Node, "Delete Node");
            Target.RemoveNode(node);
            Undo.DestroyObjectImmediate(node);
            AssetDatabase.SaveAssets();
        }

        [AttributeUsage(AttributeTargets.Class, Inherited = false)]
        public class CustomNodeGraphEditorAttribute : Attribute, INodeEditorAttrib
        {
            public readonly string EditorPrefsKey;

            private readonly Type _inspectedType;

            /// <summary> Tells a NodeGraphEditor which Graph type it is an editor for </summary>
            /// <param name="inspectedType">Type that this editor can edit</param>
            /// <param name="editorPrefsKey">Define unique key for unique layout settings instance</param>
            public CustomNodeGraphEditorAttribute(Type inspectedType, string editorPrefsKey = "Knot.Settings")
            {
                _inspectedType = inspectedType;
                EditorPrefsKey = editorPrefsKey;
            }

            public Type GetInspectedType()
            {
                return _inspectedType;
            }
        }
    }
}