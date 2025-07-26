using System;
using System.Collections.Generic;
using System.Linq;

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

    // Expose une vue en lecture seule
    public IReadOnlyList<T> Value => _buffer.AsReadOnly();

    // Events
    public event Action<List<T>> OnValueChanged;
    public event Action<T> OnItemAdded;
    public event Action<T> OnItemRemoved;
    public event Action<T> OnItemChanged;
    public event Action OnCleared;

    // Ajout
    public void Add(T item)
    {
        _buffer.Add(item);
        OnItemAdded?.Invoke(item);
        NotifyValueChanged();
    }

    // Suppression
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

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _buffer.Count) return;
        var item = _buffer[index];
        _buffer.RemoveAt(index);
        OnItemRemoved?.Invoke(item);
        NotifyValueChanged();
    }

    public void Clear()
    {
        if (_buffer.Count == 0) return;
        _buffer.Clear();
        OnCleared?.Invoke();
        NotifyValueChanged();
    }

    public void SetAt(int index, T newItem)
    {
        if (index < 0 || index >= _buffer.Count) return;
        _buffer[index] = newItem;
        OnItemChanged?.Invoke(newItem);
        NotifyValueChanged();
    }

    public void ModifyAt(int index, Action<T> update)
    {
        if (index < 0 || index >= _buffer.Count) return;
        update(_buffer[index]);
        OnItemChanged?.Invoke(_buffer[index]);
        NotifyValueChanged();
    }

    public void Modify(Predicate<T> match, Action<T> update)
    {
        var item = _buffer.FirstOrDefault(i => match(i));
        if (item == null) return;
        update(item);
        OnItemChanged?.Invoke(item);
        NotifyValueChanged();
    }

    private void NotifyValueChanged()
    {
        OnValueChanged?.Invoke(new List<T>(_buffer));
    }
}


}