using System;
using UnityEngine;

namespace Knot
{
    /// <summary>Rename-safe reference limited to Blackboard variables of type <typeparamref name="T"/>.</summary>
    [Serializable]
    public struct BlackboardVariableReference<T>
    {
        [SerializeField] private string _variableId;

        public string VariableId => _variableId;
        public bool IsAssigned => !string.IsNullOrEmpty(_variableId);

        public BlackboardVariableReference(string variableId)
        {
            _variableId = variableId;
        }
    }

    [Serializable]
    public struct BlackboardVariableReference
    {
        [SerializeField] private string _variableId;

        public const string VariableIdField = nameof(_variableId);

        public string VariableId => _variableId;
        public bool IsAssigned => !string.IsNullOrEmpty(_variableId);

        public BlackboardVariableReference(string variableId)
        {
            _variableId = variableId;
        }
    }
}