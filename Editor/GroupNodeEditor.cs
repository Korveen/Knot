using UnityEngine;
using Knot;
using Knot.Editor.UI;

namespace Knot.Editor
{
    [CustomNodeEditor(typeof(GroupNode))]
    public class GroupNodeEditor : NodeEditor
    {
        public override int GetWidth()
        {
            var group = Target as GroupNode;
            return group != null ? group.Width : 400;
        }

        protected override Color GetTint()
        {
            var group = Target as GroupNode;
            return group != null ? group.Color : base.GetTint();
        }

        public override void BuildHeader(NodeView view)
        {
            base.BuildHeader(view);
            view.AddToClassList("group-view");
        }

        public override void BuildBody(NodeView view)
        {
            if (view is GroupView groupView) groupView.ApplyGroupSize();
        }
    }
}