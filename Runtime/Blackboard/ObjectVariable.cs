using System;

namespace Knot
{
    [Serializable]
    public sealed class ObjectVariable : BlackboardVariable<UnityEngine.Object> { }

    [CreateNodeMenu("Blackboard/Get/Object")]
    public sealed class GetObject : GetBlackboardVariableNode<UnityEngine.Object> { }
}
