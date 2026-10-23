using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MagicLinks.Network
{
    /// <summary>
    /// Writes and reads one type of value on the network.
    /// <paramref name="local"/> / <c>authoritative</c> parameters are the value currently held by the
    /// receiving peer: fields that are excluded from the network are taken from it.
    /// </summary>
    public abstract class MagicNetSerializer<T>
    {
        public abstract void Write(FastBufferWriter writer, T value);
        public abstract T Read(FastBufferReader reader, T local);

        /// <summary>Compares only what travels on the network.</summary>
        public virtual bool AreEqual(T a, T b) => EqualityComparer<T>.Default.Equals(a, b);

        /// <summary>Writes only what changed between <paramref name="from"/> and <paramref name="to"/>.</summary>
        public virtual void WritePatch(FastBufferWriter writer, T from, T to) => Write(writer, to);

        /// <summary>Applies a patch written by WritePatch on top of <paramref name="authoritative"/>.</summary>
        public virtual T ReadPatch(FastBufferReader reader, T authoritative) => Read(reader, authoritative);

        protected static bool WriteNullFlag(FastBufferWriter writer, object value)
        {
            bool hasValue = value != null;
            writer.WriteValueSafe(hasValue);
            return hasValue;
        }

        protected static bool ReadNullFlag(FastBufferReader reader)
        {
            reader.ReadValueSafe(out bool hasValue);
            return hasValue;
        }
    }

    public sealed class MagicNetDelegateSerializer<T> : MagicNetSerializer<T>
    {
        private readonly Action<FastBufferWriter, T> _write;
        private readonly Func<FastBufferReader, T> _read;

        public MagicNetDelegateSerializer(Action<FastBufferWriter, T> write, Func<FastBufferReader, T> read)
        {
            _write = write;
            _read = read;
        }

        public override void Write(FastBufferWriter writer, T value) => _write(writer, value);
        public override T Read(FastBufferReader reader, T local) => _read(reader);
    }

    public sealed class MagicNetEnumSerializer<T> : MagicNetSerializer<T> where T : unmanaged, Enum
    {
        public static readonly MagicNetEnumSerializer<T> Instance = new MagicNetEnumSerializer<T>();

        public override void Write(FastBufferWriter writer, T value) => writer.WriteValueSafe(value);

        public override T Read(FastBufferReader reader, T local)
        {
            reader.ReadValueSafe(out T value);
            return value;
        }
    }

    public sealed class MagicNetNetworkSerializableSerializer<T> : MagicNetSerializer<T> where T : INetworkSerializable, new()
    {
        public static readonly MagicNetNetworkSerializableSerializer<T> Instance = new MagicNetNetworkSerializableSerializer<T>();

        private static readonly bool IsReferenceType = !typeof(T).IsValueType;

        public override void Write(FastBufferWriter writer, T value)
        {
            if (IsReferenceType && !WriteNullFlag(writer, value)) return;
            writer.WriteValueSafe(value);
        }

        public override T Read(FastBufferReader reader, T local)
        {
            if (IsReferenceType && !ReadNullFlag(reader)) return default;
            reader.ReadValueSafe(out T value);
            return value;
        }
    }

    public sealed class MagicNetListSerializer<T> : MagicNetSerializer<List<T>>
    {
        private readonly MagicNetSerializer<T> _element;

        public MagicNetListSerializer(MagicNetSerializer<T> element) => _element = element;

        public override void Write(FastBufferWriter writer, List<T> value)
        {
            if (!WriteNullFlag(writer, value)) return;

            writer.WriteValueSafe(value.Count);
            foreach (T item in value) _element.Write(writer, item);
        }

        public override List<T> Read(FastBufferReader reader, List<T> local)
        {
            if (!ReadNullFlag(reader)) return null;

            reader.ReadValueSafe(out int count);
            List<T> result = new List<T>(count);

            for (int i = 0; i < count; i++)
                result.Add(_element.Read(reader, local != null && i < local.Count ? local[i] : default));

            return result;
        }

        public override bool AreEqual(List<T> a, List<T> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Count != b.Count) return false;

            for (int i = 0; i < a.Count; i++)
                if (!_element.AreEqual(a[i], b[i])) return false;

            return true;
        }
    }

    public sealed class MagicNetArraySerializer<T> : MagicNetSerializer<T[]>
    {
        private readonly MagicNetSerializer<T> _element;

        public MagicNetArraySerializer(MagicNetSerializer<T> element) => _element = element;

        public override void Write(FastBufferWriter writer, T[] value)
        {
            if (!WriteNullFlag(writer, value)) return;

            writer.WriteValueSafe(value.Length);
            foreach (T item in value) _element.Write(writer, item);
        }

        public override T[] Read(FastBufferReader reader, T[] local)
        {
            if (!ReadNullFlag(reader)) return null;

            reader.ReadValueSafe(out int count);
            T[] result = new T[count];

            for (int i = 0; i < count; i++)
                result[i] = _element.Read(reader, local != null && i < local.Length ? local[i] : default);

            return result;
        }

        public override bool AreEqual(T[] a, T[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
                if (!_element.AreEqual(a[i], b[i])) return false;

            return true;
        }
    }

    /// <summary>A GameObject travels as the id of its spawned NetworkObject and is resolved on reception.</summary>
    public sealed class MagicNetGameObjectReferenceSerializer : MagicNetSerializer<GameObject>
    {
        public static readonly MagicNetGameObjectReferenceSerializer Instance = new MagicNetGameObjectReferenceSerializer();

        public override void Write(FastBufferWriter writer, GameObject value)
        {
            NetworkObject networkObject = value != null ? value.GetComponent<NetworkObject>() : null;
            MagicNetReferences.WriteNetworkObject(writer, networkObject, value);
        }

        public override GameObject Read(FastBufferReader reader, GameObject local)
        {
            NetworkObject networkObject = MagicNetReferences.ReadNetworkObject(reader);
            return networkObject != null ? networkObject.gameObject : null;
        }

        public override bool AreEqual(GameObject a, GameObject b) => a == b;
    }

    /// <summary>
    /// A component travels as a NetworkBehaviourReference when it is a NetworkBehaviour,
    /// otherwise as the NetworkObject on the same GameObject, and is resolved on reception.
    /// </summary>
    public sealed class MagicNetComponentReferenceSerializer<T> : MagicNetSerializer<T> where T : Component
    {
        public static readonly MagicNetComponentReferenceSerializer<T> Instance = new MagicNetComponentReferenceSerializer<T>();

        private const byte KindNull = 0;
        private const byte KindBehaviour = 1;
        private const byte KindObject = 2;

        public override void Write(FastBufferWriter writer, T value)
        {
            if (value == null)
            {
                writer.WriteValueSafe(KindNull);
                return;
            }

            if (value is NetworkBehaviour behaviour && behaviour.IsSpawned)
            {
                writer.WriteValueSafe(KindBehaviour);
                writer.WriteValueSafe(new NetworkBehaviourReference(behaviour));
                return;
            }

            writer.WriteValueSafe(KindObject);
            MagicNetReferences.WriteNetworkObject(writer, value.GetComponent<NetworkObject>(), value);
        }

        public override T Read(FastBufferReader reader, T local)
        {
            reader.ReadValueSafe(out byte kind);

            switch (kind)
            {
                case KindBehaviour:
                    reader.ReadValueSafe(out NetworkBehaviourReference reference);
                    if (reference.TryGet(out NetworkBehaviour behaviour)) return behaviour as T;
                    Debug.LogWarning($"[MagicLinks] Received a reference to a NetworkBehaviour that is not spawned locally. Resolved as null.");
                    return null;
                case KindObject:
                    NetworkObject networkObject = MagicNetReferences.ReadNetworkObject(reader);
                    return networkObject != null ? networkObject.GetComponent<T>() : null;
                default:
                    return null;
            }
        }

        public override bool AreEqual(T a, T b) => a == b;
    }

    public static class MagicNetReferences
    {
        public static void WriteNetworkObject(FastBufferWriter writer, NetworkObject networkObject, UnityEngine.Object source)
        {
            if (networkObject == null || networkObject.IsSpawned == false)
            {
                if (source != null)
                {
                    Debug.LogError($"[MagicLinks] '{source.name}' cannot be sent as a network reference: " +
                                   "it must be on a GameObject with a spawned NetworkObject. Sent as null.", source);
                }

                writer.WriteValueSafe(false);
                return;
            }

            writer.WriteValueSafe(true);
            writer.WriteValueSafe(new NetworkObjectReference(networkObject));
        }

        public static NetworkObject ReadNetworkObject(FastBufferReader reader)
        {
            reader.ReadValueSafe(out bool hasValue);
            if (hasValue == false) return null;

            reader.ReadValueSafe(out NetworkObjectReference reference);
            if (reference.TryGet(out NetworkObject networkObject)) return networkObject;

            Debug.LogWarning($"[MagicLinks] Received a reference to NetworkObject {reference.NetworkObjectId} " +
                             "which is not spawned locally. Resolved as null.");
            return null;
        }
    }

    /// <summary>Serializers for the built-in types. Used by the generated network code.</summary>
    public static class MagicNetSerializers
    {
        public static readonly MagicNetSerializer<bool> Bool = new MagicNetDelegateSerializer<bool>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out bool v); return v; });
        public static readonly MagicNetSerializer<byte> Byte = new MagicNetDelegateSerializer<byte>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out byte v); return v; });
        public static readonly MagicNetSerializer<sbyte> SByte = new MagicNetDelegateSerializer<sbyte>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out sbyte v); return v; });
        public static readonly MagicNetSerializer<short> Short = new MagicNetDelegateSerializer<short>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out short v); return v; });
        public static readonly MagicNetSerializer<ushort> UShort = new MagicNetDelegateSerializer<ushort>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out ushort v); return v; });
        public static readonly MagicNetSerializer<int> Int = new MagicNetDelegateSerializer<int>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out int v); return v; });
        public static readonly MagicNetSerializer<uint> UInt = new MagicNetDelegateSerializer<uint>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out uint v); return v; });
        public static readonly MagicNetSerializer<long> Long = new MagicNetDelegateSerializer<long>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out long v); return v; });
        public static readonly MagicNetSerializer<ulong> ULong = new MagicNetDelegateSerializer<ulong>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out ulong v); return v; });
        public static readonly MagicNetSerializer<float> Float = new MagicNetDelegateSerializer<float>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out float v); return v; });
        public static readonly MagicNetSerializer<double> Double = new MagicNetDelegateSerializer<double>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out double v); return v; });
        public static readonly MagicNetSerializer<char> Char = new MagicNetDelegateSerializer<char>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out char v); return v; });

        public static readonly MagicNetSerializer<string> String = new MagicNetDelegateSerializer<string>(
            (w, v) =>
            {
                w.WriteValueSafe(v != null);
                if (v != null) w.WriteValueSafe(v);
            },
            r =>
            {
                r.ReadValueSafe(out bool hasValue);
                if (hasValue == false) return null;
                r.ReadValueSafe(out string v);
                return v;
            });

        public static readonly MagicNetSerializer<Vector2> Vector2 = new MagicNetDelegateSerializer<Vector2>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out Vector2 v); return v; });
        public static readonly MagicNetSerializer<Vector3> Vector3 = new MagicNetDelegateSerializer<Vector3>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out Vector3 v); return v; });
        public static readonly MagicNetSerializer<Vector4> Vector4 = new MagicNetDelegateSerializer<Vector4>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out Vector4 v); return v; });
        public static readonly MagicNetSerializer<Vector2Int> Vector2Int = new MagicNetDelegateSerializer<Vector2Int>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out Vector2Int v); return v; });
        public static readonly MagicNetSerializer<Vector3Int> Vector3Int = new MagicNetDelegateSerializer<Vector3Int>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out Vector3Int v); return v; });
        public static readonly MagicNetSerializer<Quaternion> Quaternion = new MagicNetDelegateSerializer<Quaternion>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out Quaternion v); return v; });
        public static readonly MagicNetSerializer<Color> Color = new MagicNetDelegateSerializer<Color>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out Color v); return v; });
        public static readonly MagicNetSerializer<Color32> Color32 = new MagicNetDelegateSerializer<Color32>((w, v) => w.WriteValueSafe(v), r => { r.ReadValueSafe(out Color32 v); return v; });

        public static readonly MagicNetSerializer<GameObject> GameObject = MagicNetGameObjectReferenceSerializer.Instance;

        public static MagicNetSerializer<T> Enum<T>() where T : unmanaged, System.Enum => MagicNetEnumSerializer<T>.Instance;
        public static MagicNetSerializer<T> NetworkSerializable<T>() where T : INetworkSerializable, new() => MagicNetNetworkSerializableSerializer<T>.Instance;
        public static MagicNetSerializer<T> ComponentReference<T>() where T : Component => MagicNetComponentReferenceSerializer<T>.Instance;
        public static MagicNetSerializer<List<T>> ListOf<T>(MagicNetSerializer<T> element) => new MagicNetListSerializer<T>(element);
        public static MagicNetSerializer<T[]> ArrayOf<T>(MagicNetSerializer<T> element) => new MagicNetArraySerializer<T>(element);
    }
}
