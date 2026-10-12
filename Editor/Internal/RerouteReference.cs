using UnityEngine;

namespace Knot.Editor.Internal
{
    public struct RerouteReference
    {
        public readonly Knot.NodePort Port;
        public readonly int ConnectionIndex;
        public readonly int PointIndex;

        public RerouteReference(Knot.NodePort port, int connectionIndex, int pointIndex)
        {
            Port = port;
            ConnectionIndex = connectionIndex;
            PointIndex = pointIndex;
        }

        public void InsertPoint(Vector2 pos)
        {
            Port.GetReroutePoints(ConnectionIndex).Insert(PointIndex, pos);
        }

        public void SetPoint(Vector2 pos)
        {
            Port.GetReroutePoints(ConnectionIndex)[PointIndex] = pos;
        }

        public void RemovePoint()
        {
            Port.GetReroutePoints(ConnectionIndex).RemoveAt(PointIndex);
        }

        public Vector2 GetPoint()
        {
            return Port.GetReroutePoints(ConnectionIndex)[PointIndex];
        }
    }
}