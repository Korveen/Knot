using System;

namespace XNode
{
    [Serializable]
    public sealed class FloatVariable : BlackboardVariable<float> { }

    [CreateNodeMenu("Blackboard/Get/Float")]
    public sealed class GetFloat : GetBlackboardVariableNode<float> { }
}
