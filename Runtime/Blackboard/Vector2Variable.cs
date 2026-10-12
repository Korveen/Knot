using System;
using UnityEngine;

namespace Knot
{
    [Serializable]
    public sealed class Vector2Variable : BlackboardVariable<Vector2> { }

    [CreateNodeMenu("Blackboard/Get/Vector2")]
    public sealed class GetVector2 : GetBlackboardVariableNode<Vector2> { }
}
