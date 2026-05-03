#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace MagicLinks
{
    public static class MagicLinksCustomTypes
    {
        public static void CreateType()
        {
            TextField typeField = MagicLinkEditor.Instance.rootVisualElement
                .Q<TextField>(MagicLinksConst.TypeTextFieldClass);
            string type = typeField.value;

            if (string.IsNullOrWhiteSpace(type)) return;

            if (MagicLinksConst.DoesTypeExist(type) == false)
            {
                Debug.LogError("Type does not exist");
                return;
            }

            MagicLinksConfiguration config = MagicLinksUtilities.GetConfiguration();

            if (config.customTypes.Contains(type) || MagicLinksUtilities.GetBaseTypes().ContainsValue(type)) return;

            config.customTypes.Add(type);
            EditorUtility.SetDirty(config);
            typeField.SetValueWithoutNotify(string.Empty);
            UpdateTypes();
            MagicLinksInternalVar.UpdateVariablesUI();
            MagicLinksScriptsGenerator.GenerateMagicVariablesScript(false);
        }

        public static void UpdateTypes()
        {
            //Updates custom types foldout

            Foldout foldout = MagicLinkEditor.Instance.rootVisualElement.Q<Foldout>(MagicLinksConst.CustomTypesFoldout);

            foldout.Clear();

            VisualTreeAsset customTypeUXML =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.UXMLCustomTypeElement));

            MagicLinksConfiguration config = MagicLinksUtilities.GetConfiguration();
            foreach (string customType in config.customTypes)
            {
                VisualElement customTypeElement = customTypeUXML.Instantiate();

                customTypeElement.Q<Label>(MagicLinksConst.CustomTypeElementName).text = customType;
                customTypeElement.Q<Button>(MagicLinksConst.CustomTypeElementDeleteButton).clicked += () =>
                {
                    OnCustomTypeElementDeleteButton(customType);
                };

                foldout.Add(customTypeElement);
            }
        }

        public static void OnCustomTypeElementDeleteButton(string customType)
        {
            MagicLinksConfiguration config = MagicLinksUtilities.GetConfiguration();

            config.customTypes.Remove(customType);
            EditorUtility.SetDirty(config);
            UpdateTypes();
            MagicLinksScriptsGenerator.GenerateMagicVariablesScript(false);
            MagicLinksScriptsGenerator.ClearListeners(false);
        }
    }
}
#endif