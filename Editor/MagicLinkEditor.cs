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

public class MagicLinkEditor : EditorWindow
{
    private VisualTreeAsset m_VisualTreeAsset;

    const string ResourcesPath = "Assets/Resources";
    const string SpritesPath = "Editor/Sprites";
    
    const string UXMLPath = "Editor/UI/MagicLinkEditor.uxml";
    const string UXMLVariablePath = "Editor/UI/Variable.uxml";
    const string UXMLCustomTypeElement = "Editor/UI/CustomTypeElement.uxml";
    
    const string MagicVariablesTemplate = "Editor/MagicVariablesTemplate.cs";
    const string MagicVariableClassName = "MagicLinksManager";
    
    const string VariableDictTemplate = "public Dictionary<string, MagicVariableObservable<TYPE>> NAME = new();";
    const string EventDictTemplate = "public Dictionary<string, MagicEventObservable<TYPE>> NAME = new();";
    const string EventVoidDictTemplate = "public Dictionary<string, MagicEventVoidObservable> VOID = new();";
    
    const string VariableGetterTemplate = "public static Dictionary<string, MagicVariableObservable<TYPE>> NAME = MagicLinksManager.Instance.NAME;";
    const string EventGetterTemplate = "public static Dictionary<string, MagicEventObservable<TYPE>> SHORT = MagicLinksManager.Instance.NAME;";
    const string EventVoidGetterTemplate = "public static Dictionary<string, MagicEventVoidObservable> VOID = MagicLinksManager.Instance.VOID;";

    const string RefreshScriptsButton = "RefreshScripts";
    
    const string CreateTypeButtonClass = "CreateTypeButton";
    const string CreateVariableButtonClass = "CreateVariableButton";
    const string TypeTextFieldClass = "TypeName";
    const string VariableNameTextFieldClass = "VariableName";
    const string VariablesContainerVisualElementClass = "VariablesContainer";
    
    const string CategoriesDropdownClass = "CategoriesDropdown";
    const string CreateCategoryNameTextFieldClass = "CategoryName";
    const string CreateCategoryButtonClass = "CreateCategoryButton";
    const string CreateCategoryFoldoutClass = "CategoriesFoldout";
    
    const string SingleVariableIcon = "Icon";
    const string SingleVariableName = "Name";
    const string SingleVariableMagicType = "MagicType";
    const string SingleVariableCategory = "Category";
    const string SingleVariableType = "Type";
    const string SingleVariableDeleteButton = "DeleteButton";
    
    const string CustomTypesFoldout = "CustomTypesFoldout";
    const string CustomTypeElementName = "CustomTypeName";
    const string CustomTypeElementDeleteButton = "DeleteCustomType";

    [MenuItem("Window/MagicLinkEditor")]
    public static void ShowExample()
    {
        MagicLinkEditor wnd = GetWindow<MagicLinkEditor>();
        wnd.titleContent = new GUIContent("MagicLinkEditor");
    }

    public void CreateGUI()
    {
        GetConfiguration();
        
        m_VisualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(GetPackageRelativePath(UXMLPath));
        
        rootVisualElement.Add(m_VisualTreeAsset.Instantiate());
        
        HookEvents();
        UpdateTypes();
        UpdateCategories();
        UpdateVariablesUI();
        
        GenerateMagicVariablesScript(true);
    }

    private void HookEvents()
    {
        rootVisualElement.Q<Button>(CreateTypeButtonClass).clicked += CreateType;
        rootVisualElement.Q<Button>(CreateVariableButtonClass).clicked += CreateVariable;
        rootVisualElement.Q<Button>(RefreshScriptsButton).clicked += RefreshScripts;
        rootVisualElement.Q<Button>(CreateCategoryButtonClass).clicked += CreateCategory;
        rootVisualElement.Q<DropdownField>(CategoriesDropdownClass).RegisterValueChangedCallback((s) => {OnCategorySelected(s.newValue);});
    }

    private void RefreshScripts()
    {
        UpdateTypes();
        UpdateCategories();
        UpdateVariablesUI();
        
        GenerateMagicVariablesScript(false);
    }

    //CREATE TYPE
    //---------------------------------
    //---------------------------------
    private void CreateType()
    {
        string type = rootVisualElement.Q<TextField>(TypeTextFieldClass).value;

        if (MagicLinksUtilities.DoesTypeExist(type) == false)
        {
            Debug.LogError("Type does not exist");
            return;
        }

        MagicLinksConfiguration config = GetConfiguration();

        Debug.Log(type);
        if (type == string.Empty) return;
        if (config.customTypes.Contains(type) || GetBaseTypes().ContainsValue(type)) return;
            
        config.customTypes.Add(type);
        EditorUtility.SetDirty(config);
        UpdateTypes();
        UpdateVariablesUI();
        GenerateMagicVariablesScript(false);
    }

    private void UpdateTypes()
    {
        //Updates custom types foldout
        
        Foldout foldout = rootVisualElement.Q<Foldout>(CustomTypesFoldout);
        
        foldout.Clear();
        
        VisualTreeAsset customTypeUXML = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(GetPackageRelativePath(UXMLCustomTypeElement));

        foreach (string customType in GetConfiguration().customTypes)
        {
            VisualElement customTypeElement = customTypeUXML.Instantiate();
            
            customTypeElement.Q<Label>(CustomTypeElementName).text = customType;
            customTypeElement.Q<Button>(CustomTypeElementDeleteButton).clicked += () => { OnCustomTypeElementDeleteButton(customType); };
            
            foldout.Add(customTypeElement);
        }
        
        AssetDatabase.Refresh();
    }

    private void OnCategorySelected(string categoryName)
    {
        UpdateVariablesUI();
    }

    private void CreateCategory()
    {
        string newCategoryName = rootVisualElement.Q<TextField>(CreateCategoryNameTextFieldClass).value;
        
        MagicLinksConfiguration config = GetConfiguration();

        if (config.categories.Contains(newCategoryName)) return;
        
        config.categories.Add(newCategoryName);
        EditorUtility.SetDirty(config);
        
        UpdateCategories();
    }

    private void DeleteCategory(string categoryName)
    {
        MagicLinksConfiguration config = GetConfiguration();

        if (config.categories.Contains(categoryName) == false) return;
        
        config.categories.Remove(categoryName);
        EditorUtility.SetDirty(config);
        
        UpdateCategories();
    }

    private void UpdateCategories()
    {
        MagicLinksConfiguration config = GetConfiguration();
        
        //Dropdown general
        DropdownField categories = rootVisualElement.Q<DropdownField>(CategoriesDropdownClass);
        
        categories.choices.Clear();
        
        categories.choices.Add(MagicLinksUtilities.CategoryNone);

        foreach (string category in config.categories)
        {
            categories.choices.Add(category);
        }

        if (categories.choices.Count == 1 || categories.choices.IndexOf(categories.value) == -1) categories.index = 0;
        
        //Foldout
        Foldout foldout = rootVisualElement.Q<Foldout>(CreateCategoryFoldoutClass);
        
        foldout.Clear();
        
        VisualTreeAsset customTypeUXML = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(GetPackageRelativePath(UXMLCustomTypeElement));

        foreach (string category in config.categories)
        {
            VisualElement customTypeElement = customTypeUXML.Instantiate();
            
            customTypeElement.Q<Label>(CustomTypeElementName).text = category;
            customTypeElement.Q<Button>(CustomTypeElementDeleteButton).clicked += () => { DeleteCategory(category); };
            
            foldout.Add(customTypeElement);
        }
        
        UpdateVariablesUI();
        AssetDatabase.Refresh();
    }

    private void OnCustomTypeElementDeleteButton(string customType)
    {
        MagicLinksConfiguration config = GetConfiguration();

        config.customTypes.Remove(customType);
        EditorUtility.SetDirty(config);
        UpdateTypes();
        GenerateMagicVariablesScript(false);
    }

    private Dictionary<string, string> GetBaseTypes()
    {
        Dictionary<string, string> baseTypes = new Dictionary<string, string>();
        
        baseTypes.Add(MagicLinksUtilities.String, typeof(string).ToString());
        baseTypes.Add(MagicLinksUtilities.Bool, typeof(bool).ToString());
        baseTypes.Add(MagicLinksUtilities.Int, typeof(int).ToString());
        baseTypes.Add(MagicLinksUtilities.Float, typeof(float).ToString());
        baseTypes.Add(MagicLinksUtilities.Vector2, typeof(Vector2).ToString());
        baseTypes.Add(MagicLinksUtilities.Vector3, typeof(Vector3).ToString());
        baseTypes.Add(MagicLinksUtilities.GameObject, typeof(GameObject).ToString());
        baseTypes.Add(MagicLinksUtilities.Transform, typeof(Transform).ToString());
        baseTypes.Add(MagicLinksUtilities.Collider, typeof(Collider).ToString());
        baseTypes.Add(MagicLinksUtilities.Color, typeof(Color).ToString());

        return baseTypes;
    }


    //CREATE VARIABLE
    //---------------------------------
    //---------------------------------

    private void CreateVariable()
    {
        string variableName = rootVisualElement.Q<TextField>(VariableNameTextFieldClass).value;
        
        CreateVariablesFolder();
        
        //Create the variable in resources
        string newVariablePath = Path.Combine(MagicLinksUtilities.VariablesPath, variableName + ".json");
        if (File.Exists(newVariablePath)) return;
        
        DynamicVariable newVariable = new DynamicVariable();
        newVariable.vName = variableName;
        
        Dictionary<string, string> baseTypes = GetBaseTypes();

        newVariable.vTruelType = GetTrueType(baseTypes.FirstOrDefault().Key);
        newVariable.vLabelType = baseTypes.FirstOrDefault().Key;
        
        newVariable.vPath = newVariablePath;
        newVariable.category = MagicLinksUtilities.CategoryNone;
        
        File.WriteAllText(newVariablePath, JsonUtility.ToJson(newVariable, true));
        AssetDatabase.Refresh();

        UpdateVariablesUI();
    }

    private List<DynamicVariable> GetExistingVariables()
    {
        CreateVariablesFolder();
        List<DynamicVariable> existingVariables = new List<DynamicVariable>();
        
        string[] files = Directory.GetFiles(MagicLinksUtilities.VariablesPath);

        foreach(string file in files)
        {
            if (Path.GetExtension(file) != ".json") continue;

            try
            {
                DynamicVariable v = JsonUtility.FromJson<DynamicVariable>(File.ReadAllText(file));
                existingVariables.Add(v);
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
        }
        
        return existingVariables;
    }

    private void UpdateVariablesUI()
    {
        //Load all variables from resources
        List<DynamicVariable> existingVariables = GetExistingVariables();
        
        VisualTreeAsset variableUXML = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(GetPackageRelativePath(UXMLVariablePath));
        VisualElement variablesContainer = rootVisualElement.Q<VisualElement>(VariablesContainerVisualElementClass);
        variablesContainer.Clear();

        string currentCategorySelected = rootVisualElement.Q<DropdownField>(CategoriesDropdownClass).value;

        foreach (DynamicVariable v in existingVariables)
        {
            //Filter
            if (currentCategorySelected != MagicLinksUtilities.CategoryNone)
            {
                if(v.category != currentCategorySelected) continue;
            }
            
            VisualElement newUIVariable = variableUXML.Instantiate();
            
            //AddInitialSelectorToVariableUI(v, newUIVariable);
            
            DropdownField field = newUIVariable.Q<DropdownField>(SingleVariableType);

            if (v.IsVoid())
            {
                field.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            }
            else
            {
                foreach (string t in GetAllTypes())
                {
                    field.choices.Add(t);
                }

                field.index = field.choices.IndexOf(v.vLabelType);
                field.RegisterValueChangedCallback((newType) => { OnSingleVariableTypeChanged(v, newType.newValue); });
            }
            
            DropdownField magicType = newUIVariable.Q<DropdownField>(SingleVariableMagicType);
            
            magicType.choices.Clear();
            magicType.choices.Add(MagicLinksUtilities.MagicTypeVariable);
            magicType.choices.Add(MagicLinksUtilities.MagicTypeEvent);
            magicType.choices.Add(MagicLinksUtilities.MagicTypeEventVoid);

            magicType.index = v.magicType;
            magicType.RegisterValueChangedCallback((newMagicType) => { OnMagicTypeChanged(v, magicType.choices.IndexOf(newMagicType.newValue)); });
            
            newUIVariable.Q<VisualElement>(SingleVariableIcon).style.backgroundImage = GetVariableIcon(v);
            newUIVariable.Q<Label>(SingleVariableName).text = v.vName;
            newUIVariable.Q<Button>(SingleVariableDeleteButton).clicked += () => OnDeleteSingleVariable(v.vPath);
            
            //Category
            DropdownField category = newUIVariable.Q<DropdownField>(SingleVariableCategory);
            
            category.choices.Clear();
            category.choices.Add(MagicLinksUtilities.CategoryNone);

            MagicLinksConfiguration config = GetConfiguration();

            foreach (string categoryName in config.categories)
            {
                category.choices.Add(categoryName);
            }

            bool goBackToNone = false;
            if (category.choices.IndexOf(v.category) == -1)
            {
                OnSingleVariableCategoryChanged(v, MagicLinksUtilities.CategoryNone);
                goBackToNone = true;
            }
            
            category.SetValueWithoutNotify(goBackToNone ? MagicLinksUtilities.CategoryNone : v.category);
            category.RegisterValueChangedCallback((c) => { OnSingleVariableCategoryChanged(v, c.newValue); });
            
            variablesContainer.Add(newUIVariable);
        }
    }

    private void OnSingleVariableCategoryChanged(DynamicVariable variable, string newCategory)
    {
        DynamicVariable variableToUpdate = JsonUtility.FromJson<DynamicVariable>(File.ReadAllText(variable.vPath));
        variableToUpdate.category = newCategory;
        File.WriteAllText(variable.vPath, JsonUtility.ToJson(variableToUpdate, true));
        
        AssetDatabase.Refresh();
        //UpdateVariablesUI();
    }

    private void OnMagicTypeChanged(DynamicVariable variable, int newMagicType)
    {
        DynamicVariable variableToUpdate = JsonUtility.FromJson<DynamicVariable>(File.ReadAllText(variable.vPath));
        variableToUpdate.magicType = newMagicType;
        File.WriteAllText(variable.vPath, JsonUtility.ToJson(variableToUpdate, true));
        
        AssetDatabase.Refresh();
        UpdateVariablesUI();
    }

    private void AddInitialSelectorToVariableUI(DynamicVariable variable, VisualElement variableUI)
    {
        if (variable.vLabelType == MagicLinksUtilities.Color)
        {
            ColorField colorField = new ColorField();
            colorField.RegisterValueChangedCallback((v) => { UpdateDynamicVariableInitialValue(variable, v.newValue.ToString()); });
            AddClassesToVariableNewElements(colorField);
            variableUI.ElementAt(0).Add(colorField);
        }
    }

    private void AddClassesToVariableNewElements(VisualElement e)
    {
        e.AddToClassList("singleVariableElement");
        e.AddToClassList("minSize");
    }

    private void UpdateDynamicVariableInitialValue(DynamicVariable variable, string initialValue)
    {
        DynamicVariable variableToUpdate = JsonUtility.FromJson<DynamicVariable>(File.ReadAllText(variable.vPath));
        variableToUpdate.initialValue = initialValue;
        File.WriteAllText(variable.vPath, JsonUtility.ToJson(variableToUpdate, true));
        
        //AssetDatabase.Refresh();
    }

    private void CreateVariablesFolder()
    {
        if(Directory.Exists(MagicLinksUtilities.VariablesPath) == false)
            Directory.CreateDirectory(MagicLinksUtilities.VariablesPath);
    }

    private void OnSingleVariableTypeChanged(DynamicVariable variable, string newType)
    {
        DynamicVariable variableToUpdate = JsonUtility.FromJson<DynamicVariable>(File.ReadAllText(variable.vPath));
        
        variableToUpdate.vTruelType = GetTrueType(newType);
        variableToUpdate.vLabelType = newType;
        
        File.WriteAllText(variable.vPath, JsonUtility.ToJson(variableToUpdate, true));
        AssetDatabase.Refresh();
        
        UpdateVariablesUI();
    }

    private void OnDeleteSingleVariable(string path)
    {
        File.Delete(path);
        AssetDatabase.Refresh();
        UpdateVariablesUI();
    }

    private Texture2D GetVariableIcon(DynamicVariable variable)
    {
        string spritesPath = GetPackageRelativePath(SpritesPath);
        
        string[] filesPath = Directory.GetFiles(spritesPath);

        foreach (string p in filesPath)
        {
            if(Path.GetExtension(p) != ".png") continue;

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
                if(Path.GetFileNameWithoutExtension(p) == "EventIcon_Void")
                    return AssetDatabase.LoadAssetAtPath(p, typeof(Texture2D)) as Texture2D;
            }
            
            if(Path.GetFileNameWithoutExtension(p) == baseName + variable.vLabelType)
                return AssetDatabase.LoadAssetAtPath(p, typeof(Texture2D)) as Texture2D;
        }

        string customIconPath = Path.Combine(spritesPath, "VariableIcon_Custom.png");
        return AssetDatabase.LoadAssetAtPath(customIconPath, typeof(Texture2D)) as Texture2D;
    }
    
    //---------------------------------

    private void GenerateMagicVariablesScript(bool ifMissing)
    {
        string classContent = GetMagicVariablesScriptContent();
        
        CreateVariablesFolder();

        string newClassPath = Path.Combine(MagicLinksUtilities.ConfigurationPath, MagicVariableClassName + ".cs");

        if (ifMissing && File.Exists(newClassPath)) return;
        
        File.WriteAllText(newClassPath, classContent);
        
        AssetDatabase.Refresh();
    }

    private string GetMagicVariablesScriptContent()
    {
        string classContent = File.ReadAllText(GetPackageRelativePath(MagicVariablesTemplate));

        classContent = classContent.Replace("MagicVariablesTemplate", MagicVariableClassName);
        
        string variables = string.Empty;

        foreach (string customType in GetAllTypes())
        {
            variables += GetDict(VariableDictTemplate, customType, customType.ToUpper());
        }

        variables += "\n \n";
        
        foreach (string customType in GetAllTypes())
        {
            variables += GetDict(EventDictTemplate, customType, customType.ToUpper() + MagicLinksUtilities.EventDict);
        }

        variables += EventVoidDictTemplate;

        classContent = classContent.Replace("//VARIABLESLISTS", variables);
        classContent = classContent.Replace("/*", string.Empty);
        classContent = classContent.Replace("*/", string.Empty);
        classContent = classContent.Replace("#if UNITY_EDITOR", string.Empty);
        classContent = classContent.Replace("#endif", string.Empty);
        
        //Generate Getters
        string variablesGetter = string.Empty;
        foreach (string customType in GetAllTypes())
        {
            variablesGetter += GetDict(VariableGetterTemplate, customType, customType.ToUpper());
        }

        classContent = classContent.Replace("//MAGICVARIABLESGETTER", variablesGetter);

        string eventsGetter = string.Empty;
        foreach (string customType in GetAllTypes())
        {
            string eventGetter = GetDict(EventGetterTemplate, customType, customType.ToUpper() + MagicLinksUtilities.EventDict);

            eventGetter = eventGetter.Replace("SHORT", customType.ToUpper());
            
            var regex = new Regex(Regex.Escape(MagicLinksUtilities.EventDict));
            eventsGetter = regex.Replace(eventsGetter, MagicLinksUtilities.EventDict, 1);
            
            eventsGetter += eventGetter;
        }

        eventsGetter += EventVoidGetterTemplate;

        classContent = classContent.Replace("//MAGICEVENTSGETTER", eventsGetter);
        
        return classContent;
    }

    private string GetDict(string dict, string t, string n)
    {
        string newDict = dict;
        newDict = newDict.Replace("TYPE", t);
        newDict = newDict.Replace("NAME", n);
        newDict += "\n";
        return newDict;
    }
    
    //---------------------------------

    public List<string> GetAllTypes()
    {
        List<string> types = new List<string>();
        
        foreach (KeyValuePair<string, string> baseType in GetBaseTypes())
        {
            types.Add(baseType.Key);
        }

        foreach (string customType in GetConfiguration().customTypes)
        {
            types.Add(customType);
        }

        return types;
    }

    private string GetTrueType(string t)
    {
        Dictionary<string, string> baseTypes = GetBaseTypes();

        if (baseTypes.ContainsKey(t)) return baseTypes[t];
        else return t;
    }
    
    private MagicLinksConfiguration GetConfiguration()
    {
        if(Directory.Exists(MagicLinksUtilities.ConfigurationPath) == false)
            Directory.CreateDirectory(MagicLinksUtilities.ConfigurationPath);
        
        AssetDatabase.Refresh();

        string fullPath = Path.Combine(MagicLinksUtilities.ConfigurationPath, MagicLinksUtilities.ConfigurationName);
        
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

    private string GetPackageRelativePath(string subpath)
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

    private string GetPackagePath()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);

        if (info == null) return string.Empty;
        
        return info.assetPath;
    }
}

#endif

[System.Serializable]
public class DynamicVariable
{
    public string vName;
    public string vLabelType;
    public string vTruelType;
    public string vPath;
    public string initialValue;
    public int magicType;
    public string category;

    public bool IsEvent()
    {
        return magicType != 0;
    }

    public bool IsVoid()
    {
        return magicType == 2;
    }
    
    public string GetDictName()
    {
        if (magicType != 0) return vName + MagicLinksUtilities.EventDict;

        return vName;
    }
}