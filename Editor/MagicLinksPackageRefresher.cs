#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace MagicLinks
{
    [InitializeOnLoad]
    public static class MagicLinksPackageRefresher
    {
        private const string VersionKey = "MagicLinks_LastPackageVersion";

        static MagicLinksPackageRefresher()
        {
            string currentVersion = GetCurrentPackageVersion();
            if (string.IsNullOrEmpty(currentVersion))
                return;

            string storedVersion = EditorPrefs.GetString(VersionKey, string.Empty);
            if (storedVersion != currentVersion)
            {
                RefreshScripts();
                EditorPrefs.SetString(VersionKey, currentVersion);
            }
        }

        private static string GetCurrentPackageVersion()
        {
            string packagePath = MagicLinksUtilities.GetPackagePath();
            if (string.IsNullOrEmpty(packagePath))
                return string.Empty;

            string jsonPath = Path.Combine(packagePath, "package.json");
            if (!File.Exists(jsonPath))
                return string.Empty;

            string content = File.ReadAllText(jsonPath);
            Match match = Regex.Match(content, "\"version\"\\s*:\\s*\"([^\"]+)\"");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static void RefreshScripts()
        {
            MagicLinksScriptsGenerator.ClearListeners(true);
            MagicLinksScriptsGenerator.GenerateMagicVariablesScript(false);
            MagicLinksScriptsGenerator.GenerateListenersScripts();
            AssetDatabase.Refresh();
        }
    }
}
#endif