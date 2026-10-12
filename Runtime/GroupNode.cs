using System.Collections.Generic;
using UnityEngine;

namespace Knot
{
    [CreateNodeMenu("Group")]
    public class GroupNode : Node, IAuxiliaryGraphNode
    {
        public int Width = 400;
        public int Height = 400;
        public Color Color = new Color(1f, 1f, 1f, 0.1f);

        public override object GetValue(NodePort port)
        {
            return null;
        }

        public List<Node> GetNodes(System.Func<Node, Vector2> sizeOf)
        {
            var result = new List<Node>();
            if (Graph == null || Graph.Nodes == null || sizeOf == null)
            {
                return result;
            }

            var maxX = Position.x + Width;
            var maxY = Position.y + Height + 30f;
            for (var i = 0; i < Graph.Nodes.Count; i++)
            {
                var node = Graph.Nodes[i];
                if (node == null || node == this)
                {
                    continue;
                }

                var size = sizeOf(node);
                var x1 = node.Position.x;
                var y1 = node.Position.y;
                var x2 = x1 + Mathf.Max(1f, size.x);
                var y2 = y1 + Mathf.Max(1f, size.y);
                if (x1 < Position.x || y1 < Position.y)
                {
                    continue;
                }

                if (x2 > maxX || y2 > maxY)
                {
                    continue;
                }

                result.Add(node);
            }

            return result;
        }
    }
}