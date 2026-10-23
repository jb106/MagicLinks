#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace MagicLinks
{
    public class MagicNetFieldDescription
    {
        public FieldInfo field;
        public MagicNetFieldMode storedMode;
        public MagicNetFieldMode effectiveMode;
        /// <summary>Serializer expression, null when the field is excluded or invalid.</summary>
        public string serializer;
        public string error;
        public List<MagicNetTypeDescription> nestedTypes = new List<MagicNetTypeDescription>();
    }

    public class MagicNetTypeDescription
    {
        public Type type;
        public string serializerClassName;
        public List<MagicNetFieldDescription> fields = new List<MagicNetFieldDescription>();
        public List<string> errors = new List<string>();
    }

    public class MagicNetLinkDescription
    {
        public DynamicVariable variable;
        public ushort id;
        public string dictionary;
        /// <summary>Serializer expression, null for void events or when invalid.</summary>
        public string serializer;
        public List<string> errors = new List<string>();
    }

    public class MagicLinksNetworkAnalysis
    {
        public readonly List<MagicNetLinkDescription> links = new List<MagicNetLinkDescription>();
        /// <summary>Custom types used by networked links, dependencies first.</summary>
        public readonly List<MagicNetTypeDescription> types = new List<MagicNetTypeDescription>();

        public bool HasErrors => links.Any(l => l.errors.Count > 0);

        public IEnumerable<string> Errors =>
            links.SelectMany(l => l.errors.Select(e => $"'{l.variable.vName}' ({l.variable.vLabelType}): {e}"));

        public MagicNetLinkDescription GetLink(string variableName) =>
            links.FirstOrDefault(l => l.variable.vName == variableName);
    }

    /// <summary>
    /// Decides, at edit time and by reflection, how every networked link and every field of its type
    /// travels on the network. Anything that cannot travel without an explicit choice is reported as an error.
    /// Has no dependency on the netcode package: netcode types are recognized by name.
    /// </summary>
    public class MagicLinksNetworkAnalyzer
    {
        public const int MaxNetworkedFieldsPerType = 64;

        private const string NetworkSerializableInterface = "Unity.Netcode.INetworkSerializable";

        private static readonly Dictionary<Type, string> LeafSerializers = new Dictionary<Type, string>
        {
            { typeof(bool), "MagicNetSerializers.Bool" },
            { typeof(byte), "MagicNetSerializers.Byte" },
            { typeof(sbyte), "MagicNetSerializers.SByte" },
            { typeof(short), "MagicNetSerializers.Short" },
            { typeof(ushort), "MagicNetSerializers.UShort" },
            { typeof(int), "MagicNetSerializers.Int" },
            { typeof(uint), "MagicNetSerializers.UInt" },
            { typeof(long), "MagicNetSerializers.Long" },
            { typeof(ulong), "MagicNetSerializers.ULong" },
            { typeof(float), "MagicNetSerializers.Float" },
            { typeof(double), "MagicNetSerializers.Double" },
            { typeof(char), "MagicNetSerializers.Char" },
            { typeof(string), "MagicNetSerializers.String" },
            { typeof(Vector2), "MagicNetSerializers.Vector2" },
            { typeof(Vector3), "MagicNetSerializers.Vector3" },
            { typeof(Vector4), "MagicNetSerializers.Vector4" },
            { typeof(Vector2Int), "MagicNetSerializers.Vector2Int" },
            { typeof(Vector3Int), "MagicNetSerializers.Vector3Int" },
            { typeof(Quaternion), "MagicNetSerializers.Quaternion" },
            { typeof(Color), "MagicNetSerializers.Color" },
            { typeof(Color32), "MagicNetSerializers.Color32" },
        };

        private readonly MagicLinksConfiguration _config;
        private readonly Dictionary<Type, MagicNetTypeDescription> _described = new Dictionary<Type, MagicNetTypeDescription>();
        private readonly List<MagicNetTypeDescription> _ordered = new List<MagicNetTypeDescription>();
        private readonly HashSet<Type> _inProgress = new HashSet<Type>();

        public MagicLinksNetworkAnalyzer(MagicLinksConfiguration config)
        {
            _config = config;
        }

        public static MagicLinksNetworkAnalysis Analyze(IEnumerable<DynamicVariable> variables, MagicLinksConfiguration config)
        {
            MagicLinksNetworkAnalyzer analyzer = new MagicLinksNetworkAnalyzer(config);
            MagicLinksNetworkAnalysis analysis = new MagicLinksNetworkAnalysis();

            // Ids follow the name order so that every build produces the same ids.
            ushort id = 1;
            foreach (DynamicVariable v in variables.Where(v => v.isNetworked).OrderBy(v => v.vName, StringComparer.Ordinal))
            {
                MagicNetLinkDescription link = new MagicNetLinkDescription
                {
                    variable = v,
                    id = id++,
                    dictionary = GetDictionaryName(v)
                };

                if (v.IsVoid() == false)
                {
                    Type type = ResolveLinkType(v.vLabelType);

                    if (type == null)
                    {
                        link.errors.Add($"type '{v.vLabelType}' does not exist.");
                    }
                    else
                    {
                        // A list variable is synchronized item by item (its serializer is the item's),
                        // a list event is sent as a whole list.
                        string serializer = analyzer.GetLinkSerializer(type, link.errors);
                        if (serializer != null && v.isList && v.IsEvent()) serializer = $"MagicNetSerializers.ListOf({serializer})";
                        link.serializer = link.errors.Count == 0 ? serializer : null;
                    }
                }

                analysis.links.Add(link);
            }

            analysis.types.AddRange(analyzer._ordered);
            return analysis;
        }

        /// <summary>The type itself and every custom type reachable from its fields, for the editor UI.</summary>
        public List<MagicNetTypeDescription> DescribeForEditor(Type root)
        {
            List<string> ignored = new List<string>();
            GetLinkSerializer(root, ignored);

            if (_described.TryGetValue(root, out MagicNetTypeDescription description) == false)
                return new List<MagicNetTypeDescription>();

            List<MagicNetTypeDescription> result = new List<MagicNetTypeDescription>();
            CollectReachable(description, result);
            return result;
        }

        public static void CollectErrors(MagicNetTypeDescription description, List<string> errors) =>
            CollectErrors(description, errors, new HashSet<Type>());

        //----------------------------------------

        private string GetLinkSerializer(Type type, List<string> errors)
        {
            // A Unity object stored in a link can only be a reference to a network object.
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                string reference = ResolveReferenceSerializer(type, out string referenceError);
                if (referenceError != null) errors.Add(referenceError);
                return reference;
            }

            List<MagicNetTypeDescription> nested = new List<MagicNetTypeDescription>();
            string serializer = ResolveValueSerializer(type, nested, out string error);

            if (error != null) errors.Add(error);
            foreach (MagicNetTypeDescription description in nested) CollectErrors(description, errors, new HashSet<Type>());

            return serializer;
        }

        private string ResolveValueSerializer(Type type, List<MagicNetTypeDescription> nested, out string error)
        {
            error = null;

            if (LeafSerializers.TryGetValue(type, out string leaf)) return leaf;

            if (type.IsEnum) return $"MagicNetSerializers.Enum<{GetCSharpName(type)}>()";

            if (IsNetworkSerializable(type))
            {
                if (type.IsValueType || type.GetConstructor(Type.EmptyTypes) != null)
                    return $"MagicNetSerializers.NetworkSerializable<{GetCSharpName(type)}>()";

                error = $"{type.Name} implements INetworkSerializable but has no public parameterless constructor.";
                return null;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                string element = ResolveValueSerializer(type.GetGenericArguments()[0], nested, out error);
                return element != null ? $"MagicNetSerializers.ListOf({element})" : null;
            }

            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1)
                {
                    error = "multidimensional arrays cannot be sent. Use a List or choose Excluded.";
                    return null;
                }

                string element = ResolveValueSerializer(type.GetElementType(), nested, out error);
                return element != null ? $"MagicNetSerializers.ArrayOf({element})" : null;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                error = typeof(GameObject) == type || typeof(Component).IsAssignableFrom(type)
                    ? $"{type.Name} is a scene object and cannot be copied by value. Choose Reference (it must have a spawned NetworkObject) or Excluded."
                    : $"{type.Name} is a Unity asset and cannot be sent. Choose Excluded.";
                return null;
            }

            if (typeof(Delegate).IsAssignableFrom(type) || typeof(UnityEventBase).IsAssignableFrom(type))
            {
                error = $"{type.Name} is a callback and cannot be sent. Choose Excluded.";
                return null;
            }

            if (type == typeof(object) || type.IsInterface || type.IsAbstract || type.IsPointer)
            {
                error = $"{type.Name} has no concrete type known in advance and cannot be sent. Choose Excluded.";
                return null;
            }

            if (type.IsGenericType || type.IsPrimitive || type == typeof(decimal))
            {
                error = $"{GetFriendlyName(type)} is not supported on the network. Use a List, a supported type, or choose Excluded.";
                return null;
            }

            MagicNetTypeDescription description = Describe(type, out error);
            if (description == null) return null;

            nested.Add(description);
            return $"{description.serializerClassName}.Instance";
        }

        private static string ResolveReferenceSerializer(Type type, out string error)
        {
            error = null;

            if (type == typeof(GameObject)) return "MagicNetSerializers.GameObject";

            if (typeof(Component).IsAssignableFrom(type))
                return $"MagicNetSerializers.ComponentReference<{GetCSharpName(type)}>()";

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                string element = ResolveReferenceSerializer(type.GetGenericArguments()[0], out error);
                return element != null ? $"MagicNetSerializers.ListOf({element})" : null;
            }

            if (type.IsArray && type.GetArrayRank() == 1)
            {
                string element = ResolveReferenceSerializer(type.GetElementType(), out error);
                return element != null ? $"MagicNetSerializers.ArrayOf({element})" : null;
            }

            error = $"Reference only works with GameObject or Component types (or lists of them), not {GetFriendlyName(type)}.";
            return null;
        }

        private MagicNetTypeDescription Describe(Type type, out string error)
        {
            error = null;

            if (_described.TryGetValue(type, out MagicNetTypeDescription existing)) return existing;

            if (_inProgress.Add(type) == false)
            {
                error = $"{type.Name} contains itself (directly or through other types). Recursive types are not supported: choose Excluded on one of the fields.";
                return null;
            }

            MagicNetTypeDescription description = new MagicNetTypeDescription
            {
                type = type,
                serializerClassName = "MagicNet_" + Sanitize(type.FullName)
            };

            if (type.IsVisible == false && type.Assembly.GetName().Name != "Assembly-CSharp")
                description.errors.Add("the type must be public to be used by the generated network code.");

            if (type.IsValueType == false && type.GetConstructor(Type.EmptyTypes) == null)
                description.errors.Add("a class needs a public parameterless constructor to be received.");

            string typeName = type.FullName;

            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                MagicNetFieldDescription fieldDescription = new MagicNetFieldDescription
                {
                    field = field,
                    storedMode = _config is null ? MagicNetFieldMode.Unset : _config.GetFieldMode(typeName, field.Name)
                };

                ResolveField(fieldDescription);
                description.fields.Add(fieldDescription);
            }

            int networkedCount = description.fields.Count(f => f.serializer != null);
            if (networkedCount > MaxNetworkedFieldsPerType)
                description.errors.Add($"{networkedCount} networked fields, the maximum is {MaxNetworkedFieldsPerType}. Exclude some fields or split the type.");

            _inProgress.Remove(type);
            _described.Add(type, description);
            _ordered.Add(description);

            return description;
        }

        private void ResolveField(MagicNetFieldDescription f)
        {
            FieldInfo field = f.field;
            string error = null;

            switch (f.storedMode)
            {
                case MagicNetFieldMode.Excluded:
                    f.effectiveMode = MagicNetFieldMode.Excluded;
                    return;

                case MagicNetFieldMode.Value:
                    f.effectiveMode = MagicNetFieldMode.Value;
                    f.serializer = field.IsInitOnly ? null : ResolveValueSerializer(field.FieldType, f.nestedTypes, out error);
                    if (field.IsInitOnly) error = "readonly fields cannot be received. Choose Excluded.";
                    break;

                case MagicNetFieldMode.Reference:
                    f.effectiveMode = MagicNetFieldMode.Reference;
                    f.serializer = field.IsInitOnly ? null : ResolveReferenceSerializer(field.FieldType, out error);
                    if (field.IsInitOnly) error = "readonly fields cannot be received. Choose Excluded.";
                    break;

                default:
                    // No choice made: obvious cases are decided, anything else must be chosen explicitly.
                    if (field.IsInitOnly || field.IsNotSerialized)
                    {
                        f.effectiveMode = MagicNetFieldMode.Excluded;
                        return;
                    }

                    f.serializer = ResolveValueSerializer(field.FieldType, f.nestedTypes, out error);
                    f.effectiveMode = f.serializer != null ? MagicNetFieldMode.Value : MagicNetFieldMode.Unset;
                    break;
            }

            if (error != null)
            {
                f.error = error;
                f.serializer = null;
            }
        }

        private static void CollectErrors(MagicNetTypeDescription description, List<string> errors, HashSet<Type> visited)
        {
            if (visited.Add(description.type) == false) return;

            foreach (string error in description.errors)
                errors.Add($"{description.type.Name}: {error}");

            foreach (MagicNetFieldDescription field in description.fields)
            {
                if (field.error != null) errors.Add($"{description.type.Name}.{field.field.Name}: {field.error}");

                foreach (MagicNetTypeDescription nested in field.nestedTypes)
                    CollectErrors(nested, errors, visited);
            }
        }

        private static void CollectReachable(MagicNetTypeDescription description, List<MagicNetTypeDescription> result)
        {
            if (result.Contains(description)) return;
            result.Add(description);

            foreach (MagicNetFieldDescription field in description.fields)
            foreach (MagicNetTypeDescription nested in field.nestedTypes)
                CollectReachable(nested, result);
        }

        //----------------------------------------

        public static string GetDictionaryName(DynamicVariable v)
        {
            if (v.IsVoid()) return "VOID";

            string name = v.vLabelType.ToUpper();
            if (v.isList) name += MagicLinksConst.ListDict;
            if (v.magicType == 1) name += MagicLinksConst.EventDict;
            return name;
        }

        public static Type ResolveLinkType(string labelType)
        {
            switch (labelType)
            {
                case MagicLinksConst.String: return typeof(string);
                case MagicLinksConst.Bool: return typeof(bool);
                case MagicLinksConst.Int: return typeof(int);
                case MagicLinksConst.Float: return typeof(float);
                case MagicLinksConst.Vector2: return typeof(Vector2);
                case MagicLinksConst.Vector3: return typeof(Vector3);
                case MagicLinksConst.GameObject: return typeof(GameObject);
                case MagicLinksConst.Transform: return typeof(Transform);
                case MagicLinksConst.Collider: return typeof(Collider);
                case MagicLinksConst.Color: return typeof(Color);
            }

            return FindType(labelType);
        }

        /// <summary>Same lookup as the generated MagicLinksManager: custom types are referenced by their simple name.</summary>
        public static Type FindType(string typeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }

                foreach (Type type in types)
                {
                    if (type.Name == typeName) return type;
                }
            }

            return null;
        }

        public static bool IsNetworkSerializable(Type type) =>
            type.GetInterfaces().Any(i => i.FullName == NetworkSerializableInterface);

        public static string GetCSharpName(Type type)
        {
            if (type.IsArray) return GetCSharpName(type.GetElementType()) + "[]";

            if (type.IsGenericType)
            {
                string definition = type.GetGenericTypeDefinition().FullName;
                definition = definition.Substring(0, definition.IndexOf('`')).Replace('+', '.');
                string arguments = string.Join(", ", type.GetGenericArguments().Select(GetCSharpName));
                return $"global::{definition}<{arguments}>";
            }

            return "global::" + type.FullName.Replace('+', '.');
        }

        public static string GetFriendlyName(Type type)
        {
            if (type.IsArray) return GetFriendlyName(type.GetElementType()) + "[]";
            if (type.IsGenericType == false) return type.Name;

            string name = type.Name.Substring(0, type.Name.IndexOf('`'));
            return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(GetFriendlyName))}>";
        }

        private static string Sanitize(string s)
        {
            char[] chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (char.IsLetterOrDigit(chars[i]) == false) chars[i] = '_';
            }

            return new string(chars);
        }
    }
}
#endif
