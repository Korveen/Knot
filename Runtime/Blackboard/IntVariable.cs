using System;

namespace XNode
{
    [Serializable]
    public sealed class IntVariable : BlackboardVariable<int> { }

    [CreateNodeMenu("Blackboard/Get/Int")]
    public sealed class GetInt : GetBlackboardVariableNode<int> { }
}
