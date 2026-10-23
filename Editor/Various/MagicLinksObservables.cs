using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MagicLinks.Network;

namespace MagicLinks.Observables
{
    public class MagicEventObservable<T>
    {
        public event Action<T> OnEventRaised;

        internal IMagicEventSync<T> Sync;

        public void Raise(T v)
        {
            if (Sync != null && Sync.IsActive)
            {
                Sync.RequestRaise(v);
                return;
            }

            RaiseLocal(v);
        }

        internal void RaiseLocal(T v)
        {
            OnEventRaised?.Invoke(v);
        }
    }

    public class MagicEventVoidObservable
    {
        public event Action OnEventRaised;

        internal IMagicEventVoidSync Sync;

        public void Raise()
        {
            if (Sync != null && Sync.IsActive)
            {
                Sync.RequestRaise();
                return;
            }

            RaiseLocal();
        }

        internal void RaiseLocal()
        {
            OnEventRaised?.Invoke();
        }
    }

    public class MagicVariableObservable<T>
    {
        private static readonly EqualityComparer<T> _comparer = EqualityComparer<T>.Default;
        private T _value;
        private readonly T _initialValue;

        internal IMagicVariableSync<T> Sync;

        /// <summary>
        /// Optional rule checked before accepting a write, where the value is committed:
        /// offline, or on the server/host for every peer's writes. A refused write is ignored
        /// (SetAsync / TrySetAsync return Rejected).
        /// </summary>
        public MagicVariableValidator<T> Validator { get; set; }

        /// <summary>
        /// Reading is always local. Writing commits locally when offline or on the server,
        /// and sends a request to the server when connected as a client: the value changes
        /// once the authoritative value comes back.
        /// </summary>
        public T Value
        {
            get => _value;
            set
            {
                if (Sync != null && Sync.IsActive)
                {
                    Sync.RequestSet(value);
                    return;
                }

                TryCommitLocal(value);
            }
        }

        protected void NotifyValueChanged()
        {
            OnValueChanged?.Invoke(_value);
        }

        public event Action<T> OnValueChanged;

        public MagicVariableObservable()
        {
            _value = default;
            _initialValue = default;
        }

        public MagicVariableObservable(T initialValue)
        {
            _value = initialValue;
            _initialValue = initialValue;
        }

        // Reset to the value provided at construction (the inspector "initial value"),
        // or default(T) if no initial value was provided.
        public void Reset() => Value = _initialValue;

        /// <summary>
        /// Same as setting Value, but completes once the authoritative value is applied locally.
        /// Completes immediately when offline or on the server.
        /// </summary>
        public Task<MagicSetResult> SetAsync(T value)
        {
            if (Sync != null && Sync.IsActive)
                return Sync.RequestSetAsync(value);

            return Task.FromResult(TryCommitLocal(value) ? MagicSetResult.Success : MagicSetResult.Rejected);
        }

        /// <summary>
        /// Sets the value only if the current authoritative value equals <paramref name="expected"/>.
        /// The check and the write happen atomically on the server, so when several peers try at
        /// the same time exactly one of them succeeds.
        /// </summary>
        public Task<MagicSetResult> TrySetAsync(T expected, T value)
        {
            if (Sync != null && Sync.IsActive)
                return Sync.RequestTrySetAsync(expected, value);

            if (!_comparer.Equals(_value, expected))
                return Task.FromResult(MagicSetResult.Rejected);

            return Task.FromResult(TryCommitLocal(value) ? MagicSetResult.Success : MagicSetResult.Rejected);
        }

        private bool TryCommitLocal(T value)
        {
            if (MagicLinksNetwork.RunValidator(Validator, MagicLinksNetwork.LocalClientId, _value, value) == false)
                return false;

            Commit(value);
            return true;
        }

        internal void Commit(T value)
        {
            if (!_comparer.Equals(_value, value))
            {
                _value = value;
                NotifyValueChanged();
            }
        }
    }

    public class MagicListVariableObservable<T>
{
    private readonly List<T> _buffer = new List<T>();
    private System.Collections.ObjectModel.ReadOnlyCollection<T> _readOnlyView;

    public IReadOnlyList<T> Value => _readOnlyView ??= _buffer.AsReadOnly();

    // In a network session, every change is sent to the server as an operation and applied
    // when the server sends it back (offline, or on the server, it is applied at once).
    internal IMagicListSync<T> Sync;
    private bool IsSynced => Sync != null && Sync.IsActive;

    // Events
    public event Action<List<T>> OnValueChanged;
    public event Action<T> OnItemAdded;
    public event Action<T> OnItemRemoved;
    public event Action<T> OnItemChanged;
    public event Action OnCleared;

    /// <summary>
    /// Optional rule checked before accepting a change, where the list is committed: offline, or on the
    /// server/host for every peer's changes. A refused change is ignored (the Async versions return Rejected).
    /// ModifyAt / Modify change the item in place, so offline they cannot be validated: prefer SetAt on validated lists.
    /// </summary>
    public MagicListValidator<T> Validator { get; set; }

    private static readonly Task<MagicSetResult> SuccessTask = Task.FromResult(MagicSetResult.Success);
    private static readonly Task<MagicSetResult> RejectedTask = Task.FromResult(MagicSetResult.Rejected);

    public int FindIndex(Predicate<T> match) => _buffer.FindIndex(match);

    // Ajout
    public void Add(T item) => DoAdd(item, false);

    /// <summary>Completes once the server applied (or refused) the change. Immediate offline.</summary>
    public Task<MagicSetResult> AddAsync(T item) => DoAdd(item, true);

    private Task<MagicSetResult> DoAdd(T item, bool wantResult)
    {
        if (IsSynced) return Sync.RequestAdd(item, wantResult);

        return TryApplyLocal(new MagicListChange<T>(MagicListOperation.Add, _buffer.Count, item));
    }

    // Suppression
    // In a network session, returns whether the item exists locally: it is removed when the server confirms.
    public bool Remove(T item)
    {
        int index = _buffer.IndexOf(item);
        if (index < 0) return false;

        Task<MagicSetResult> result = DoRemoveAt(index, false);
        return IsSynced || result.Result == MagicSetResult.Success;
    }

    public Task<MagicSetResult> RemoveAsync(T item)
    {
        int index = _buffer.IndexOf(item);
        return index < 0 ? RejectedTask : DoRemoveAt(index, true);
    }

    public void RemoveAt(int index) => DoRemoveAt(index, false);

    public Task<MagicSetResult> RemoveAtAsync(int index) => DoRemoveAt(index, true);

    private Task<MagicSetResult> DoRemoveAt(int index, bool wantResult)
    {
        if (index < 0 || index >= _buffer.Count) return RejectedTask;

        if (IsSynced) return Sync.RequestRemoveAt(index, _buffer[index], wantResult);

        return TryApplyLocal(new MagicListChange<T>(MagicListOperation.RemoveAt, index, _buffer[index]));
    }

    public void Clear() => DoClear(false);

    public Task<MagicSetResult> ClearAsync() => DoClear(true);

    private Task<MagicSetResult> DoClear(bool wantResult)
    {
        if (_buffer.Count == 0) return SuccessTask;

        if (IsSynced) return Sync.RequestClear(wantResult);

        return TryApplyLocal(new MagicListChange<T>(MagicListOperation.Clear, -1, default));
    }

    public void SetAt(int index, T newItem) => DoSetAt(index, newItem, false);

    public Task<MagicSetResult> SetAtAsync(int index, T newItem) => DoSetAt(index, newItem, true);

    private Task<MagicSetResult> DoSetAt(int index, T newItem, bool wantResult)
    {
        if (index < 0 || index >= _buffer.Count) return RejectedTask;

        if (IsSynced) return Sync.RequestSetAt(index, _buffer[index], newItem, wantResult);

        return TryApplyLocal(new MagicListChange<T>(MagicListOperation.SetAt, index, newItem));
    }

    public void ModifyAt(int index, Action<T> update) => DoModifyAt(index, update, false);

    public Task<MagicSetResult> ModifyAtAsync(int index, Action<T> update) => DoModifyAt(index, update, true);

    private Task<MagicSetResult> DoModifyAt(int index, Action<T> update, bool wantResult)
    {
        if (index < 0 || index >= _buffer.Count) return RejectedTask;

        if (IsSynced) return Sync.RequestModifyAt(index, _buffer[index], update, wantResult);

        update(_buffer[index]);
        OnItemChanged?.Invoke(_buffer[index]);
        NotifyValueChanged();
        return SuccessTask;
    }

    public void Modify(Predicate<T> match, Action<T> update)
    {
        if (IsSynced)
        {
            int index = _buffer.FindIndex(match);
            if (index < 0) return;
            Sync.RequestModifyAt(index, _buffer[index], update, false);
            return;
        }

        var item = _buffer.FirstOrDefault(i => match(i));
        if (item == null) return;
        update(item);
        OnItemChanged?.Invoke(item);
        NotifyValueChanged();
    }

    public void Reset() => Clear();

    private Task<MagicSetResult> TryApplyLocal(MagicListChange<T> change)
    {
        if (IsChangeAllowed(MagicLinksNetwork.LocalClientId, change) == false) return RejectedTask;

        ApplyLocal(change);
        return SuccessTask;
    }

    internal bool IsChangeAllowed(ulong clientId, MagicListChange<T> change) =>
        MagicLinksNetwork.RunValidator(Validator, clientId, change, Value);

    internal void ApplyLocal(MagicListChange<T> change)
    {
        switch (change.Operation)
        {
            case MagicListOperation.Add: AddLocal(change.Item); break;
            case MagicListOperation.RemoveAt: RemoveAtLocal(change.Index); break;
            case MagicListOperation.SetAt: SetAtLocal(change.Index, change.Item); break;
            case MagicListOperation.Clear: ClearLocal(); break;
        }
    }

    private void NotifyValueChanged()
    {
        OnValueChanged?.Invoke(_buffer);
    }

    // --- Applied by the network synchronization (or directly when offline)

    internal void AddLocal(T item)
    {
        _buffer.Add(item);
        OnItemAdded?.Invoke(item);
        NotifyValueChanged();
    }

    internal void RemoveAtLocal(int index)
    {
        var item = _buffer[index];
        _buffer.RemoveAt(index);
        OnItemRemoved?.Invoke(item);
        NotifyValueChanged();
    }

    internal void SetAtLocal(int index, T newItem)
    {
        _buffer[index] = newItem;
        OnItemChanged?.Invoke(newItem);
        NotifyValueChanged();
    }

    internal void ClearLocal()
    {
        _buffer.Clear();
        OnCleared?.Invoke();
        NotifyValueChanged();
    }

    /// <summary>Replaces the whole content (late join, reset). Only OnValueChanged is raised.</summary>
    internal void ReplaceLocal(List<T> items)
    {
        _buffer.Clear();
        if (items != null) _buffer.AddRange(items);
        NotifyValueChanged();
    }
}


}
