using System;
using System.Linq;
using UnityEngine;

namespace MagicLinks
{
    public static class MagicLinksConst
    {
        public const string ConfigurationPath = "Assets/Resources/MagicLinks/";
        public const string EventsListenersPath = "Assets/Resources/MagicLinks/EventsListeners/";
        public const string VariablesListenersPath = "Assets/Resources/MagicLinks/VariablesListeners/";
        public const string ConfigurationName = "MagicLinksConfiguration.asset";

        public const string VariablesPath = "Assets/Resources/MagicLinks/Links/";
        public const string VariablesResourcesPath = "MagicLinks/Links/";

        public const string EventDict = "_EVENT";
        public const string ListDict = "_LIST";

        public const string EventListenerName = "_EventListener";
        public const string VariableListenerName = "_VariableListener";

        public const string MagicTypeVariable = "Variable";
        public const string MagicTypeEvent = "Event";
        public const string MagicTypeEventVoid = "EventVoid";

        public const string CategoryNone = "None";

        public const string String = "string";
        public const string Bool = "bool";
        public const string Int = "int";
        public const string Float = "float";
        public const string Vector2 = "Vector2";
        public const string Vector3 = "Vector3";
        public const string GameObject = "GameObject";
        public const string Transform = "Transform";
        public const string Collider = "Collider";
        public const string Color = "Color";

        public const string SpritesPath = "Editor/Sprites";

        public const string UXMLPath = "Editor/UI/MagicLinkEditor.uxml";
        public const string UXMLVariablePath = "Editor/UI/Variable.uxml";
        public const string UXMLVariableHeaderPath = "Editor/UI/VariableHeader.uxml";
        public const string UXMLCustomTypeElement = "Editor/UI/CustomTypeElement.uxml";
        
        public const string RuntimeLinksUIPrefab = "Runtime/RuntimeLinks.prefab";
        
        public const string UXMLRuntimeFieldsUIPath = "Runtime/UI/Fields/";
        public const string UXMLRuntimeLinkItemPath = "Runtime/UI/RuntimeLinkItem.uxml";
        public const string UXMLRuntimeLinkHeaderPath = "Runtime/UI/RuntimeLinkHeader.uxml";

        public const string MagicVariablesTemplate = "Editor/Various/MagicVariablesTemplate.cs";
        public const string MagicVariableClassName = "MagicLinksManager";

        public const string TemplateListenerSeparation = "#SEPARATION";

        public const string VariableDictTemplate =
            "public Dictionary<string, MagicVariableObservable<TYPE>> NAME = new();";

        public const string ListVariableDictTemplate =
            "public Dictionary<string, MagicListVariableObservable<TYPE>> NAME = new();";

        public const string EventDictTemplate = "public Dictionary<string, MagicEventObservable<TYPE>> NAME = new();";
        public const string ListEventDictTemplate = "public Dictionary<string, MagicEventObservable<List<TYPE>>> NAME = new();";
        public const string EventVoidDictTemplate = "public Dictionary<string, MagicEventVoidObservable> VOID = new();";

        public const string VariableGetterTemplate =
            "public static Dictionary<string, MagicVariableObservable<TYPE>> NAME = MagicLinksManager.Instance.NAME;";

        public const string ListVariableGetterTemplate =
            "public static Dictionary<string, MagicListVariableObservable<TYPE>> NAME = MagicLinksManager.Instance.NAME;";

        public const string EventGetterTemplate =
            "public static Dictionary<string, MagicEventObservable<TYPE>> SHORT = MagicLinksManager.Instance.NAME;";

        public const string ListEventGetterTemplate =
            "public static Dictionary<string, MagicEventObservable<List<TYPE>>> SHORT = MagicLinksManager.Instance.NAME;";

        public const string EventVoidGetterTemplate =
            "public static Dictionary<string, MagicEventVoidObservable> VOID = MagicLinksManager.Instance.VOID;";

        public const string RefreshScriptsButton = "RefreshScripts";
        
        public const string EnableRuntimeUIToggle = "EnableRuntimeUI";

        public const string CreateTypeButtonClass = "CreateTypeButton";
        public const string CreateVariableButtonClass = "CreateVariableButton";
        public const string TypeTextFieldClass = "TypeName";
        public const string VariableNameTextFieldClass = "VariableName";
        public const string VariablesContainerVisualElementClass = "VariablesContainer";

        public const string CategoriesDropdownClass = "CategoriesDropdown";
        public const string CreateCategoryNameTextFieldClass = "CategoryName";
        public const string CreateCategoryButtonClass = "CreateCategoryButton";
        public const string CreateCategoryFoldoutClass = "CategoriesFoldout";

        public const string SingleVariableIcon = "Icon";
        public const string SingleVariableName = "Name";
        public const string SingleVariableMagicType = "MagicType";
        public const string SingleVariableCategory = "Category";
        public const string SingleVariableType = "Type";
        public const string SingleVariableIsList = "IsList";
        public const string SingleVariableIsListLabel = "IsListLabel";
        public const string SingleVariableDeleteButton = "DeleteButton";
        public const string SingleVariableExpandToggle = "ExpandToggle";
        public const string SingleVariableInitialValueContainer = "InitialValueContainer";
        public const string SingleVariableSettings = "Settings";

        public const string ExpandSessionKeyPrefix = "MagicLinks_Var_Expanded_";
        public const string ExpandedArrow = "▾";
        public const string CollapsedArrow = "▸";

        public const string MainSplitViewName = "MainSplitView";
        public const string LeftPanelName = "LeftPanel";
        public const string CollapseLeftPanelButton = "CollapseLeftPanel";
        public const string ExpandLeftPanelButton = "ExpandLeftPanel";

        public const string LeftPanelCollapsedKey = "MagicLinks_LeftPanelCollapsed";
        public const string LeftPanelWidthKey = "MagicLinks_LeftPanelWidth";
        public const float LeftPanelDefaultWidth = 240f;

        public const string VariablesSearchField = "VariablesSearch";
        public const string VariablesSortDropdown = "VariablesSort";
        public const string VariablesMagicTypeFilterDropdown = "VariablesMagicTypeFilter";

        public const string VariablesSortKey = "MagicLinks_VariablesSort";
        public const string VariablesMagicTypeFilterKey = "MagicLinks_VariablesMagicTypeFilter";
        public const string VariablesCategoryFilterKey = "MagicLinks_VariablesCategoryFilter";
        public const string VariablesSearchKey = "MagicLinks_VariablesSearch";

        public const string SortDefault = "Default";
        public const string SortNameAsc = "Name (A-Z)";
        public const string SortNameDesc = "Name (Z-A)";
        public const string SortNewest = "Newest";
        public const string SortOldest = "Oldest";

        public const string MagicTypeFilterAll = "All";

        public const string AdvancedSettingsFoldout = "AdvancedSettingsFoldout";

        public const string CustomTypesFoldout = "CustomTypesFoldout";
        public const string CustomTypeElementName = "CustomTypeName";
        public const string CustomTypeElementDeleteButton = "DeleteCustomType";

        public static bool DoesTypeExist(string typeName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Any(t => t.Name == typeName);
        }

        public static string GetRuntimeField(string t)
        {
            return UXMLRuntimeFieldsUIPath + t.ToLower() + ".uxml";
        }
    }
}