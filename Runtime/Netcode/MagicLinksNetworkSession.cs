using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MagicLinks.Observables;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MagicLinks.Network
{
    /// <summary>
    /// Synchronizes the networked magic links for one netcode session.
    ///
    /// Everything goes through one named message on one reliable sequenced channel, so every peer
    /// receives the server's changes (states, events, answers) in the exact order the server applied them.
    /// The server is the only one committing values: clients send requests and wait for the result.
    /// </summary>
    public sealed class MagicLinksNetworkSession
    {
        internal const string MessageName = "MagicLinks";

        private const NetworkDelivery Delivery = NetworkDelivery.ReliableFragmentedSequenced;
        private const int InitialWriterSize = 256;
        private const int MaxMessageSize = 1024 * 1024;

        internal enum Op : byte
        {
            Hello = 1,
            Welcome = 2,
            State = 3,
            Event = 4,
            Set = 5,
            SetAsync = 6,
            TrySet = 7,
            Raise = 8,
            Response = 9,
            ListRequest = 10,
            ListChange = 11
        }

        internal enum ListOp : byte
        {
            Add = 1,
            RemoveAt = 2,
            SetAt = 3,
            Clear = 4
        }

        private readonly NetworkManager _networkManager;
        private readonly Func<ulong, string, bool> _clientWriteFilter;
        private readonly Action _onReady;

        private readonly Dictionary<ushort, Entry> _entries = new Dictionary<ushort, Entry>();
        private readonly List<Entry> _orderedEntries = new List<Entry>();
        private readonly HashSet<ulong> _readyClients = new HashSet<ulong>();
        private readonly List<ulong> _broadcastTargets = new List<ulong>();
        private readonly Dictionary<uint, TaskCompletionSource<MagicSetResult>> _pendingRequests =
            new Dictionary<uint, TaskCompletionSource<MagicSetResult>>();

        private uint _nextRequestId;
        private bool _connectedToServer;
        private bool _running;

        internal bool IsServer => _networkManager.IsServer;

        /// <summary>
        /// True for the whole session. A client still connecting refuses writes instead of applying them locally:
        /// a local write would be silently replaced by the server's state (and a lock could look acquired).
        /// </summary>
        internal bool IsActive => _running;

        /// <param name="onReady">Called when this peer can write: at start on the server, after the handshake on a client.</param>
        internal MagicLinksNetworkSession(NetworkManager networkManager, Func<ulong, string, bool> clientWriteFilter, Action onReady)
        {
            _networkManager = networkManager;
            _clientWriteFilter = clientWriteFilter;
            _onReady = onReady;
        }

        //----------------------------------------
        // Registration (called by the generated code)

        public void AddVariable<T>(ushort id, string name, Dictionary<string, MagicVariableObservable<T>> dictionary,
            MagicNetSerializer<T> serializer, bool resetOnWriterDisconnect)
        {
            if (TryGetLink(dictionary, name, out MagicVariableObservable<T> observable))
                AddEntry(new VariableEntry<T>(this, id, name, observable, serializer, resetOnWriterDisconnect));
        }

        /// <param name="removeItemsOnLeave">When a client disconnects, the server removes the items it added.</param>
        public void AddList<T>(ushort id, string name, Dictionary<string, MagicListVariableObservable<T>> dictionary,
            MagicNetSerializer<T> itemSerializer, bool removeItemsOnLeave)
        {
            if (TryGetLink(dictionary, name, out MagicListVariableObservable<T> observable))
                AddEntry(new ListEntry<T>(this, id, name, observable, itemSerializer, removeItemsOnLeave));
        }

        public void AddEvent<T>(ushort id, string name, Dictionary<string, MagicEventObservable<T>> dictionary,
            MagicNetSerializer<T> serializer)
        {
            if (TryGetLink(dictionary, name, out MagicEventObservable<T> observable))
                AddEntry(new EventEntry<T>(this, id, name, observable, serializer));
        }

        public void AddVoidEvent(ushort id, string name, Dictionary<string, MagicEventVoidObservable> dictionary)
        {
            if (TryGetLink(dictionary, name, out MagicEventVoidObservable observable))
                AddEntry(new VoidEventEntry(this, id, name, observable));
        }

        private static bool TryGetLink<TLink>(Dictionary<string, TLink> dictionary, string name, out TLink link)
        {
            if (dictionary != null && dictionary.TryGetValue(name, out link)) return true;

            Debug.LogError($"[MagicLinks] Networked link '{name}' does not exist in MagicLinksManager. " +
                           "Use 'Refresh Scripts' in the MagicLinkEditor. This link will not be synchronized.");
            link = default;
            return false;
        }

        private void AddEntry(Entry entry)
        {
            if (_entries.ContainsKey(entry.Id))
            {
                Debug.LogError($"[MagicLinks] Duplicate network id {entry.Id} for '{entry.Name}'. Use 'Refresh Scripts'.");
                return;
            }

            _entries.Add(entry.Id, entry);
            _orderedEntries.Add(entry);
        }

        //----------------------------------------
        // Lifecycle (driven by MagicLinksNetworkManager)

        internal void Start()
        {
            _running = true;

            _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessage);

            foreach (Entry entry in _orderedEntries)
            {
                if (IsServer) entry.CaptureResetValue();
                entry.Bind();
            }

            if (IsServer) _onReady?.Invoke();
        }

        internal void Stop()
        {
            if (_running == false) return;
            _running = false;

            if (_networkManager.CustomMessagingManager != null)
                _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);

            foreach (Entry entry in _orderedEntries) entry.Unbind();

            _readyClients.Clear();
            _connectedToServer = false;

            // Nobody will answer anymore: release every awaiting caller.
            List<TaskCompletionSource<MagicSetResult>> pending = new List<TaskCompletionSource<MagicSetResult>>(_pendingRequests.Values);
            _pendingRequests.Clear();
            foreach (TaskCompletionSource<MagicSetResult> request in pending) request.TrySetResult(MagicSetResult.Disconnected);
        }

        internal void OnClientConnected(ulong clientId)
        {
            if (IsServer || clientId != _networkManager.LocalClientId) return;

            _connectedToServer = true;

            using FastBufferWriter writer = CreateWriter(Op.Hello);
            writer.WriteValueSafe(MagicLinksNetworkRegistry.SchemaHash);
            SendToServer(writer);
        }

        internal void OnClientDisconnected(ulong clientId)
        {
            if (IsServer == false) return;

            _readyClients.Remove(clientId);

            foreach (Entry entry in _orderedEntries) entry.OnClientLeft(clientId);
        }

        //----------------------------------------
        // Messages

        private void OnMessage(ulong senderId, FastBufferReader reader)
        {
            if (_running == false) return;

            try
            {
                reader.ReadValueSafe(out byte op);

                if (IsServer) HandleOnServer((Op)op, senderId, reader);
                else if (senderId == NetworkManager.ServerClientId) HandleOnClient((Op)op, reader);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MagicLinks] Invalid network message from client {senderId}: {e}");
            }
        }

        private void HandleOnServer(Op op, ulong senderId, FastBufferReader reader)
        {
            if (op == Op.Hello)
            {
                HandleHello(senderId, reader);
                return;
            }

            if (_readyClients.Contains(senderId) == false)
            {
                Debug.LogWarning($"[MagicLinks] Ignored a request from client {senderId} before its handshake.");
                return;
            }

            // 0 means the client does not wait for an answer.
            uint requestId = 0;
            if (op == Op.SetAsync || op == Op.TrySet || op == Op.ListRequest) reader.ReadValueSafe(out requestId);

            reader.ReadValueSafe(out ushort id);

            if (_entries.TryGetValue(id, out Entry entry) == false)
            {
                Debug.LogWarning($"[MagicLinks] Client {senderId} sent a request for unknown network id {id}.");
                if (requestId != 0) SendResponse(senderId, requestId, MagicSetResult.Rejected);
                return;
            }

            MagicSetResult result;

            try
            {
                switch (op)
                {
                    case Op.Set:
                    case Op.SetAsync:
                        result = entry.ServerHandleSet(senderId, reader);
                        break;
                    case Op.TrySet:
                        result = entry.ServerHandleTrySet(senderId, reader);
                        break;
                    case Op.Raise:
                        entry.ServerHandleRaise(senderId, reader);
                        return;
                    case Op.ListRequest:
                        result = entry.ServerHandleListRequest(senderId, reader);
                        break;
                    default:
                        Debug.LogWarning($"[MagicLinks] Unexpected message {op} from client {senderId}.");
                        result = MagicSetResult.Rejected;
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MagicLinks] Failed to apply '{entry.Name}' from client {senderId}: {e}");
                result = MagicSetResult.Rejected;
            }

            // Sent after the state broadcast: when the caller's await resumes, the new value is already applied there.
            if (requestId != 0) SendResponse(senderId, requestId, result);
        }

        private void HandleHello(ulong senderId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out uint schemaHash);

            if (schemaHash != MagicLinksNetworkRegistry.SchemaHash)
            {
                string reason = "MagicLinks: the networked links of this client do not match the server " +
                                "(different build or different generated scripts).";
                Debug.LogError($"[MagicLinks] Client {senderId} rejected: {reason}");
                _networkManager.DisconnectClient(senderId, reason);
                return;
            }

            using FastBufferWriter writer = CreateWriter(Op.Welcome);

            int stateCount = 0;
            foreach (Entry entry in _orderedEntries)
                if (entry.IsState) stateCount++;

            writer.WriteValueSafe(stateCount);
            foreach (Entry entry in _orderedEntries)
            {
                if (entry.IsState == false) continue;
                writer.WriteValueSafe(entry.Id);
                entry.WriteState(writer);
            }

            SendTo(senderId, writer);
            _readyClients.Add(senderId);
        }

        private void HandleOnClient(Op op, FastBufferReader reader)
        {
            switch (op)
            {
                case Op.Welcome:
                    reader.ReadValueSafe(out int count);
                    for (int i = 0; i < count; i++)
                    {
                        reader.ReadValueSafe(out ushort stateId);
                        GetEntryOrThrow(stateId).ClientApplyState(reader);
                    }
                    // Current values received: this client can now read and write.
                    _onReady?.Invoke();
                    break;
                case Op.State:
                    reader.ReadValueSafe(out ushort id);
                    GetEntryOrThrow(id).ClientApplyState(reader);
                    break;
                case Op.Event:
                    reader.ReadValueSafe(out ushort eventId);
                    GetEntryOrThrow(eventId).ClientApplyEvent(reader);
                    break;
                case Op.ListChange:
                    reader.ReadValueSafe(out ushort listId);
                    GetEntryOrThrow(listId).ClientApplyListChange(reader);
                    break;
                case Op.Response:
                    reader.ReadValueSafe(out uint requestId);
                    reader.ReadValueSafe(out byte result);
                    if (_pendingRequests.Remove(requestId, out TaskCompletionSource<MagicSetResult> request))
                        request.TrySetResult((MagicSetResult)result);
                    break;
                default:
                    Debug.LogWarning($"[MagicLinks] Unexpected message {op} from the server.");
                    break;
            }
        }

        private Entry GetEntryOrThrow(ushort id)
        {
            if (_entries.TryGetValue(id, out Entry entry)) return entry;
            // The rest of the message cannot be read without knowing the type: stop here.
            throw new InvalidOperationException($"Unknown network id {id}.");
        }

        //----------------------------------------
        // Sending

        internal static FastBufferWriter CreateWriter(Op op)
        {
            FastBufferWriter writer = new FastBufferWriter(InitialWriterSize, Allocator.Temp, MaxMessageSize);
            writer.WriteValueSafe((byte)op);
            return writer;
        }

        /// <summary>False while a client is still connecting: the write is dropped with a warning.</summary>
        internal bool CanSendToServer(string linkName)
        {
            if (_connectedToServer) return true;

            Debug.LogWarning($"[MagicLinks] '{linkName}' was written while the client is not connected yet. The write is ignored.");
            return false;
        }

        internal void SendToServer(FastBufferWriter writer)
        {
            _networkManager.CustomMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId, writer, Delivery);
        }

        private void SendTo(ulong clientId, FastBufferWriter writer)
        {
            _networkManager.CustomMessagingManager.SendNamedMessage(MessageName, clientId, writer, Delivery);
        }

        /// <summary>Sends to every remote client that completed its handshake (never to the host itself).</summary>
        internal void Broadcast(FastBufferWriter writer)
        {
            _broadcastTargets.Clear();
            foreach (ulong clientId in _networkManager.ConnectedClientsIds)
            {
                if (_readyClients.Contains(clientId)) _broadcastTargets.Add(clientId);
            }

            if (_broadcastTargets.Count == 0) return;

            _networkManager.CustomMessagingManager.SendNamedMessage(MessageName, _broadcastTargets, writer, Delivery);
        }

        private void SendResponse(ulong clientId, uint requestId, MagicSetResult result)
        {
            using FastBufferWriter writer = CreateWriter(Op.Response);
            writer.WriteValueSafe(requestId);
            writer.WriteValueSafe((byte)result);
            SendTo(clientId, writer);
        }

        private static readonly Task<MagicSetResult> SentTask = Task.FromResult(MagicSetResult.Success);

        /// <summary>
        /// Sends a request to the server and returns a task completed by its response.
        /// Without <paramref name="wantResult"/>, no answer is asked for and the returned task is already completed.
        /// </summary>
        internal Task<MagicSetResult> SendRequest(Op op, ushort id, Action<FastBufferWriter> writePayload, bool wantResult = true)
        {
            if (wantResult == false)
            {
                using FastBufferWriter writer = CreateWriter(op);
                writer.WriteValueSafe(0u);
                writer.WriteValueSafe(id);
                writePayload(writer);
                SendToServer(writer);
                return SentTask;
            }

            uint requestId = ++_nextRequestId;
            if (requestId == 0) requestId = ++_nextRequestId;

            TaskCompletionSource<MagicSetResult> request =
                new TaskCompletionSource<MagicSetResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                using FastBufferWriter writer = CreateWriter(op);
                writer.WriteValueSafe(requestId);
                writer.WriteValueSafe(id);
                writePayload(writer);
                SendToServer(writer);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MagicLinks] Failed to send a request: {e}");
                return Task.FromResult(MagicSetResult.Rejected);
            }

            _pendingRequests.Add(requestId, request);
            return request.Task;
        }

        internal bool IsClientWriteAllowed(ulong clientId, string name)
        {
            if (_clientWriteFilter == null) return true;

            if (_clientWriteFilter(clientId, name)) return true;

            Debug.LogWarning($"[MagicLinks] Write to '{name}' from client {clientId} was rejected by the write filter.");
            return false;
        }

        //----------------------------------------
        // Entries

        internal abstract class Entry
        {
            internal readonly ushort Id;
            internal readonly string Name;
            protected readonly MagicLinksNetworkSession Session;

            protected Entry(MagicLinksNetworkSession session, ushort id, string name)
            {
                Session = session;
                Id = id;
                Name = name;
            }

            public bool IsActive => Session.IsActive;

            internal virtual bool IsState => false;

            internal abstract void Bind();
            internal abstract void Unbind();

            internal virtual void CaptureResetValue() { }
            internal virtual void OnClientLeft(ulong clientId) { }

            internal virtual void WriteState(FastBufferWriter writer) => throw NotSupported();
            internal virtual void ClientApplyState(FastBufferReader reader) => throw NotSupported();
            internal virtual MagicSetResult ServerHandleSet(ulong senderId, FastBufferReader reader) => throw NotSupported();
            internal virtual MagicSetResult ServerHandleTrySet(ulong senderId, FastBufferReader reader) => throw NotSupported();
            internal virtual void ServerHandleRaise(ulong senderId, FastBufferReader reader) => throw NotSupported();
            internal virtual void ClientApplyEvent(FastBufferReader reader) => throw NotSupported();
            internal virtual MagicSetResult ServerHandleListRequest(ulong senderId, FastBufferReader reader) => throw NotSupported();
            internal virtual void ClientApplyListChange(FastBufferReader reader) => throw NotSupported();

            private Exception NotSupported() => new InvalidOperationException($"'{Name}' does not support this operation.");
        }

        private sealed class VariableEntry<T> : Entry, IMagicVariableSync<T>
        {
            private readonly MagicVariableObservable<T> _observable;
            private readonly MagicNetSerializer<T> _serializer;
            private readonly bool _resetOnWriterDisconnect;

            private ulong _lastWriter = NetworkManager.ServerClientId;
            private byte[] _resetValue;

            // Last value written by this client: its excluded (local only) fields are kept when the
            // authoritative value comes back.
            private T _localWrite;
            private bool _hasLocalWrite;

            public VariableEntry(MagicLinksNetworkSession session, ushort id, string name,
                MagicVariableObservable<T> observable, MagicNetSerializer<T> serializer, bool resetOnWriterDisconnect)
                : base(session, id, name)
            {
                _observable = observable;
                _serializer = serializer;
                _resetOnWriterDisconnect = resetOnWriterDisconnect;
            }

            internal override bool IsState => true;

            internal override void Bind() => _observable.Sync = this;

            internal override void Unbind()
            {
                if (_observable.Sync == this) _observable.Sync = null;
            }

            // --- Requests coming from local game code

            public void RequestSet(T value)
            {
                if (Session.IsServer)
                {
                    ServerWrite(value, NetworkManager.ServerClientId);
                    return;
                }

                bool sameInstance = IsSameInstance(value);
                if (sameInstance == false && CommitLocalOnlyChange(value)) return;

                if (Session.CanSendToServer(Name) == false) return;

                RememberLocalWrite(value);

                using FastBufferWriter writer = CreateWriter(Op.Set);
                writer.WriteValueSafe(Id);
                _serializer.WritePatch(writer, sameInstance ? default : _observable.Value, value);
                Session.SendToServer(writer);
            }

            public Task<MagicSetResult> RequestSetAsync(T value)
            {
                if (Session.IsServer)
                    return Task.FromResult(ServerWrite(value, NetworkManager.ServerClientId));

                bool sameInstance = IsSameInstance(value);
                if (sameInstance == false && CommitLocalOnlyChange(value)) return Task.FromResult(MagicSetResult.Success);

                if (Session.CanSendToServer(Name) == false) return Task.FromResult(MagicSetResult.Disconnected);

                RememberLocalWrite(value);

                T from = sameInstance ? default : _observable.Value;
                return Session.SendRequest(Op.SetAsync, Id, writer => _serializer.WritePatch(writer, from, value));
            }

            public Task<MagicSetResult> RequestTrySetAsync(T expected, T value)
            {
                if (Session.IsServer)
                    return Task.FromResult(ServerTrySet(expected, value, NetworkManager.ServerClientId));

                if (Session.CanSendToServer(Name) == false) return Task.FromResult(MagicSetResult.Disconnected);

                RememberLocalWrite(value);

                return Session.SendRequest(Op.TrySet, Id, writer =>
                {
                    _serializer.Write(writer, expected);
                    _serializer.Write(writer, value);
                });
            }

            /// <summary>
            /// The same object or list was mutated in place and assigned again: nothing can be compared,
            /// so it is sent in full.
            /// </summary>
            private bool IsSameInstance(T value) =>
                IsReferenceType && value != null && ReferenceEquals(_observable.Value, value);

            private static readonly bool IsReferenceType = typeof(T).IsValueType == false;

            /// <summary>Nothing networked changed (only excluded fields, or the same value): apply locally, send nothing.</summary>
            private bool CommitLocalOnlyChange(T value)
            {
                if (_serializer.AreEqual(_observable.Value, value) == false) return false;

                _observable.Commit(value);
                return true;
            }

            private void RememberLocalWrite(T value)
            {
                _localWrite = value;
                _hasLocalWrite = true;
            }

            // --- Server side

            private void ServerCommit(T value, ulong writerId)
            {
                _lastWriter = writerId;

                bool networkedChange = IsSameInstance(value) || _serializer.AreEqual(_observable.Value, value) == false;

                // Broadcast before committing: if a listener writes another link while handling this change,
                // that second change is sent after this one, so clients see the same order as the server.
                if (networkedChange)
                {
                    using FastBufferWriter writer = CreateWriter(Op.State);
                    writer.WriteValueSafe(Id);
                    _serializer.Write(writer, value);
                    Session.Broadcast(writer);
                }

                // A server-side write may only change excluded fields: still apply it locally.
                if (networkedChange || writerId == NetworkManager.ServerClientId)
                    _observable.Commit(value);
            }

            /// <summary>A write asked by a peer (or the host): the validator decides before anything is applied.</summary>
            private MagicSetResult ServerWrite(T value, ulong writerId)
            {
                if (MagicLinksNetwork.RunValidator(_observable.Validator, writerId, _observable.Value, value) == false)
                    return MagicSetResult.Rejected;

                ServerCommit(value, writerId);
                return MagicSetResult.Success;
            }

            private MagicSetResult ServerTrySet(T expected, T value, ulong writerId)
            {
                if (_serializer.AreEqual(_observable.Value, expected) == false) return MagicSetResult.Rejected;

                return ServerWrite(value, writerId);
            }

            internal override MagicSetResult ServerHandleSet(ulong senderId, FastBufferReader reader)
            {
                T value = _serializer.ReadPatch(reader, _observable.Value);

                if (Session.IsClientWriteAllowed(senderId, Name) == false) return MagicSetResult.Rejected;

                return ServerWrite(value, senderId);
            }

            internal override MagicSetResult ServerHandleTrySet(ulong senderId, FastBufferReader reader)
            {
                T expected = _serializer.Read(reader, _observable.Value);
                T value = _serializer.Read(reader, _observable.Value);

                if (Session.IsClientWriteAllowed(senderId, Name) == false) return MagicSetResult.Rejected;

                return ServerTrySet(expected, value, senderId);
            }

            internal override void WriteState(FastBufferWriter writer) => _serializer.Write(writer, _observable.Value);

            internal override void CaptureResetValue()
            {
                _lastWriter = NetworkManager.ServerClientId;

                if (_resetOnWriterDisconnect == false) return;

                using FastBufferWriter writer = new FastBufferWriter(InitialWriterSize, Allocator.Temp, MaxMessageSize);
                _serializer.Write(writer, _observable.Value);
                _resetValue = writer.ToArray();
            }

            internal override void OnClientLeft(ulong clientId)
            {
                if (_resetOnWriterDisconnect == false || _lastWriter != clientId || _resetValue == null) return;

                using FastBufferReader reader = new FastBufferReader(_resetValue, Allocator.Temp);
                ServerCommit(_serializer.Read(reader, _observable.Value), NetworkManager.ServerClientId);
            }

            // --- Client side

            internal override void ClientApplyState(FastBufferReader reader)
            {
                bool hadLocalWrite = _hasLocalWrite;
                T local = hadLocalWrite ? _localWrite : _observable.Value;

                _localWrite = default;
                _hasLocalWrite = false;

                T value = _serializer.Read(reader, local);

                if (hadLocalWrite == false && _serializer.AreEqual(_observable.Value, value)) return;

                _observable.Commit(value);
            }
        }

        /// <summary>
        /// A list is synchronized operation by operation, so that two peers adding items at the same
        /// time both get their item in. The server resolves every operation to an index, checks the
        /// list's validator, applies it and sends it back.
        /// </summary>
        private sealed class ListEntry<T> : Entry, IMagicListSync<T>
        {
            private static readonly Task<MagicSetResult> DisconnectedTask = Task.FromResult(MagicSetResult.Disconnected);

            private readonly MagicListVariableObservable<T> _observable;
            private readonly MagicNetSerializer<T> _item;
            private readonly MagicNetListSerializer<T> _list;
            private readonly bool _removeItemsOnLeave;

            // Server only: the client that added each item, in the same order as the list.
            private readonly List<ulong> _owners = new List<ulong>();

            public ListEntry(MagicLinksNetworkSession session, ushort id, string name,
                MagicListVariableObservable<T> observable, MagicNetSerializer<T> item, bool removeItemsOnLeave)
                : base(session, id, name)
            {
                _observable = observable;
                _item = item;
                _list = new MagicNetListSerializer<T>(item);
                _removeItemsOnLeave = removeItemsOnLeave;
            }

            internal override bool IsState => true;

            internal override void Bind() => _observable.Sync = this;

            internal override void Unbind()
            {
                if (_observable.Sync == this) _observable.Sync = null;
            }

            // --- Requests coming from local game code

            public Task<MagicSetResult> RequestAdd(T item, bool wantResult)
            {
                if (Session.IsServer) return Task.FromResult(ServerAdd(item, NetworkManager.ServerClientId));

                return SendRequest(ListOp.Add, wantResult, writer => _item.Write(writer, item));
            }

            public Task<MagicSetResult> RequestRemoveAt(int index, T expected, bool wantResult)
            {
                if (Session.IsServer) return Task.FromResult(ServerRemoveAt(index, expected, NetworkManager.ServerClientId));

                return SendRequest(ListOp.RemoveAt, wantResult, writer =>
                {
                    writer.WriteValueSafe(index);
                    _item.Write(writer, expected);
                });
            }

            public Task<MagicSetResult> RequestSetAt(int index, T expected, T item, bool wantResult)
            {
                if (Session.IsServer) return Task.FromResult(ServerSetAt(index, expected, item, NetworkManager.ServerClientId));

                return SendRequest(ListOp.SetAt, wantResult, writer =>
                {
                    writer.WriteValueSafe(index);
                    _item.Write(writer, expected);
                    _item.Write(writer, item);
                });
            }

            public Task<MagicSetResult> RequestModifyAt(int index, T current, Action<T> update, bool wantResult)
            {
                if (Session.IsServer && _observable.Validator == null)
                {
                    // Same as offline: the item is modified in place, then sent to everyone.
                    update(current);
                    return Task.FromResult(ServerSetAt(index, current, current, NetworkManager.ServerClientId));
                }

                // Modify a copy: the local item only changes once the change is accepted.
                T copy = Copy(current);
                update(copy);
                return RequestSetAt(index, current, copy, wantResult);
            }

            public Task<MagicSetResult> RequestClear(bool wantResult)
            {
                if (Session.IsServer) return Task.FromResult(ServerClear(NetworkManager.ServerClientId));

                return SendRequest(ListOp.Clear, wantResult, null);
            }

            private Task<MagicSetResult> SendRequest(ListOp op, bool wantResult, Action<FastBufferWriter> writePayload)
            {
                if (Session.CanSendToServer(Name) == false) return DisconnectedTask;

                return Session.SendRequest(Op.ListRequest, Id, writer =>
                {
                    writer.WriteValueSafe((byte)op);
                    writePayload?.Invoke(writer);
                }, wantResult);
            }

            private T Copy(T item)
            {
                using FastBufferWriter writer = new FastBufferWriter(InitialWriterSize, Allocator.Temp, MaxMessageSize);
                _item.Write(writer, item);
                using FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp);
                return _item.Read(reader, item);
            }

            // --- Server side

            internal override MagicSetResult ServerHandleListRequest(ulong senderId, FastBufferReader reader)
            {
                reader.ReadValueSafe(out byte op);

                switch ((ListOp)op)
                {
                    case ListOp.Add:
                    {
                        T item = _item.Read(reader, default);
                        if (Session.IsClientWriteAllowed(senderId, Name) == false) return MagicSetResult.Rejected;
                        return ServerAdd(item, senderId);
                    }
                    case ListOp.RemoveAt:
                    {
                        reader.ReadValueSafe(out int index);
                        T expected = _item.Read(reader, default);
                        if (Session.IsClientWriteAllowed(senderId, Name) == false) return MagicSetResult.Rejected;
                        return ServerRemoveAt(index, expected, senderId);
                    }
                    case ListOp.SetAt:
                    {
                        reader.ReadValueSafe(out int index);
                        T expected = _item.Read(reader, default);
                        T item = _item.Read(reader, ValueAt(index));
                        if (Session.IsClientWriteAllowed(senderId, Name) == false) return MagicSetResult.Rejected;
                        return ServerSetAt(index, expected, item, senderId);
                    }
                    case ListOp.Clear:
                        if (Session.IsClientWriteAllowed(senderId, Name) == false) return MagicSetResult.Rejected;
                        return ServerClear(senderId);
                    default:
                        throw new InvalidOperationException($"Unknown list operation {op}.");
                }
            }

            private T ValueAt(int index) =>
                index >= 0 && index < _observable.Value.Count ? _observable.Value[index] : default;

            /// <summary>The index the client meant, even if the list changed since it looked at it. -1 if the item is gone.</summary>
            private int ResolveIndex(int index, T expected)
            {
                IReadOnlyList<T> items = _observable.Value;

                if (index >= 0 && index < items.Count && _item.AreEqual(items[index], expected)) return index;

                for (int i = 0; i < items.Count; i++)
                    if (_item.AreEqual(items[i], expected)) return i;

                return -1;
            }

            private MagicSetResult ServerAdd(T item, ulong writerId) =>
                TryApply(new MagicListChange<T>(MagicListOperation.Add, _observable.Value.Count, item), writerId);

            private MagicSetResult ServerRemoveAt(int index, T expected, ulong writerId)
            {
                int resolved = ResolveIndex(index, expected);
                if (resolved < 0) return MagicSetResult.Rejected;

                return TryApply(new MagicListChange<T>(MagicListOperation.RemoveAt, resolved, _observable.Value[resolved]), writerId);
            }

            private MagicSetResult ServerSetAt(int index, T expected, T item, ulong writerId)
            {
                int resolved = ResolveIndex(index, expected);
                if (resolved < 0) return MagicSetResult.Rejected;

                return TryApply(new MagicListChange<T>(MagicListOperation.SetAt, resolved, item), writerId);
            }

            private MagicSetResult ServerClear(ulong writerId)
            {
                if (_observable.Value.Count == 0) return MagicSetResult.Success;

                return TryApply(new MagicListChange<T>(MagicListOperation.Clear, -1, default), writerId);
            }

            private MagicSetResult TryApply(MagicListChange<T> change, ulong writerId)
            {
                if (_observable.IsChangeAllowed(writerId, change) == false) return MagicSetResult.Rejected;

                Apply(change, writerId);
                return MagicSetResult.Success;
            }

            private void Apply(MagicListChange<T> change, ulong writerId)
            {
                // Broadcast before applying locally, for the same ordering reason as variables.
                using (FastBufferWriter writer = CreateWriter(Op.ListChange))
                {
                    writer.WriteValueSafe(Id);
                    writer.WriteValueSafe((byte)ToListOp(change.Operation));

                    if (change.Operation != MagicListOperation.Add && change.Operation != MagicListOperation.Clear)
                        writer.WriteValueSafe(change.Index);

                    if (change.Operation == MagicListOperation.Add || change.Operation == MagicListOperation.SetAt)
                        _item.Write(writer, change.Item);

                    Session.Broadcast(writer);
                }

                switch (change.Operation)
                {
                    case MagicListOperation.Add: _owners.Add(writerId); break;
                    case MagicListOperation.RemoveAt: _owners.RemoveAt(change.Index); break;
                    case MagicListOperation.Clear: _owners.Clear(); break;
                }

                _observable.ApplyLocal(change);
            }

            private static ListOp ToListOp(MagicListOperation operation)
            {
                switch (operation)
                {
                    case MagicListOperation.Add: return ListOp.Add;
                    case MagicListOperation.RemoveAt: return ListOp.RemoveAt;
                    case MagicListOperation.SetAt: return ListOp.SetAt;
                    default: return ListOp.Clear;
                }
            }

            internal override void WriteState(FastBufferWriter writer) =>
                _list.Write(writer, new List<T>(_observable.Value));

            internal override void CaptureResetValue()
            {
                // Items present when the session starts belong to the server.
                _owners.Clear();
                for (int i = 0; i < _observable.Value.Count; i++) _owners.Add(NetworkManager.ServerClientId);
            }

            internal override void OnClientLeft(ulong clientId)
            {
                if (_removeItemsOnLeave == false) return;

                // A server decision: not subject to the validator.
                for (int i = _owners.Count - 1; i >= 0; i--)
                {
                    if (_owners[i] == clientId)
                        Apply(new MagicListChange<T>(MagicListOperation.RemoveAt, i, _observable.Value[i]), NetworkManager.ServerClientId);
                }
            }

            // --- Client side

            internal override void ClientApplyState(FastBufferReader reader) =>
                _observable.ReplaceLocal(_list.Read(reader, new List<T>(_observable.Value)));

            internal override void ClientApplyListChange(FastBufferReader reader)
            {
                reader.ReadValueSafe(out byte op);

                switch ((ListOp)op)
                {
                    case ListOp.Add:
                        _observable.AddLocal(_item.Read(reader, default));
                        break;
                    case ListOp.RemoveAt:
                        reader.ReadValueSafe(out int removeIndex);
                        _observable.RemoveAtLocal(removeIndex);
                        break;
                    case ListOp.SetAt:
                        reader.ReadValueSafe(out int setIndex);
                        _observable.SetAtLocal(setIndex, _item.Read(reader, ValueAt(setIndex)));
                        break;
                    case ListOp.Clear:
                        _observable.ClearLocal();
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown list operation {op}.");
                }
            }
        }

        private sealed class EventEntry<T> : Entry, IMagicEventSync<T>
        {
            private readonly MagicEventObservable<T> _observable;
            private readonly MagicNetSerializer<T> _serializer;

            public EventEntry(MagicLinksNetworkSession session, ushort id, string name,
                MagicEventObservable<T> observable, MagicNetSerializer<T> serializer)
                : base(session, id, name)
            {
                _observable = observable;
                _serializer = serializer;
            }

            internal override void Bind() => _observable.Sync = this;

            internal override void Unbind()
            {
                if (_observable.Sync == this) _observable.Sync = null;
            }

            public void RequestRaise(T value)
            {
                if (Session.IsServer)
                {
                    ServerRaise(value);
                    return;
                }

                if (Session.CanSendToServer(Name) == false) return;

                // Not raised locally: the server echoes it back, so it is raised exactly once on every peer.
                using FastBufferWriter writer = CreateWriter(Op.Raise);
                writer.WriteValueSafe(Id);
                _serializer.Write(writer, value);
                Session.SendToServer(writer);
            }

            private void ServerRaise(T value)
            {
                using (FastBufferWriter writer = CreateWriter(Op.Event))
                {
                    writer.WriteValueSafe(Id);
                    _serializer.Write(writer, value);
                    Session.Broadcast(writer);
                }

                _observable.RaiseLocal(value);
            }

            internal override void ServerHandleRaise(ulong senderId, FastBufferReader reader)
            {
                T value = _serializer.Read(reader, default);

                if (Session.IsClientWriteAllowed(senderId, Name) == false) return;

                ServerRaise(value);
            }

            internal override void ClientApplyEvent(FastBufferReader reader)
            {
                _observable.RaiseLocal(_serializer.Read(reader, default));
            }
        }

        private sealed class VoidEventEntry : Entry, IMagicEventVoidSync
        {
            private readonly MagicEventVoidObservable _observable;

            public VoidEventEntry(MagicLinksNetworkSession session, ushort id, string name, MagicEventVoidObservable observable)
                : base(session, id, name)
            {
                _observable = observable;
            }

            internal override void Bind() => _observable.Sync = this;

            internal override void Unbind()
            {
                if (_observable.Sync == this) _observable.Sync = null;
            }

            public void RequestRaise()
            {
                if (Session.IsServer)
                {
                    ServerRaise();
                    return;
                }

                if (Session.CanSendToServer(Name) == false) return;

                using FastBufferWriter writer = CreateWriter(Op.Raise);
                writer.WriteValueSafe(Id);
                Session.SendToServer(writer);
            }

            private void ServerRaise()
            {
                using (FastBufferWriter writer = CreateWriter(Op.Event))
                {
                    writer.WriteValueSafe(Id);
                    Session.Broadcast(writer);
                }

                _observable.RaiseLocal();
            }

            internal override void ServerHandleRaise(ulong senderId, FastBufferReader reader)
            {
                if (Session.IsClientWriteAllowed(senderId, Name) == false) return;

                ServerRaise();
            }

            internal override void ClientApplyEvent(FastBufferReader reader) => _observable.RaiseLocal();
        }
    }
}
