using System;

namespace XNode
{
    [Serializable]
    public sealed class BoolVariable : BlackboardVariable<bool> { }

    [CreateNodeMenu("Blackboard/Get/Bool")]
    public sealed class GetBool : GetBlackboardVariableNode<bool> { }
}
