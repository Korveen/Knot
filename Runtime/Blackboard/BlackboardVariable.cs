using System;
using System.Collections.Generic;
using UnityEngine;

namespace Knot
{
    /// <summary>Serialized definition and default value of one Blackboard variable.</summary>
    [Serializable]
    public abstract class BlackboardVariable
    {
        [HideInInspector] [SerializeField] private string _id = Guid.NewGuid().ToString("N");
        [SerializeField] private string _name = "Variable";

        public const string IdField = nameof(_id);
        public const string NameField = nameof(_name);

        public string Id => _id;
        public string Name => _name;
        public abstract Type ValueType { get; }

        internal void EnsureIdentity()
        {
            if (string.IsNullOrEmpty(_id)) _id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(_name)) _name = "Variable";
        }

        internal void SetName(string value)
        {
            _name = value;
        }

        public abstract object GetBoxedDefault();
        public abstract void SetBoxedDefault(object value);
        public abstract BlackboardVariable CloneDefinition();

        internal abstract BlackboardRuntimeValue CreateRuntimeValue();
        internal abstract bool TryGetDefault<T>(out T value);
    }

    /// <summary>Base class for custom strongly typed Blackboard variables.</summary>
    [Serializable]
    public abstract class BlackboardVariable<T> : BlackboardVariable
    {
        [SerializeField] private T _defaultValue;

        public const string DefaultValueField = nameof(_defaultValue);

        public sealed override Type ValueType => typeof(T);

        public T DefaultValue
        {
            get => _defaultValue;
            set => _defaultValue = value;
        }

        public sealed override object GetBoxedDefault() => _defaultValue;

        public sealed override void SetBoxedDefault(object value)
        {
            if (value is T typedValue)
            {
                _defaultValue = typedValue;
                return;
            }

            if (value == null && default(T) == null)
            {
                _defaultValue = default(T);
                return;
            }

            throw new InvalidOperationException(
                $"Cannot assign {value?.GetType().FullName ?? "null"} to {typeof(T).FullName}.");
        }

        public sealed override BlackboardVariable CloneDefinition()
        {
            var clone = (BlackboardVariable<T>) Activator.CreateInstance(GetType());
            clone.DefaultValue = _defaultValue;
            return clone;
        }

        internal sealed override BlackboardRuntimeValue CreateRuntimeValue()
        {
            return new BlackboardRuntimeValue<T>(_defaultValue);
        }

        internal sealed override bool TryGetDefault<TValue>(out TValue value)
        {
            if (_defaultValue is TValue typedValue)
            {
                value = typedValue;
                return true;
            }

            if ((object) _defaultValue == null && (object) default(TValue) == null)
            {
                value = default(TValue);
                return true;
            }

            value = default(TValue);
            return false;
        }
    }

    internal abstract class BlackboardRuntimeValue
    {
        public abstract Type ValueType { get; }
        public abstract object GetBoxed();
        public abstract bool TrySetBoxed(object value, out bool changed);
        public abstract void Reset();
    }

    internal sealed class BlackboardRuntimeValue<T> : BlackboardRuntimeValue
    {
        private readonly T _defaultValue;
        public T Value;

        public BlackboardRuntimeValue(T defaultValue)
        {
            _defaultValue = defaultValue;
            Value = defaultValue;
        }

        public override Type ValueType => typeof(T);
        public override object GetBoxed() => Value;

        public override bool TrySetBoxed(object value, out bool changed)
        {
            if (value is T typedValue)
            {
                changed = !EqualityComparer<T>.Default.Equals(Value, typedValue);
                Value = typedValue;
                return true;
            }

            if (value == null && default(T) == null)
            {
                changed = !EqualityComparer<T>.Default.Equals(Value, default(T));
                Value = default(T);
                return true;
            }

            changed = false;
            return false;
        }

        public override void Reset()
        {
            Value = _defaultValue;
        }
    }
}