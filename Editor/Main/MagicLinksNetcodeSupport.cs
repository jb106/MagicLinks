#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEngine;

namespace MagicLinks
{
    /// <summary>
    /// Keeps the MAGICLINKS_NETCODE scripting define in sync with the presence of Netcode for GameObjects.
    /// The define guards the generated network script, which lives in the game assembly.
    /// (The MagicLinks.Netcode assembly gets the same define through its own versionDefines.)
    /// </summary>
    [InitializeOnLoad]
    public static class MagicLinksNetcodeSupport
    {
        public static bool IsNetcodeInstalled =>
            Type.GetType("Unity.Netcode.NetworkManager, Unity.Netcode.Runtime") != null;

        static MagicLinksNetcodeSupport()
        {
            Events.registeringPackages -= OnRegisteringPackages;
            Events.registeringPackages += OnRegisteringPackages;

            EditorApplication.delayCall += () => SetDefine(IsNetcodeInstalled);
        }

        private static void OnRegisteringPackages(PackageRegistrationEventArgs args)
        {
            // Before the package goes away: remove what depends on it, otherwise the game assembly stops compiling.
            if (args.removed.Any(p => p.name == MagicLinksConst.NetcodePackageName))
            {
                MagicLinksNetworkScriptsGenerator.DeleteNetworkScript();
                SetDefine(false);
            }
        }

        private static void SetDefine(bool enabled)
        {
            NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            List<string> defines = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            bool hasDefine = defines.Contains(MagicLinksConst.NetcodeDefine);
            if (hasDefine == enabled) return;

            if (enabled) defines.Add(MagicLinksConst.NetcodeDefine);
            else defines.Remove(MagicLinksConst.NetcodeDefine);

            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
        }
    }

    /// <summary>A build never ships networked links that would not synchronize.</summary>
    public class MagicLinksNetworkBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            bool hasNetworkedLinks = MagicLinksInternalVar.GetExistingVariables().Any(v => v.isNetworked);
            if (hasNetworkedLinks == false) return;

            if (MagicLinksNetcodeSupport.IsNetcodeInstalled == false)
            {
                Debug.LogWarning("[MagicLinks] Some links are marked as networked but Netcode for GameObjects is not installed: they stay local.");
                return;
            }

            string expected = MagicLinksNetworkScriptsGenerator.GetNetworkScriptContent(out MagicLinksNetworkAnalysis analysis);

            if (analysis.HasErrors)
            {
                throw new BuildFailedException("[MagicLinks] Networked links have errors:\n- " +
                                               string.Join("\n- ", analysis.Errors));
            }

            string path = MagicLinksNetworkScriptsGenerator.NetworkScriptPath;
            if (System.IO.File.Exists(path) == false || System.IO.File.ReadAllText(path) != expected)
            {
                throw new BuildFailedException("[MagicLinks] The network script is out of date. " +
                                               "Click 'Refresh Scripts' in the MagicLinkEditor, then build again.");
            }
        }
    }
}
#endif
