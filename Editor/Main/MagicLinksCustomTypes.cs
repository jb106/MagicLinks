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
            string type = MagicLinkEditor.Instance.rootVisualElement.Q<TextField>(MagicLinksConst.TypeTextFieldClass)
                .value;

            if (MagicLinksConst.DoesTypeExist(type) == false)
            {
                Debug.LogError("Type does not exist");
                return;
            }

            MagicLinksConfiguration config = MagicLinksUtilities.GetConfiguration();

            Debug.Log(type);
            if (type == string.Empty) return;
            if (config.customTypes.Contains(type) || MagicLinksUtilities.GetBaseTypes().ContainsValue(type)) return;

            config.customTypes.Add(type);
            EditorUtility.SetDirty(config);
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

            foreach (string customType in MagicLinksUtilities.GetConfiguration().customTypes)
            {
                VisualElement customTypeElement = customTypeUXML.Instantiate();

                customTypeElement.Q<Label>(MagicLinksConst.CustomTypeElementName).text = customType;
                customTypeElement.Q<Button>(MagicLinksConst.CustomTypeElementDeleteButton).clicked += () =>
                {
                    OnCustomTypeElementDeleteButton(customType);
                };

                foldout.Add(customTypeElement);
            }

            AssetDatabase.Refresh();
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