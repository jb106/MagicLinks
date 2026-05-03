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

            MagicLinksConfiguration config = MagicLinksUtilities.GetConfiguration();

            m_VisualTreeAsset =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.UXMLPath));

            rootVisualElement.Add(m_VisualTreeAsset.Instantiate());
            
            rootVisualElement.Q<Toggle>().SetValueWithoutNotify(config.enableRuntimeUI);

            HookEvents();
            SetupLeftPanelSplit();
            MagicLinksCustomTypes.UpdateTypes();
            MagicLinksCategories.UpdateCategories();
            MagicLinksInternalVar.UpdateVariablesUI();

            MagicLinksScriptsGenerator.GenerateMagicVariablesScript(true);
            MagicLinksScriptsGenerator.GenerateListenersScripts(true);
        }

        private void SetupLeftPanelSplit()
        {
            var splitView = rootVisualElement.Q<TwoPaneSplitView>(MagicLinksConst.MainSplitViewName);
            var leftPanel = rootVisualElement.Q<VisualElement>(MagicLinksConst.LeftPanelName);
            var collapseBtn = rootVisualElement.Q<Button>(MagicLinksConst.CollapseLeftPanelButton);
            var expandBtn = rootVisualElement.Q<Button>(MagicLinksConst.ExpandLeftPanelButton);
            if (splitView == null || leftPanel == null || collapseBtn == null || expandBtn == null) return;

            float savedWidth = EditorPrefs.GetFloat(MagicLinksConst.LeftPanelWidthKey,
                MagicLinksConst.LeftPanelDefaultWidth);
            splitView.fixedPaneInitialDimension = savedWidth;

            bool startCollapsed = EditorPrefs.GetBool(MagicLinksConst.LeftPanelCollapsedKey, false);
            ApplyLeftPanelCollapsed(splitView, expandBtn, startCollapsed);

            collapseBtn.clicked += () => ApplyLeftPanelCollapsed(splitView, expandBtn, true);
            expandBtn.clicked += () => ApplyLeftPanelCollapsed(splitView, expandBtn, false);

            // Persist width whenever the user drags the splitter (only when expanded)
            leftPanel.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (EditorPrefs.GetBool(MagicLinksConst.LeftPanelCollapsedKey, false)) return;
                float w = leftPanel.resolvedStyle.width;
                if (w > 50f && System.Math.Abs(EditorPrefs.GetFloat(MagicLinksConst.LeftPanelWidthKey, -1f) - w) > 0.5f)
                    EditorPrefs.SetFloat(MagicLinksConst.LeftPanelWidthKey, w);
            });
        }

        private static void ApplyLeftPanelCollapsed(TwoPaneSplitView splitView, Button expandBtn, bool collapsed)
        {
            if (collapsed) splitView.CollapseChild(0);
            else splitView.UnCollapse();

            expandBtn.style.display = collapsed ? DisplayStyle.Flex : DisplayStyle.None;
            EditorPrefs.SetBool(MagicLinksConst.LeftPanelCollapsedKey, collapsed);
        }

        private void HookEvents()
        {
            rootVisualElement.Q<Toggle>(MagicLinksConst.EnableRuntimeUIToggle).RegisterValueChangedCallback(evt => ChangeEnableRuntimeUI(evt.newValue));

            rootVisualElement.Q<Button>(MagicLinksConst.CreateTypeButtonClass).clicked += MagicLinksCustomTypes.CreateType;
            rootVisualElement.Q<Button>(MagicLinksConst.CreateVariableButtonClass).clicked += MagicLinksInternalVar.CreateVariable;
            rootVisualElement.Q<Button>(MagicLinksConst.RefreshScriptsButton).clicked += RefreshScripts;
            rootVisualElement.Q<Button>(MagicLinksConst.CreateCategoryButtonClass).clicked += MagicLinksCategories.CreateCategory;
            rootVisualElement.Q<DropdownField>(MagicLinksConst.CategoriesDropdownClass).RegisterValueChangedCallback((s) => { MagicLinksCategories.OnCategorySelected(s.newValue); });

            HookEnterShortcut(rootVisualElement.Q<TextField>(MagicLinksConst.VariableNameTextFieldClass), MagicLinksInternalVar.CreateVariable);
            HookEnterShortcut(rootVisualElement.Q<TextField>(MagicLinksConst.TypeTextFieldClass), MagicLinksCustomTypes.CreateType);
            HookEnterShortcut(rootVisualElement.Q<TextField>(MagicLinksConst.CreateCategoryNameTextFieldClass), MagicLinksCategories.CreateCategory);

            rootVisualElement.Q<TextField>(MagicLinksConst.VariablesSearchField)
                ?.RegisterValueChangedCallback(_ => MagicLinksInternalVar.UpdateVariablesUI());
            rootVisualElement.Q<DropdownField>(MagicLinksConst.VariablesSortDropdown)
                ?.RegisterValueChangedCallback(evt =>
                {
                    EditorPrefs.SetString(MagicLinksConst.VariablesSortKey, evt.newValue);
                    MagicLinksInternalVar.UpdateVariablesUI();
                });
            rootVisualElement.Q<DropdownField>(MagicLinksConst.VariablesMagicTypeFilterDropdown)
                ?.RegisterValueChangedCallback(evt =>
                {
                    EditorPrefs.SetString(MagicLinksConst.VariablesMagicTypeFilterKey, evt.newValue);
                    MagicLinksInternalVar.UpdateVariablesUI();
                });
        }

        private static void HookEnterShortcut(TextField field, System.Action onEnter)
        {
            if (field == null || onEnter == null) return;
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                onEnter();
                evt.StopPropagation();
            });
        }

        private void ChangeEnableRuntimeUI(bool enabled)
        {
            MagicLinksConfiguration configuration = MagicLinksUtilities.GetConfiguration();

            configuration.enableRuntimeUI = enabled;
            
            EditorUtility.SetDirty(configuration);
        }

        private void RefreshScripts()
        {
            MagicLinksCustomTypes.UpdateTypes();
            MagicLinksCategories.UpdateCategories();
            MagicLinksInternalVar.UpdateVariablesUI();

            AssetDatabase.StartAssetEditing();
            try
            {
                MagicLinksScriptsGenerator.ClearListeners(true);
                MagicLinksScriptsGenerator.GenerateMagicVariablesScript(false);
                MagicLinksScriptsGenerator.GenerateListenersScripts();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.Refresh();
        }
    }
}
#endif