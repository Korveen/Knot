using System;
using UnityEngine;

namespace XNode
{
    [Serializable]
    public sealed class QuaternionVariable : BlackboardVariable<Quaternion> { }

    [CreateNodeMenu("Blackboard/Get/Quaternion")]
    public sealed class GetQuaternion : GetBlackboardVariableNode<Quaternion> { }
}
