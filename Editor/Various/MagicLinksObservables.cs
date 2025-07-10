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
    
    public class MagicListVariableObservable<T>
    {
        private readonly List<T> _buffer = new List<T>();

        public IReadOnlyList<T> Value => _buffer;

        public event Action<List<T>> OnValueChanged;
        public event Action<T> OnItemAdded;
        public event Action<T> OnItemRemoved;
        public event Action OnCleared;

        public void Add(T item)
        {
            _buffer.Add(item);
            OnItemAdded?.Invoke(item);
            NotifyValueChanged();
        }

        public bool Remove(T item)
        {
            bool removed = _buffer.Remove(item);
            if (removed)
            {
                OnItemRemoved?.Invoke(item);
                NotifyValueChanged();
            }
            return removed;
        }

        public void Clear()
        {
            if (_buffer.Count == 0) return;

            _buffer.Clear();
            OnCleared?.Invoke();
            NotifyValueChanged();
        }

        private void NotifyValueChanged()
        {
            OnValueChanged?.Invoke(new List<T>(_buffer));
        }
    }

}