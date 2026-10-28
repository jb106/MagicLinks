using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

[assembly: InternalsVisibleTo("MagicLinks.Netcode")]
[assembly: InternalsVisibleTo("MagicLinks.Tests")]

namespace MagicLinks.Network
{
    public enum MagicWriteResult
    {
        /// <summary>The value was applied by the authority and is now visible locally.</summary>
        Accepted,
        /// <summary>The authority refused the write (condition not met, write rule refused, item gone).</summary>
        Refused,
        /// <summary>The session ended before the authority answered. Nothing can be assumed.</summary>
        Disconnected
    }

    /// <summary>
    /// A rule checked where the value is committed (offline, or on the server/host) before accepting a write.
    /// <paramref name="fromClientId"/> is the client that asked for it (MagicLinksNetwork.LocalClientId offline or on the host).
    /// </summary>
    public delegate bool MagicVariableRule<T>(ulong fromClientId, T current, T value);

    /// <summary>
    /// A rule checked where the list is committed (offline, or on the server/host) before accepting a change.
    /// <paramref name="items"/> is the list before the change.
    /// </summary>
    public delegate bool MagicListRule<T>(ulong fromClientId, MagicListChange<T> change, IReadOnlyList<T> items);

    public enum MagicListOperation
    {
        Add,
        RemoveAt,
        SetAt,
        Clear
    }

    public readonly struct MagicListChange<T>
    {
        public readonly MagicListOperation Operation;
        /// <summary>Index affected (for Add, the index the item will have).</summary>
        public readonly int Index;
        /// <summary>New item for Add and SetAt, removed item for RemoveAt, default for Clear.</summary>
        public readonly T Item;

        public MagicListChange(MagicListOperation operation, int index, T item)
        {
            Operation = operation;
            Index = index;
            Item = item;
        }
    }

    /// <summary>
    /// Network state seen by game code. Has no dependency on any netcode package:
    /// when no session is running, IsOnline is false and everything is local.
    /// </summary>
    public static class MagicLinksNetwork
    {
        public static bool IsOnline { get; private set; }

        /// <summary>True when offline or when this peer is the server/host.</summary>
        public static bool IsServerOrOffline => !IsOnline || _isServer;

        /// <summary>Id of this machine in the session (0 offline and on the host).</summary>
        public static ulong LocalClientId { get; private set; }

        /// <summary>
        /// Raised when the session is ready: on the server when it starts, on a client once connected and
        /// the current values are received. From then on, writes from this machine are accepted.
        /// </summary>
        public static event Action OnSessionReady;

        /// <summary>
        /// Raised when the session stops, for any reason (local shutdown, server lost, kicked).
        /// The parameter is the disconnect reason given by the transport, or an empty string.
        /// Networked variables keep their last known value and become local again.
        /// </summary>
        public static event Action<string> OnSessionEnded;

        private static bool _isServer;

        internal static void NotifySessionReady(bool isServer, ulong localClientId)
        {
            if (IsOnline) return;

            IsOnline = true;
            _isServer = isServer;
            LocalClientId = localClientId;
            OnSessionReady?.Invoke();
        }

        internal static void NotifySessionEnded(string reason)
        {
            if (IsOnline == false) return;

            IsOnline = false;
            _isServer = false;
            LocalClientId = 0;
            OnSessionEnded?.Invoke(reason ?? string.Empty);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsOnline = false;
            _isServer = false;
            LocalClientId = 0;
            OnSessionReady = null;
            OnSessionEnded = null;
        }

        internal static bool RunWriteRule<T>(MagicVariableRule<T> rule, ulong clientId, T current, T value)
        {
            if (rule == null) return true;

            try
            {
                return rule(clientId, current, value);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MagicLinks] A write rule threw an exception, the write is rejected: {e}");
                return false;
            }
        }

        internal static bool RunWriteRule<T>(MagicListRule<T> rule, ulong clientId, MagicListChange<T> change, IReadOnlyList<T> items)
        {
            if (rule == null) return true;

            try
            {
                return rule(clientId, change, items);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MagicLinks] A write rule threw an exception, the change is rejected: {e}");
                return false;
            }
        }
    }

    internal interface IMagicVariableSync<T>
    {
        bool IsActive { get; }
        void RequestSet(T value);
        Task<MagicWriteResult> RequestSetAsync(T value);
        Task<MagicWriteResult> RequestSetIfAsync(T expected, T value);
    }

    /// <summary>
    /// List operations carry the item the client saw at that index: if another peer changed the list
    /// in the meantime, the server finds the item again instead of touching the wrong one.
    /// With <c>wantResult</c> false, nothing is awaited and the returned task is already completed.
    /// </summary>
    internal interface IMagicListSync<T>
    {
        bool IsActive { get; }
        Task<MagicWriteResult> RequestAdd(T item, bool wantResult);
        Task<MagicWriteResult> RequestRemoveAt(int index, T expected, bool wantResult);
        Task<MagicWriteResult> RequestSetAt(int index, T expected, T item, bool wantResult);
        Task<MagicWriteResult> RequestModifyAt(int index, T current, Action<T> update, bool wantResult);
        Task<MagicWriteResult> RequestClear(bool wantResult);
    }

    internal interface IMagicEventSync<T>
    {
        bool IsActive { get; }
        void RequestRaise(T value);
    }

    internal interface IMagicEventVoidSync
    {
        bool IsActive { get; }
        void RequestRaise();
    }
}
