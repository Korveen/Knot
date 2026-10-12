using System;
using System.Collections.Generic;
using UnityEngine;

namespace XNode
{
    /// <summary>Per-runner override of one Blackboard variable default.</summary>
    [Serializable]
    public sealed class BlackboardOverride
    {
        [SerializeField] private string _variableId;
        [SerializeField] private bool _enabled = true;
        [SerializeReference] private BlackboardVariable _value;

        public string VariableId => _variableId;
        public bool Enabled => _enabled;
        public BlackboardVariable Value => _value;

        public BlackboardOverride()
        {
        }

        public BlackboardOverride(BlackboardVariable definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            _variableId = definition.Id;
            _enabled = true;
            _value = definition.CloneDefinition();
        }

        internal void SetEnabled(bool isEnabled) => _enabled = isEnabled;

        internal void ReplaceValue(BlackboardVariable definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            _variableId = definition.Id;
            _value = definition.CloneDefinition();
        }
    }

    /// <summary>Serialized instance defaults applied on context create and Blackboard reset.</summary>
    [Serializable]
    public sealed class BlackboardOverrideSet
    {
        [SerializeField] private List<BlackboardOverride> _overrides = new List<BlackboardOverride>();

        public IReadOnlyList<BlackboardOverride> Overrides => _overrides;

        public bool TryGet(string variableId, out BlackboardOverride overrideValue)
        {
            var index = IndexOf(variableId);
            if (index >= 0)
            {
                overrideValue = _overrides[index];
                return true;
            }

            overrideValue = null;
            return false;
        }

        public BlackboardOverride Enable(BlackboardVariable definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            var index = IndexOf(definition.Id);
            if (index >= 0)
            {
                var existing = _overrides[index];
                if (existing.Value == null || existing.Value.ValueType != definition.ValueType)
                {
                    existing.ReplaceValue(definition);
                }

                existing.SetEnabled(true);
                return existing;
            }

            var created = new BlackboardOverride(definition);
            _overrides.Add(created);
            return created;
        }

        public void Disable(string variableId)
        {
            var index = IndexOf(variableId);
            if (index >= 0) _overrides[index].SetEnabled(false);
        }

        public bool Remove(string variableId)
        {
            var index = IndexOf(variableId);
            if (index < 0) return false;
            _overrides.RemoveAt(index);
            return true;
        }

        public void Set<T>(BlackboardVariable definition, T value)
        {
            var overrideValue = Enable(definition);
            overrideValue.Value.SetBoxedDefault(value);
        }

        public void Apply(BlackboardInstance instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            for (var i = 0; i < _overrides.Count; i++)
            {
                var overrideValue = _overrides[i];
                if (overrideValue == null ||
                    !overrideValue.Enabled ||
                    overrideValue.Value == null)
                {
                    continue;
                }

                instance.TrySetBoxed(overrideValue.VariableId, overrideValue.Value.GetBoxedDefault());
            }
        }

        private int IndexOf(string variableId)
        {
            if (string.IsNullOrEmpty(variableId)) return -1;
            for (var i = 0; i < _overrides.Count; i++)
            {
                if (_overrides[i] != null && _overrides[i].VariableId == variableId) return i;
            }

            return -1;
        }
    }
}