#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
            MagicLinksNetworkScriptsGenerator.GenerateNetworkScript();
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

                VisualElement networkFields = CreateNetworkFieldsUI(customType, config);
                if (networkFields != null) foldout.Add(networkFields);
            }
        }

        // Keeps the "Network fields" foldouts open when the list is rebuilt.
        private static readonly HashSet<string> ExpandedNetworkFields = new HashSet<string>();

        private static readonly List<string> FieldModeChoices = new List<string>
        {
            "Auto", nameof(MagicNetFieldMode.Value), nameof(MagicNetFieldMode.Excluded), nameof(MagicNetFieldMode.Reference)
        };

        /// <summary>Per-field network modes of a custom type. Null for types without public fields.</summary>
        public static VisualElement CreateNetworkFieldsUI(string customType, MagicLinksConfiguration config)
        {
            Type type = MagicLinksNetworkAnalyzer.FindType(customType);
            if (type == null) return null;

            List<MagicNetTypeDescription> descriptions = new MagicLinksNetworkAnalyzer(config).DescribeForEditor(type);
            if (descriptions.Count == 0 || descriptions[0].fields.Count == 0) return null;

            Foldout networkFields = new Foldout { text = "Network fields" };
            networkFields.style.marginLeft = 12;
            networkFields.style.marginRight = 4;
            networkFields.style.marginBottom = 4;
            networkFields.style.fontSize = 11;

            List<string> errors = new List<string>();
            MagicLinksNetworkAnalyzer.CollectErrors(descriptions[0], errors);
            if (errors.Count > 0)
            {
                networkFields.text = $"Network fields ({errors.Count} to fix if networked)";
                networkFields.tooltip = string.Join("\n", errors);
            }

            networkFields.SetValueWithoutNotify(ExpandedNetworkFields.Contains(customType));
            networkFields.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != networkFields) return;
                if (evt.newValue) ExpandedNetworkFields.Add(customType);
                else ExpandedNetworkFields.Remove(customType);
            });

            foreach (MagicNetTypeDescription description in descriptions)
            {
                if (descriptions.Count > 1)
                {
                    Label typeHeader = new Label(description.type.Name);
                    typeHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                    typeHeader.style.marginTop = 4;
                    networkFields.Add(typeHeader);
                }

                foreach (string error in description.errors)
                    networkFields.Add(CreateErrorLabel(error));

                foreach (MagicNetFieldDescription field in description.fields)
                    networkFields.Add(CreateFieldRow(description.type, field));
            }

            return networkFields;
        }

        private static VisualElement CreateFieldRow(Type ownerType, MagicNetFieldDescription field)
        {
            VisualElement row = new VisualElement();

            VisualElement line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.alignItems = Align.Center;

            Label name = new Label($"{field.field.Name} ({MagicLinksNetworkAnalyzer.GetFriendlyName(field.field.FieldType)})");
            name.style.flexGrow = 1;
            name.style.flexShrink = 1;
            name.style.minWidth = 0;
            name.style.overflow = Overflow.Hidden;
            name.style.textOverflow = TextOverflow.Ellipsis;
            line.Add(name);

            DropdownField mode = new DropdownField(FieldModeChoices, (int)field.storedMode);
            mode.style.width = 80;
            mode.style.flexShrink = 0;
            if (field.storedMode == MagicNetFieldMode.Unset)
                mode.tooltip = $"Auto: {(field.effectiveMode == MagicNetFieldMode.Unset ? "a choice is required" : field.effectiveMode.ToString())}";
            mode.RegisterValueChangedCallback(evt =>
            {
                OnFieldModeChanged(ownerType, field.field.Name, (MagicNetFieldMode)FieldModeChoices.IndexOf(evt.newValue));
            });
            line.Add(mode);

            row.Add(line);

            if (field.error != null) row.Add(CreateErrorLabel(field.error));

            return row;
        }

        private static Label CreateErrorLabel(string error)
        {
            Label label = new Label(error);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = new Color(1f, 0.55f, 0.55f);
            label.style.marginLeft = 8;
            return label;
        }

        public static void OnFieldModeChanged(Type ownerType, string fieldName, MagicNetFieldMode mode)
        {
            MagicLinksConfiguration config = MagicLinksUtilities.GetConfiguration();

            config.SetFieldMode(ownerType.FullName, fieldName, mode);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);

            UpdateTypes();
            MagicLinksInternalVar.UpdateVariablesUI();
            MagicLinksNetworkScriptsGenerator.GenerateNetworkScript();
        }

        public static void OnCustomTypeElementDeleteButton(string customType)
        {
            MagicLinksConfiguration config = MagicLinksUtilities.GetConfiguration();

            config.customTypes.Remove(customType);
            EditorUtility.SetDirty(config);
            UpdateTypes();
            MagicLinksScriptsGenerator.GenerateMagicVariablesScript(false);
            MagicLinksNetworkScriptsGenerator.GenerateNetworkScript();
            MagicLinksScriptsGenerator.ClearListeners(false);
        }
    }
}
#endif