using System;
using System.Collections.Generic;

namespace MagicLinks.Observables
{
    public class MagicEventObservable<T>
    {
        public event Action<T> OnEventRaised;

        public void Raise(T v)
        {
            OnEventRaised?.Invoke(v);
        }
    }
    
    public class MagicEventVoidObservable
    {
        public event Action OnEventRaised;

        public void Raise()
        {
            OnEventRaised?.Invoke();
        }
    }

    public class MagicVariableObservable<T>
    {
        private T _value;

        public T Value
        {
            get => _value;
            set
            {
                if (!Equals(_value, value))
                {
                    _value = value;
                    NotifyValueChanged();
                }
            }
        }
        
        protected void NotifyValueChanged()
        {
            OnValueChanged?.Invoke(_value);
        }

        public event Action<T> OnValueChanged;

        public MagicVariableObservable() => _value = default;
        public MagicVariableObservable(T initialValue) => _value = initialValue;
    }
    
    public class MagicListVariableObservable<T> : MagicVariableObservable<List<T>>
    {
        public void Add(T item)
        {
            if (Value == null)
                Value = new List<T>();
            Value.Add(item);
            NotifyValueChanged();
        }

        public bool Remove(T item)
        {
            if (Value == null)
                return false;
            bool result = Value.Remove(item);
            if (result)
                NotifyValueChanged();
            return result;
        }

        public void Clear()
        {
            if (Value == null)
                return;
            Value.Clear();
            NotifyValueChanged();
        }
    }
}