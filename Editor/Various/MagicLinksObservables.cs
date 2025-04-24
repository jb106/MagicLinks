using System;

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
                    OnValueChanged?.Invoke(_value);
                }
            }
        }

        public event Action<T> OnValueChanged;

        public MagicVariableObservable() => _value = default;
        public MagicVariableObservable(T initialValue) => _value = initialValue;
    }
}