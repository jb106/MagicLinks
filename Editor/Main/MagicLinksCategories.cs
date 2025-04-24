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
            string newCategoryName = MagicLinkEditor.Instance.rootVisualElement
                .Q<TextField>(MagicLinksConst.CreateCategoryNameTextFieldClass).value;

            MagicLinksConfiguration config = MagicLinksUtilities.GetConfiguration();

            if (config.categories.Contains(newCategoryName)) return;

            config.categories.Add(newCategoryName);
            EditorUtility.SetDirty(config);

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

            if (categories.choices.Count == 1 || categories.choices.IndexOf(categories.value) == -1)
                categories.index = 0;

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

            MagicLinksInternalVar.UpdateVariablesUI();
            AssetDatabase.Refresh();
        }
    }
}
#endif