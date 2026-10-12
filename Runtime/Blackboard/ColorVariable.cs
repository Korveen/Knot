using System;
using UnityEngine;

namespace Knot
{
    [Serializable]
    public sealed class ColorVariable : BlackboardVariable<Color> { }

    [CreateNodeMenu("Blackboard/Get/Color")]
    public sealed class GetColor : GetBlackboardVariableNode<Color> { }
}
