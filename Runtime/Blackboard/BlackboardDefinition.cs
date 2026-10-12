using System;
using System.Collections.Generic;
using UnityEngine;

namespace XNode
{
    /// <summary>Serializable Blackboard schema stored on a graph asset.</summary>
    [Serializable]
    public sealed class BlackboardDefinition : ISerializationCallbackReceiver
    {
        [SerializeReference] private List<BlackboardVariable> _variables =
            new List<BlackboardVariable>();

        public const string VariablesField = nameof(_variables);

        public IReadOnlyList<BlackboardVariable> Variables => _variables;

        public BlackboardInstance CreateInstance()
        {
            EnsureValidIdentities();
            return new BlackboardInstance(this);
        }

        public bool TryGetVariable(string id, out BlackboardVariable variable)
        {
            if (!string.IsNullOrEmpty(id))
            {
                for (var i = 0; i < _variables.Count; i++)
                {
                    var candidate = _variables[i];
                    if (candidate != null && candidate.Id == id)
                    {
                        variable = candidate;
                        return true;
                    }
                }
            }

            variable = null;
            return false;
        }

        public bool TryGetDefault<T>(string id, out T value)
        {
            if (TryGetVariable(id, out var variable))
            {
                return variable.TryGetDefault(out value);
            }

            value = default(T);
            return false;
        }

        public T Add<T>(string variableName = null) where T : BlackboardVariable, new()
        {
            var variable = new T();
            variable.EnsureIdentity();
            variable.SetName(GetUniqueName(
                string.IsNullOrWhiteSpace(variableName) ? variable.ValueType.Name : variableName));
            _variables.Add(variable);
            return variable;
        }

        public bool Remove(string id)
        {
            for (var i = 0; i < _variables.Count; i++)
            {
                if (_variables[i] != null && _variables[i].Id == id)
                {
                    _variables.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public bool Rename(string id, string newName)
        {
            if (!TryGetVariable(id, out var variable)) return false;
            if (string.IsNullOrWhiteSpace(newName)) return false;

            var trimmedName = newName.Trim();
            for (var i = 0; i < _variables.Count; i++)
            {
                var other = _variables[i];
                if (other == null || other == variable) continue;
                if (string.Equals(other.Name, trimmedName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            variable.SetName(trimmedName);
            return true;
        }

        public string GetUniqueName(string requestedName)
        {
            var baseName = string.IsNullOrWhiteSpace(requestedName)
                ? "Variable"
                : requestedName.Trim();
            var candidate = baseName;
            var suffix = 1;

            while (ContainsName(candidate))
            {
                candidate = $"{baseName} {suffix++}";
            }

            return candidate;
        }

        public void OnBeforeSerialize()
        {
            EnsureValidIdentities();
        }

        public void OnAfterDeserialize()
        {
            if (_variables == null) _variables = new List<BlackboardVariable>();
            EnsureValidIdentities();
        }

        private bool ContainsName(string variableName)
        {
            for (var i = 0; i < _variables.Count; i++)
            {
                var variable = _variables[i];
                if (variable != null &&
                    string.Equals(variable.Name, variableName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureValidIdentities()
        {
            if (_variables == null) _variables = new List<BlackboardVariable>();
            for (var i = 0; i < _variables.Count; i++)
            {
                _variables[i]?.EnsureIdentity();
            }
        }
    }
}