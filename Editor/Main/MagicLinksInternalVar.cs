#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MagicLinks
{
    public static class MagicLinksInternalVar
    {
        public static void CreateVariable()
        {
            string variableName = MagicLinkEditor.Instance.rootVisualElement
                .Q<TextField>(MagicLinksConst.VariableNameTextFieldClass).value;

            MagicLinksUtilities.CreateVariablesFolder();

            //Create the variable in resources
            string newVariablePath = Path.Combine(MagicLinksConst.VariablesPath, variableName + ".json");
            if (File.Exists(newVariablePath)) return;

            DynamicVariable newVariable = new DynamicVariable();
            newVariable.vName = variableName;

            Dictionary<string, string> baseTypes = MagicLinksUtilities.GetBaseTypes();

            newVariable.vTruelType = MagicLinksUtilities.GetTrueType(baseTypes.FirstOrDefault().Key);
            newVariable.vLabelType = baseTypes.FirstOrDefault().Key;

            newVariable.vPath = newVariablePath;
            newVariable.category = MagicLinksConst.CategoryNone;

            File.WriteAllText(newVariablePath, JsonUtility.ToJson(newVariable, true));
            AssetDatabase.Refresh();

            UpdateVariablesUI();
        }

        public static List<DynamicVariable> GetExistingVariables()
        {
            MagicLinksUtilities.CreateVariablesFolder();
            List<DynamicVariable> existingVariables = new List<DynamicVariable>();

            string[] files = Directory.GetFiles(MagicLinksConst.VariablesPath);

            foreach (string file in files)
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

        public static void UpdateVariablesUI()
        {
            //Load all variables from resources
            List<DynamicVariable> existingVariables = GetExistingVariables();

            VisualTreeAsset variableUXML =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.UXMLVariablePath));

            VisualTreeAsset variableHeader =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.UXMLVariableHeaderPath));
            
            VisualElement variablesContainer =
                MagicLinkEditor.Instance.rootVisualElement.Q<VisualElement>(MagicLinksConst
                    .VariablesContainerVisualElementClass);
            
            variablesContainer.Clear();

            string currentCategorySelected = MagicLinkEditor.Instance.rootVisualElement
                .Q<DropdownField>(MagicLinksConst.CategoriesDropdownClass).value;
            
            MagicLinksConfiguration config = MagicLinksUtilities.GetConfiguration();
            config.typesNamesPairs.Clear();
            
            //Sort variable by category
            Dictionary<string, int> categoryPriority = config.categories
                .Select((category, index) => new { category, index })
                .ToDictionary(x => x.category, x => x.index);
            
            List<DynamicVariable> sortedVariables = existingVariables
                .OrderBy(v => categoryPriority.ContainsKey(v.category) ? categoryPriority[v.category] : int.MinValue)
                .ToList();

            string lastCategory = string.Empty;

            if (currentCategorySelected == MagicLinksConst.CategoryNone)
            {
                VisualElement newHeader = variableHeader.Instantiate();
                newHeader.Q<Label>("HeaderText").text = MagicLinksConst.CategoryNone;
                
                variablesContainer.Add(newHeader);
            }
            
            foreach (DynamicVariable v in sortedVariables)
            {
                if (v.category != lastCategory)
                {
                    if (v.category != MagicLinksConst.CategoryNone && currentCategorySelected == MagicLinksConst.CategoryNone)
                    {
                        VisualElement newHeader = variableHeader.Instantiate();
                        newHeader.Q<Label>("HeaderText").text = v.category;

                        variablesContainer.Add(newHeader);
                    }

                    lastCategory = v.category;
                }
                
                config.typesNamesPairs.Add(new MagicLinkTypeNamePair(v.IsVoid() ? string.Empty : v.vLabelType, v.vName));
                
                //Filter
                if (currentCategorySelected != MagicLinksConst.CategoryNone)
                {
                    if (v.category != currentCategorySelected) continue;
                }
                
                EditorUtility.SetDirty(config);

                VisualElement newUIVariable = variableUXML.Instantiate();

                //AddInitialSelectorToVariableUI(v, newUIVariable);

                DropdownField field = newUIVariable.Q<DropdownField>(MagicLinksConst.SingleVariableType);

                if (v.IsVoid())
                {
                    field.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                }
                else
                {
                    foreach (string t in MagicLinksUtilities.GetAllTypes())
                    {
                        field.choices.Add(t);
                    }

                    field.index = field.choices.IndexOf(v.vLabelType);
                    field.RegisterValueChangedCallback((newType) =>
                    {
                        OnSingleVariableTypeChanged(v, newType.newValue);
                    });
                }

                DropdownField magicType = newUIVariable.Q<DropdownField>(MagicLinksConst.SingleVariableMagicType);

                magicType.choices.Clear();
                magicType.choices.Add(MagicLinksConst.MagicTypeVariable);
                magicType.choices.Add(MagicLinksConst.MagicTypeEvent);
                magicType.choices.Add(MagicLinksConst.MagicTypeEventVoid);

                magicType.index = v.magicType;
                magicType.RegisterValueChangedCallback((newMagicType) =>
                {
                    OnMagicTypeChanged(v, magicType.choices.IndexOf(newMagicType.newValue));
                });

                newUIVariable.Q<VisualElement>(MagicLinksConst.SingleVariableIcon).style.backgroundImage =
                    MagicLinksUtilities.GetVariableIcon(v);
                newUIVariable.Q<Label>(MagicLinksConst.SingleVariableName).text = v.vName;
                newUIVariable.Q<Button>(MagicLinksConst.SingleVariableDeleteButton).clicked +=
                    () => OnDeleteSingleVariable(v.vPath);

                //Category
                DropdownField category = newUIVariable.Q<DropdownField>(MagicLinksConst.SingleVariableCategory);

                category.choices.Clear();
                category.choices.Add(MagicLinksConst.CategoryNone);

                foreach (string categoryName in config.categories)
                {
                    category.choices.Add(categoryName);
                }

                bool goBackToNone = false;
                if (category.choices.IndexOf(v.category) == -1)
                {
                    OnSingleVariableCategoryChanged(v, MagicLinksConst.CategoryNone);
                    goBackToNone = true;
                }

                category.SetValueWithoutNotify(goBackToNone ? MagicLinksConst.CategoryNone : v.category);
                category.RegisterValueChangedCallback((c) => { OnSingleVariableCategoryChanged(v, c.newValue); });

                variablesContainer.Add(newUIVariable);
            }
        }

        public static void OnSingleVariableCategoryChanged(DynamicVariable variable, string newCategory)
        {
            DynamicVariable variableToUpdate = JsonUtility.FromJson<DynamicVariable>(File.ReadAllText(variable.vPath));
            variableToUpdate.category = newCategory;
            File.WriteAllText(variable.vPath, JsonUtility.ToJson(variableToUpdate, true));

            AssetDatabase.Refresh();
            UpdateVariablesUI();
        }

        public static void OnMagicTypeChanged(DynamicVariable variable, int newMagicType)
        {
            DynamicVariable variableToUpdate = JsonUtility.FromJson<DynamicVariable>(File.ReadAllText(variable.vPath));
            variableToUpdate.magicType = newMagicType;
            File.WriteAllText(variable.vPath, JsonUtility.ToJson(variableToUpdate, true));

            AssetDatabase.Refresh();
            UpdateVariablesUI();
        }

        public static void AddInitialSelectorToVariableUI(DynamicVariable variable, VisualElement variableUI)
        {
            if (variable.vLabelType == MagicLinksConst.Color)
            {
                ColorField colorField = new ColorField();
                colorField.RegisterValueChangedCallback((v) =>
                {
                    UpdateDynamicVariableInitialValue(variable, v.newValue.ToString());
                });
                AddClassesToVariableNewElements(colorField);
                variableUI.ElementAt(0).Add(colorField);
            }
        }

        public static void AddClassesToVariableNewElements(VisualElement e)
        {
            e.AddToClassList("singleVariableElement");
            e.AddToClassList("minSize");
        }

        public static void UpdateDynamicVariableInitialValue(DynamicVariable variable, string initialValue)
        {
            DynamicVariable variableToUpdate = JsonUtility.FromJson<DynamicVariable>(File.ReadAllText(variable.vPath));
            variableToUpdate.initialValue = initialValue;
            File.WriteAllText(variable.vPath, JsonUtility.ToJson(variableToUpdate, true));

            //AssetDatabase.Refresh();
        }

        public static void OnSingleVariableTypeChanged(DynamicVariable variable, string newType)
        {
            DynamicVariable variableToUpdate = JsonUtility.FromJson<DynamicVariable>(File.ReadAllText(variable.vPath));

            variableToUpdate.vTruelType = MagicLinksUtilities.GetTrueType(newType);
            variableToUpdate.vLabelType = newType;

            File.WriteAllText(variable.vPath, JsonUtility.ToJson(variableToUpdate, true));
            AssetDatabase.Refresh();

            UpdateVariablesUI();
        }

        public static void OnDeleteSingleVariable(string path)
        {
            File.Delete(path);
            AssetDatabase.Refresh();
            UpdateVariablesUI();
        }
    }
}
#endif