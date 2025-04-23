#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
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
    
    const string VariableGetterTemplate = "public static Dictionary<string, MagicVariableObservable<TYPE>> NAME = MagicLinksManager.Instance.NAME;";
    const string EventGetterTemplate = "public static Dictionary<string, MagicEventObservable<TYPE>> NAME = MagicLinksManager.Instance.NAME;";

    const string RefreshScriptsButton = "RefreshScripts";
    
    const string CreateTypeButtonClass = "CreateTypeButton";
    const string CreateVariableButtonClass = "CreateVariableButton";
    const string DropdownVariableTypeClass = "DropdownVariableType";
    const string TypeTextFieldClass = "TypeName";
    const string VariableNameTextFieldClass = "VariableName";
    const string VariablesContainerVisualElementClass = "VariablesContainer";
    
    const string SingleVariableIcon = "Icon";
    const string SingleVariableName = "Name";
    const string SingleVariableType = "Type";
    const string SingleVariableDeleteButton = "DeleteButton";
    const string SingleVariableIsEvent = "IsEvent";
    
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
        GetTypesConfiguration();
        
        m_VisualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(GetPackageRelativePath(UXMLPath));
        
        rootVisualElement.Add(m_VisualTreeAsset.Instantiate());
        
        HookEvents();
        UpdateTypes();
        UpdateVariablesUI();
        
        GenerateMagicVariablesScript(true);
    }

    private void HookEvents()
    {
        rootVisualElement.Q<Button>(CreateTypeButtonClass).clicked += CreateType;
        rootVisualElement.Q<Button>(CreateVariableButtonClass).clicked += CreateVariable;
        rootVisualElement.Q<Button>(RefreshScriptsButton).clicked += RefreshScripts;
    }

    private void RefreshScripts()
    {
        UpdateTypes();
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

        MagicLinksTypesConfiguration config = GetTypesConfiguration();

        Debug.Log(type);
        if (type == string.Empty) return;
        if (config.customTypes.Contains(type) || GetBaseTypes().ContainsValue(type)) return;
            
        config.customTypes.Add(type);
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

        foreach (string customType in GetTypesConfiguration().customTypes)
        {
            VisualElement customTypeElement = customTypeUXML.Instantiate();
            
            customTypeElement.Q<Label>(CustomTypeElementName).text = customType;
            customTypeElement.Q<Button>(CustomTypeElementDeleteButton).clicked += () => { OnCustomTypeElementDeleteButton(customType); };
            
            foldout.Add(customTypeElement);
        }
        
        //-------------------
        DropdownField field = rootVisualElement.Q<DropdownField>(DropdownVariableTypeClass);

        field.choices.Clear();

        foreach (string t in GetAllTypes())
        {
            field.choices.Add(t);
        }
        
        if(field.index == -1) field.index = 0;
        AssetDatabase.Refresh();
    }

    private void OnCustomTypeElementDeleteButton(string customType)
    {
        GetTypesConfiguration().customTypes.Remove(customType);
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
        string variableType = rootVisualElement.Q<DropdownField>(DropdownVariableTypeClass).value;
        
        CreateVariablesFolder();
        
        //Create the variable in resources
        string newVariablePath = Path.Combine(MagicLinksUtilities.VariablesPath, variableName + ".json");
        if (File.Exists(newVariablePath)) return;
        
        DynamicVariable newVariable = new DynamicVariable();
        newVariable.vName = variableName;
        
        Dictionary<string, string> baseTypes = GetBaseTypes();

        newVariable.vTruelType = GetTrueType(variableType);
        newVariable.vLabelType = variableType;
        
        newVariable.vPath = newVariablePath;
        
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

        foreach (DynamicVariable v in existingVariables)
        {
            VisualElement newUIVariable = variableUXML.Instantiate();
            
            //AddInitialSelectorToVariableUI(v, newUIVariable);
            
            DropdownField field = newUIVariable.Q<DropdownField>(SingleVariableType);

            foreach (string t in GetAllTypes())
            {
                field.choices.Add(t);
            }

            field.index = field.choices.IndexOf(v.vLabelType);
            field.RegisterValueChangedCallback((newType) => { OnSingleVariableTypeChanged(v, newType.newValue); });
                
            Toggle isEventToggle = newUIVariable.Q<Toggle>(SingleVariableIsEvent);
                
            isEventToggle.SetValueWithoutNotify(v.isEvent);
            isEventToggle.RegisterValueChangedCallback((newBool) => { OnIsEventChanged(v, newBool.newValue); });
            
            newUIVariable.Q<VisualElement>(SingleVariableIcon).style.backgroundImage = GetVariableIcon(v.vLabelType);
            newUIVariable.Q<Label>(SingleVariableName).text = v.vName;
            newUIVariable.Q<Button>(SingleVariableDeleteButton).clicked += () => OnDeleteSingleVariable(v.vPath);
            
            variablesContainer.Add(newUIVariable);
        }
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
        
        File.WriteAllText(variable.vPath, JsonUtility.ToJson(variableToUpdate, true));
        AssetDatabase.Refresh();
        
        UpdateVariablesUI();
    }

    private void OnIsEventChanged(DynamicVariable variable, bool newValue)
    {
        DynamicVariable variableToUpdate = JsonUtility.FromJson<DynamicVariable>(File.ReadAllText(variable.vPath));
        
        variableToUpdate.isEvent = newValue;
        
        File.WriteAllText(variable.vPath, JsonUtility.ToJson(variableToUpdate, true));
        AssetDatabase.Refresh();
    }

    private void OnDeleteSingleVariable(string path)
    {
        File.Delete(path);
        AssetDatabase.Refresh();
        UpdateVariablesUI();
    }

    private Texture2D GetVariableIcon(string variableType)
    {
        string spritesPath = GetPackageRelativePath(SpritesPath);
        
        string[] filesPath = Directory.GetFiles(spritesPath);

        foreach (string p in filesPath)
        {
            if(Path.GetExtension(p) != ".png") continue;
            
            if(Path.GetFileNameWithoutExtension(p) == "VariableIcon_" + variableType)
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

        string newClassPath = Path.Combine(MagicLinksUtilities.TypesConfigurationPath, MagicVariableClassName + ".cs");

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
            
            var regex = new Regex(Regex.Escape(MagicLinksUtilities.EventDict));
            eventsGetter = regex.Replace(eventsGetter, MagicLinksUtilities.EventDict, 1);
            
            eventsGetter += eventGetter;
        }

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

        foreach (string customType in GetTypesConfiguration().customTypes)
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
    
    private MagicLinksTypesConfiguration GetTypesConfiguration()
    {
        if(Directory.Exists(MagicLinksUtilities.TypesConfigurationPath) == false)
            Directory.CreateDirectory(MagicLinksUtilities.TypesConfigurationPath);
        
        AssetDatabase.Refresh();

        string fullPath = Path.Combine(MagicLinksUtilities.TypesConfigurationPath, MagicLinksUtilities.TypesConfigurationName);
        
        if (File.Exists(fullPath))
        {
            return AssetDatabase.LoadAssetAtPath<MagicLinksTypesConfiguration>(fullPath);
        }
        else
        {
            AssetDatabase.CreateAsset(new MagicLinksTypesConfiguration(), fullPath);
            return AssetDatabase.LoadAssetAtPath<MagicLinksTypesConfiguration>(fullPath);
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
    public bool isEvent;

    public string GetDictName()
    {
        if (isEvent) return vName + MagicLinksUtilities.EventDict;

        return vName;
    }
}