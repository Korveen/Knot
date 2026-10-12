using System;

namespace Knot
{
    [Serializable]
    public sealed class IntVariable : BlackboardVariable<int> { }

    [CreateNodeMenu("Blackboard/Get/Int")]
    public sealed class GetInt : GetBlackboardVariableNode<int> { }
}
