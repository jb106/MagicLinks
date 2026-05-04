#if  UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace MagicLinks
{
    public static class MagicLinksUtilities
    {
        private static MagicLinksConfiguration _cachedConfiguration;
        private static string _cachedPackagePath;
        private static Dictionary<string, string> _cachedBaseTypes;
        private static readonly Dictionary<string, Texture2D> _iconCache = new Dictionary<string, Texture2D>();

        public static MagicLinksConfiguration GetConfiguration()
        {
            if (_cachedConfiguration != null) return _cachedConfiguration;

            if (Directory.Exists(MagicLinksConst.ConfigurationPath) == false)
                Directory.CreateDirectory(MagicLinksConst.ConfigurationPath);

            string fullPath = Path.Combine(MagicLinksConst.ConfigurationPath, MagicLinksConst.ConfigurationName);

            if (File.Exists(fullPath))
            {
                _cachedConfiguration = AssetDatabase.LoadAssetAtPath<MagicLinksConfiguration>(fullPath);
            }
            else
            {
                AssetDatabase.CreateAsset(new MagicLinksConfiguration(), fullPath);
                _cachedConfiguration = AssetDatabase.LoadAssetAtPath<MagicLinksConfiguration>(fullPath);
            }

            return _cachedConfiguration;
        }

        public static void CreateVariablesFolder()
        {
            if (Directory.Exists(MagicLinksConst.VariablesPath) == false)
                Directory.CreateDirectory(MagicLinksConst.VariablesPath);
        }

        public static void CreateListenersFolder()
        {
            if (Directory.Exists(MagicLinksConst.EventsListenersPath) == false)
                Directory.CreateDirectory(MagicLinksConst.EventsListenersPath);
            
            if (Directory.Exists(MagicLinksConst.VariablesListenersPath) == false)
                Directory.CreateDirectory(MagicLinksConst.VariablesListenersPath);
        }

        public static string GetPackageRelativePath(string subpath)
        {
            string packagePath = GetPackagePath();
            string path = string.Empty;

            if (packagePath != string.Empty)
            {
                path = Path.Combine(packagePath, subpath);
            }
            else
            {
                path = Path.Combine("Assets/MagicLinks", subpath);
            }

            return path.Replace("\\", "/");
        }

        public static string GetPackagePath()
        {
            if (_cachedPackagePath != null) return _cachedPackagePath;

            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);

            _cachedPackagePath = info == null ? string.Empty : info.assetPath;
            return _cachedPackagePath;
        }

        public static string GetPackageVersion()
        {
            string packagePath = GetPackagePath();
            string root = string.IsNullOrEmpty(packagePath) ? "Assets/MagicLinks" : packagePath;
            string jsonPath = Path.Combine(root, "package.json");
            if (!File.Exists(jsonPath)) return string.Empty;
            try
            {
                string content = File.ReadAllText(jsonPath);
                var match = System.Text.RegularExpressions.Regex.Match(content, "\"version\"\\s*:\\s*\"([^\"]+)\"");
                return match.Success ? match.Groups[1].Value : string.Empty;
            }
            catch { return string.Empty; }
        }

        public static List<string> GetAllTypes()
        {
            List<string> types = new List<string>();

            foreach (KeyValuePair<string, string> baseType in GetBaseTypes())
            {
                types.Add(baseType.Key);
            }

            foreach (string customType in MagicLinksUtilities.GetConfiguration().customTypes)
            {
                types.Add(customType);
            }

            return types;
        }

        public static string GetTrueType(string t)
        {
            Dictionary<string, string> baseTypes = GetBaseTypes();

            if (baseTypes.ContainsKey(t)) return baseTypes[t];
            else return t;
        }

        public static Dictionary<string, string> GetBaseTypes()
        {
            if (_cachedBaseTypes != null) return _cachedBaseTypes;

            _cachedBaseTypes = new Dictionary<string, string>
            {
                { MagicLinksConst.String, typeof(string).ToString() },
                { MagicLinksConst.Bool, typeof(bool).ToString() },
                { MagicLinksConst.Int, typeof(int).ToString() },
                { MagicLinksConst.Float, typeof(float).ToString() },
                { MagicLinksConst.Vector2, typeof(Vector2).ToString() },
                { MagicLinksConst.Vector3, typeof(Vector3).ToString() },
                { MagicLinksConst.GameObject, typeof(GameObject).ToString() },
                { MagicLinksConst.Transform, typeof(Transform).ToString() },
                { MagicLinksConst.Collider, typeof(Collider).ToString() },
                { MagicLinksConst.Color, typeof(Color).ToString() },
            };

            return _cachedBaseTypes;
        }

        public static Texture2D GetVariableIcon(DynamicVariable variable)
        {
            string spritesPath = GetPackageRelativePath(MagicLinksConst.SpritesPath);

            string fileName;
            if (variable.magicType == 2)
                fileName = "EventIcon_Void.png";
            else if (variable.magicType == 1)
                fileName = "EventIcon_" + variable.vLabelType + ".png";
            else
                fileName = "VariableIcon_" + variable.vLabelType + ".png";

            Texture2D icon = LoadIconCached(Path.Combine(spritesPath, fileName));
            if (icon != null) return icon;

            return LoadIconCached(Path.Combine(spritesPath, "VariableIcon_Custom.png"));
        }

        private static Texture2D LoadIconCached(string path)
        {
            string normalized = path.Replace("\\", "/");
            if (_iconCache.TryGetValue(normalized, out var cached) && cached != null) return cached;

            Texture2D icon = AssetDatabase.LoadAssetAtPath(normalized, typeof(Texture2D)) as Texture2D;
            if (icon != null) _iconCache[normalized] = icon;
            return icon;
        }

        public static string GetEventListenerName(string type)
        {
            return GetStringWithFirstUpperCaseLetter(type + MagicLinksConst.EventListenerName);
        }
        
        public static string GetVariableListenerName(string type)
        {
            return GetStringWithFirstUpperCaseLetter(type + MagicLinksConst.VariableListenerName);
        }

        public static string GetStringWithFirstUpperCaseLetter(string s)
        {
            return char.ToUpper(s[0]) + s.Substring(1);
        }
        
        public static void DisableFocusRecursive(VisualElement root)
        {
            if (root == null)
                return;

            root.focusable = false;

            foreach (var child in root.Children())
            {
                DisableFocusRecursive(child);
            }
        }
    }
}
#endif