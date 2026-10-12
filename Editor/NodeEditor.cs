using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Knot.Editor
{
    /// <summary> Base class to derive custom Node editors from. Use this to create your own custom inspectors and editors for your nodes. </summary>
    [CustomNodeEditor(typeof(Knot.Node))]
    public class NodeEditor : Internal.NodeEditorBase<NodeEditor, NodeEditor.CustomNodeEditorAttribute, Knot.Node>
    {
        /// <summary> Fires every whenever a node was modified through the editor </summary>
        public static Action<Knot.Node> OnUpdateNode;

        public static readonly Dictionary<Knot.NodePort, Vector2> PortPositions = new Dictionary<Knot.NodePort, Vector2>();

        public virtual void BuildHeader(UI.NodeView view)
        {
            if (view == null) return;
            view.SetTitle(Target != null ? Target.name : "");
            view.SetTint(GetTint());
            var tip = GetHeaderTooltip();
            if (!string.IsNullOrEmpty(tip)) view.tooltip = tip;
        }

        public virtual void BuildBody(UI.NodeView view)
        {
            UI.NodeUiUtility.BindNodeFields(view, SerializedObject);
        }

        public virtual int GetWidth()
        {
            var type = Target.GetType();
            if (type.TryGetAttributeWidth(out var width)) return width;
            else return 280;
        }

        /// <summary> Returns color for target node </summary>
        protected virtual Color GetTint()
        {
            var type = Target.GetType();
            if (type.TryGetAttributeTint(out var color)) return color;
            else return NodeEditorPreferences.GetSettings().TintColor;
        }

        /// <summary> Height of the header used for node dragging. </summary>
        protected virtual float GetHeaderHeight()
        {
            return 30;
        }

        /// <summary> Override to display custom node header tooltips </summary>
        protected virtual string GetHeaderTooltip()
        {
            return null;
        }

        /// <summary> Rename the node asset. This will trigger a reimport of the node. </summary>
        public void Rename(string newName)
        {
            if (newName == null || newName.Trim() == "")
                newName = NodeEditorUtilities.NodeDefaultName(Target.GetType());
            Target.name = newName;
            OnRename();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(Target));
        }

        /// <summary> Called after this node's name has changed. </summary>
        protected virtual void OnRename()
        {
        }

        [AttributeUsage(AttributeTargets.Class)]
        public class CustomNodeEditorAttribute : Attribute, INodeEditorAttrib
        {
            private readonly Type _inspectedType;

            /// <summary> Tells a NodeEditor which Node type it is an editor for </summary>
            /// <param name="inspectedType">Type that this editor can edit</param>
            public CustomNodeEditorAttribute(Type inspectedType)
            {
                _inspectedType = inspectedType;
            }

            public Type GetInspectedType()
            {
                return _inspectedType;
            }
        }
    }
}