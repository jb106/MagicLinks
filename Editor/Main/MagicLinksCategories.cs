#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace MagicLinks
{
    public static class MagicLinksCategories
    {
        public static void OnCategorySelected(string categoryName)
        {
            MagicLinksInternalVar.UpdateVariablesUI();
        }

        public static void CreateCategory()
        {
            TextField nameField = MagicLinkEditor.Instance.rootVisualElement
                .Q<TextField>(MagicLinksConst.CreateCategoryNameTextFieldClass);
            string newCategoryName = nameField.value;

            if (string.IsNullOrWhiteSpace(newCategoryName)) return;

            MagicLinksConfiguration config = MagicLinksUtilities.GetConfiguration();

            if (config.categories.Contains(newCategoryName)) return;

            config.categories.Add(newCategoryName);
            EditorUtility.SetDirty(config);
            nameField.SetValueWithoutNotify(string.Empty);

            UpdateCategories();
        }

        public static void DeleteCategory(string categoryName)
        {
            MagicLinksConfiguration config = MagicLinksUtilities.GetConfiguration();

            if (config.categories.Contains(categoryName) == false) return;

            config.categories.Remove(categoryName);
            EditorUtility.SetDirty(config);

            UpdateCategories();
        }

        public static void UpdateCategories()
        {
            MagicLinksConfiguration config = MagicLinksUtilities.GetConfiguration();

            //Dropdown general
            DropdownField categories =
                MagicLinkEditor.Instance.rootVisualElement.Q<DropdownField>(MagicLinksConst.CategoriesDropdownClass);

            categories.choices.Clear();

            categories.choices.Add(MagicLinksConst.CategoryNone);

            foreach (string category in config.categories)
            {
                categories.choices.Add(category);
            }

            // Restore the previously selected category (survives recompile + re-open) if it still exists.
            string stored = EditorPrefs.GetString(MagicLinksConst.VariablesCategoryFilterKey, MagicLinksConst.CategoryNone);
            int storedIndex = categories.choices.IndexOf(stored);
            categories.index = storedIndex >= 0 ? storedIndex : 0;

            //Foldout
            Foldout foldout =
                MagicLinkEditor.Instance.rootVisualElement.Q<Foldout>(MagicLinksConst.CreateCategoryFoldoutClass);

            foldout.Clear();

            VisualTreeAsset customTypeUXML =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.UXMLCustomTypeElement));

            foreach (string category in config.categories)
            {
                VisualElement customTypeElement = customTypeUXML.Instantiate();

                customTypeElement.Q<Label>(MagicLinksConst.CustomTypeElementName).text = category;
                customTypeElement.Q<Button>(MagicLinksConst.CustomTypeElementDeleteButton).clicked +=
                    () => { DeleteCategory(category); };

                foldout.Add(customTypeElement);
            }

            MagicLinksAdvancedSettings.Build();
            MagicLinksInternalVar.UpdateVariablesUI();
        }
    }
}
#endif