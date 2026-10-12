using UnityEngine;
using XNode;
using XNodeEditor.UI;

namespace XNodeEditor
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