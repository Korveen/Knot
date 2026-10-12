using UnityEngine;

namespace Knot
{
    public abstract class GetBlackboardVariableNode<T> : Node, IAuxiliaryGraphNode
    {
        [SerializeField] private BlackboardVariableReference<T> _variable;
        [Output(ShowBackingValue.Never)] public T Value;

        public BlackboardVariableReference<T> Variable
        {
            get => _variable;
            set => _variable = value;
        }

        public sealed override object GetValue(NodePort port)
        {
            if (port == null || port.FieldName != nameof(Value)) return null;
            return Graph != null &&
                   Graph.BlackboardDefinition.TryGetDefault(_variable.VariableId, out T defaultValue)
                ? defaultValue
                : default;
        }

        public sealed override object GetValue(NodePort port, GraphExecutionContext context)
        {
            if (port == null || port.FieldName != nameof(Value)) return null;
            if (context == null) return GetValue(port);
            return context.Blackboard.TryGet(_variable.VariableId, out T runtimeValue)
                ? runtimeValue
                : default;
        }
    }
}