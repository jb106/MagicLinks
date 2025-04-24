#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.UIElements;

namespace MagicLinks
{
    public class MagicLinkEditor : EditorWindow
    {
        public static MagicLinkEditor Instance;

        private VisualTreeAsset m_VisualTreeAsset;

        [MenuItem("Window/MagicLinkEditor")]
        public static void ShowExample()
        {
            MagicLinkEditor wnd = GetWindow<MagicLinkEditor>();
            wnd.titleContent = new GUIContent("MagicLinkEditor");
        }

        public void CreateGUI()
        {
            Instance = this;

            MagicLinksUtilities.GetConfiguration();

            m_VisualTreeAsset =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.UXMLPath));

            rootVisualElement.Add(m_VisualTreeAsset.Instantiate());

            HookEvents();
            MagicLinksCustomTypes.UpdateTypes();
            MagicLinksCategories.UpdateCategories();
            MagicLinksInternalVar.UpdateVariablesUI();

            MagicLinksScriptsGenerator.ClearListeners(false);
            MagicLinksScriptsGenerator.GenerateMagicVariablesScript(true);
            MagicLinksScriptsGenerator.GenerateListenersScripts();
        }

        private void HookEvents()
        {
            rootVisualElement.Q<Button>(MagicLinksConst.CreateTypeButtonClass).clicked +=
                MagicLinksCustomTypes.CreateType;
            rootVisualElement.Q<Button>(MagicLinksConst.CreateVariableButtonClass).clicked +=
                MagicLinksInternalVar.CreateVariable;
            rootVisualElement.Q<Button>(MagicLinksConst.RefreshScriptsButton).clicked += RefreshScripts;
            rootVisualElement.Q<Button>(MagicLinksConst.CreateCategoryButtonClass).clicked +=
                MagicLinksCategories.CreateCategory;
            rootVisualElement.Q<DropdownField>(MagicLinksConst.CategoriesDropdownClass)
                .RegisterValueChangedCallback((s) => { MagicLinksCategories.OnCategorySelected(s.newValue); });
        }

        private void RefreshScripts()
        {
            MagicLinksCustomTypes.UpdateTypes();
            MagicLinksCategories.UpdateCategories();
            MagicLinksInternalVar.UpdateVariablesUI();

            MagicLinksScriptsGenerator.ClearListeners(true);
            MagicLinksScriptsGenerator.GenerateMagicVariablesScript(false);
            MagicLinksScriptsGenerator.GenerateListenersScripts();
        }
    }
}
#endif