using System;
using System.Collections.Generic;

namespace Knot
{
    /// <summary>Mutable per-execution values created from a Blackboard definition.</summary>
    public sealed class BlackboardInstance
    {
        private readonly BlackboardRuntimeValue[] _values;
        private readonly string[] _ids;
        private readonly Dictionary<string, int> _indicesById;
        private readonly Dictionary<string, int> _indicesByName;

        public event Action<string> ValueChanged;

        internal BlackboardInstance(BlackboardDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            var definitions = definition.Variables;
            _values = new BlackboardRuntimeValue[definitions.Count];
            _ids = new string[definitions.Count];
            _indicesById = new Dictionary<string, int>(definitions.Count, StringComparer.Ordinal);
            _indicesByName = new Dictionary<string, int>(
                definitions.Count, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < definitions.Count; i++)
            {
                var variable = definitions[i];
                if (variable == null)
                {
                    throw new InvalidOperationException($"Blackboard variable at index {i} is null.");
                }

                if (!_indicesById.TryAdd(variable.Id, i))
                {
                    throw new InvalidOperationException(
                        $"Blackboard contains duplicate variable id '{variable.Id}'.");
                }

                if (!_indicesByName.TryAdd(variable.Name, i))
                {
                    throw new InvalidOperationException(
                        $"Blackboard contains duplicate variable name '{variable.Name}'.");
                }

                _values[i] = variable.CreateRuntimeValue();
                _ids[i] = variable.Id;
            }
        }

        public T Get<T>(string id)
        {
            if (!TryGet(id, out T value))
            {
                throw CreateLookupException<T>(id);
            }

            return value;
        }

        public T GetByName<T>(string name)
        {
            if (!TryGetByName(name, out T value))
            {
                throw new KeyNotFoundException(
                    $"Blackboard variable named '{name}' with type {typeof(T).FullName} was not found.");
            }

            return value;
        }

        public bool TryGet<T>(string id, out T value)
        {
            return TryGet(_indicesById, id, out value);
        }

        public bool TryGetByName<T>(string name, out T value)
        {
            return TryGet(_indicesByName, name, out value);
        }

        public void Set<T>(string id, T value)
        {
            if (!_indicesById.TryGetValue(id, out var index))
            {
                throw new KeyNotFoundException($"Blackboard variable id '{id}' was not found.");
            }

            if (!(_values[index] is BlackboardRuntimeValue<T> typedValue))
            {
                throw new InvalidOperationException(
                    $"Blackboard variable '{id}' stores {_values[index].ValueType.FullName}, " +
                    $"not {typeof(T).FullName}.");
            }

            if (EqualityComparer<T>.Default.Equals(typedValue.Value, value)) return;

            typedValue.Value = value;
            ValueChanged?.Invoke(id);
        }

        public bool TrySet<T>(string id, T value)
        {
            if (!_indicesById.TryGetValue(id, out var index)) return false;
            return TrySetValueAt(index, id, value);
        }

        public void SetByName<T>(string name, T value)
        {
            if (!TrySetByName(name, value))
            {
                throw CreateNameLookupException<T>(name);
            }
        }

        public bool TrySetByName<T>(string name, T value)
        {
            if (name == null || !_indicesByName.TryGetValue(name, out var index)) return false;
            return TrySetValueAt(index, name, value);
        }

        public bool TryGetBoxed(string id, out object value)
        {
            if (id != null && _indicesById.TryGetValue(id, out var index))
            {
                value = _values[index].GetBoxed();
                return true;
            }

            value = null;
            return false;
        }

        public bool TrySetBoxed(string id, object value)
        {
            if (id == null || !_indicesById.TryGetValue(id, out var index)) return false;
            if (!_values[index].TrySetBoxed(value, out var changed)) return false;
            if (changed) ValueChanged?.Invoke(id);
            return true;
        }

        public void Reset()
        {
            for (var i = 0; i < _values.Length; i++)
            {
                _values[i].Reset();
                ValueChanged?.Invoke(_ids[i]);
            }
        }

        private bool TryGet<T>(
            Dictionary<string, int> indices,
            string key,
            out T value)
        {
            if (key != null &&
                indices.TryGetValue(key, out var index) &&
                _values[index] is BlackboardRuntimeValue<T> typedValue)
            {
                value = typedValue.Value;
                return true;
            }

            value = default(T);
            return false;
        }

        private bool TrySetValueAt<T>(int index, string key, T value)
        {
            if (!(_values[index] is BlackboardRuntimeValue<T> typedValue)) return false;
            if (EqualityComparer<T>.Default.Equals(typedValue.Value, value)) return true;

            typedValue.Value = value;
            ValueChanged?.Invoke(key);
            return true;
        }

        private void SetValueAt<T>(int index, string key, T value)
        {
            if (!TrySetValueAt(index, key, value))
            {
                throw new InvalidOperationException(
                    $"Blackboard variable '{key}' stores {_values[index].ValueType.FullName}, " +
                    $"not {typeof(T).FullName}.");
            }
        }

        private Exception CreateLookupException<T>(string id)
        {
            if (!_indicesById.TryGetValue(id, out var index))
            {
                return new KeyNotFoundException($"Blackboard variable id '{id}' was not found.");
            }

            return new InvalidOperationException(
                $"Blackboard variable '{id}' stores {_values[index].ValueType.FullName}, " +
                $"not {typeof(T).FullName}.");
        }

        private Exception CreateNameLookupException<T>(string name)
        {
            if (name == null || !_indicesByName.TryGetValue(name, out var index))
            {
                return new KeyNotFoundException(
                    $"Blackboard variable named '{name}' with type {typeof(T).FullName} was not found.");
            }

            return new InvalidOperationException(
                $"Blackboard variable '{name}' stores {_values[index].ValueType.FullName}, " +
                $"not {typeof(T).FullName}.");
        }
    }
}