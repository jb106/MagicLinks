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
        public static MagicLinksConfiguration GetConfiguration()
        {
            if (Directory.Exists(MagicLinksConst.ConfigurationPath) == false)
                Directory.CreateDirectory(MagicLinksConst.ConfigurationPath);

            AssetDatabase.Refresh();

            string fullPath = Path.Combine(MagicLinksConst.ConfigurationPath, MagicLinksConst.ConfigurationName);

            if (File.Exists(fullPath))
            {
                return AssetDatabase.LoadAssetAtPath<MagicLinksConfiguration>(fullPath);
            }
            else
            {
                AssetDatabase.CreateAsset(new MagicLinksConfiguration(), fullPath);
                return AssetDatabase.LoadAssetAtPath<MagicLinksConfiguration>(fullPath);
            }
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
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);

            if (info == null) return string.Empty;

            return info.assetPath;
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
            Dictionary<string, string> baseTypes = new Dictionary<string, string>();

            baseTypes.Add(MagicLinksConst.String, typeof(string).ToString());
            baseTypes.Add(MagicLinksConst.Bool, typeof(bool).ToString());
            baseTypes.Add(MagicLinksConst.Int, typeof(int).ToString());
            baseTypes.Add(MagicLinksConst.Float, typeof(float).ToString());
            baseTypes.Add(MagicLinksConst.Vector2, typeof(Vector2).ToString());
            baseTypes.Add(MagicLinksConst.Vector3, typeof(Vector3).ToString());
            baseTypes.Add(MagicLinksConst.GameObject, typeof(GameObject).ToString());
            baseTypes.Add(MagicLinksConst.Transform, typeof(Transform).ToString());
            baseTypes.Add(MagicLinksConst.Collider, typeof(Collider).ToString());
            baseTypes.Add(MagicLinksConst.Color, typeof(Color).ToString());

            return baseTypes;
        }

        public static Texture2D GetVariableIcon(DynamicVariable variable)
        {
            string spritesPath = MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.SpritesPath);

            string[] filesPath = Directory.GetFiles(spritesPath);

            foreach (string p in filesPath)
            {
                if (Path.GetExtension(p) != ".png") continue;

                string baseName = string.Empty;

                if (variable.magicType == 0)
                {
                    baseName = "VariableIcon_";
                }
                else if (variable.magicType == 1)
                {
                    baseName = "EventIcon_";
                }
                else if (variable.magicType == 2)
                {
                    if (Path.GetFileNameWithoutExtension(p) == "EventIcon_Void")
                        return AssetDatabase.LoadAssetAtPath(p, typeof(Texture2D)) as Texture2D;
                }

                if (Path.GetFileNameWithoutExtension(p) == baseName + variable.vLabelType)
                    return AssetDatabase.LoadAssetAtPath(p, typeof(Texture2D)) as Texture2D;
            }

            string customIconPath = Path.Combine(spritesPath, "VariableIcon_Custom.png");
            return AssetDatabase.LoadAssetAtPath(customIconPath, typeof(Texture2D)) as Texture2D;
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