using UnityEngine;
using UnityEngine.UIElements;
using Knot;

namespace Knot.Editor.UI
{
    public sealed class PortView : VisualElement
    {
        public NodePort Port { get; }
        public NodeView NodeView { get; }

        public Vector2 LocalOffsetOnNode;

        private static readonly Color ConnectedBorder = new Color(0.102f, 0.102f, 0.102f, 1f);
        private static readonly Color SlotBorder = new Color(0.533f, 0.533f, 0.533f, 1f);
        private Color _restingBorder = SlotBorder;

        public PortView(NodePort port, NodeView nodeView)
        {
            Port = port;
            NodeView = nodeView;
            AddToClassList("port-view");
            AddToClassList(port != null && port.IsOutput ? "output" : "input");
            pickingMode = PickingMode.Position;
            tooltip = "";
            RegisterCallback<GeometryChangedEvent>(_ => CacheOffset());
        }

        public void ApplyColor(Color color)
        {
            var slot = ClassListContains("port-slot");
            var connected = !slot && Port != null && Port.IsConnected;
            style.backgroundColor = connected ? color : Color.clear;
            if (slot) _restingBorder = SlotBorder;
            else if (connected) _restingBorder = ConnectedBorder;
            else _restingBorder = color;
            if (!ClassListContains("hovered")) ApplyBorder(_restingBorder);
        }

        public void SetHovered(bool hovered)
        {
            EnableInClassList("hovered", hovered);
            ApplyBorder(hovered ? Color.white : _restingBorder);
        }

        private void ApplyBorder(Color color)
        {
            style.borderTopColor = color;
            style.borderRightColor = color;
            style.borderBottomColor = color;
            style.borderLeftColor = color;
        }

        public void ApplyTooltip(string text)
        {
            tooltip = text ?? "";
        }

        public bool HasValidLayout
        {
            get
            {
                var world = worldBound;
                return world.width >= 2f && world.height >= 2f;
            }
        }

        public void CacheOffset()
        {
            if (NodeView == null || !HasValidLayout) return;
            LocalOffsetOnNode = NodeView.WorldToLocal(worldBound.center);
        }

        public Vector2 CenterIn(VisualElement space)
        {
            if (space == null) return Vector2.zero;
            if (HasValidLayout) return space.WorldToLocal(worldBound.center);
            if (NodeView == null) return Vector2.zero;
            var localOnNode = LocalOffsetOnNode;
            if (localOnNode.sqrMagnitude < 0.01f)
            {
                var width = NodeView.resolvedStyle.width > 1f ? NodeView.resolvedStyle.width : 280f;
                localOnNode = Port != null && Port.IsOutput
                    ? new Vector2(width, 22f)
                    : new Vector2(0f, 22f);
            }

            return space.WorldToLocal(NodeView.LocalToWorld(localOnNode));
        }
    }
}