using System;

namespace Knot
{
    [Serializable]
    public sealed class StringVariable : BlackboardVariable<string> { }

    [CreateNodeMenu("Blackboard/Get/String")]
    public sealed class GetString : GetBlackboardVariableNode<string> { }
}
