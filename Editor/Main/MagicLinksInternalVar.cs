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
            TextField nameField = MagicLinkEditor.Instance.rootVisualElement
                .Q<TextField>(MagicLinksConst.VariableNameTextFieldClass);
            string variableName = nameField.value;

            if (string.IsNullOrWhiteSpace(variableName)) return;

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
            newVariable.isList = false;

            File.WriteAllText(newVariablePath, JsonUtility.ToJson(newVariable, true));
            AssetDatabase.ImportAsset(newVariablePath);

            nameField.SetValueWithoutNotify(string.Empty);
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

            // Fix orphans (variables whose category was deleted) in a single pass BEFORE rendering.
            // Otherwise we'd trigger a recursive UpdateVariablesUI inside the per-variable loop,
            // which clears the container while we're still iterating it (causes Unity UI corruption).
            FixOrphanCategories(existingVariables);

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

            VisualElement root = MagicLinkEditor.Instance.rootVisualElement;

            // Restore persisted toolbar values on first build, then read current values
            DropdownField categoryField = root.Q<DropdownField>(MagicLinksConst.CategoriesDropdownClass);
            DropdownField sortField = root.Q<DropdownField>(MagicLinksConst.VariablesSortDropdown);
            DropdownField magicTypeField = root.Q<DropdownField>(MagicLinksConst.VariablesMagicTypeFilterDropdown);
            TextField searchField = root.Q<TextField>(MagicLinksConst.VariablesSearchField);

            string currentCategorySelected = categoryField != null
                ? RestoreDropdownValue(categoryField, MagicLinksConst.VariablesCategoryFilterKey, MagicLinksConst.CategoryNone)
                : MagicLinksConst.CategoryNone;
            string sortMode = sortField != null
                ? RestoreDropdownValue(sortField, MagicLinksConst.VariablesSortKey, MagicLinksConst.SortDefault)
                : MagicLinksConst.SortDefault;
            string magicTypeFilter = magicTypeField != null
                ? RestoreDropdownValue(magicTypeField, MagicLinksConst.VariablesMagicTypeFilterKey, MagicLinksConst.MagicTypeFilterAll)
                : MagicLinksConst.MagicTypeFilterAll;

            // Restore search from EditorPrefs if the field is empty (after recompile),
            // otherwise keep what the user just typed.
            string search;
            if (searchField != null)
            {
                if (string.IsNullOrEmpty(searchField.value))
                {
                    string stored = EditorPrefs.GetString(MagicLinksConst.VariablesSearchKey, string.Empty);
                    if (!string.IsNullOrEmpty(stored)) searchField.SetValueWithoutNotify(stored);
                }
                search = (searchField.value ?? string.Empty).Trim();
            }
            else search = string.Empty;

            MagicLinksConfiguration config = MagicLinksUtilities.GetConfiguration();
            config.typesNamesPairs.Clear();

            // Apply name search + magic type filter
            IEnumerable<DynamicVariable> filtered = existingVariables;
            if (!string.IsNullOrEmpty(search))
            {
                filtered = filtered.Where(v => !string.IsNullOrEmpty(v.vName)
                    && v.vName.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0);
            }
            if (magicTypeFilter != MagicLinksConst.MagicTypeFilterAll)
            {
                int wantedMagicType = magicTypeFilter switch
                {
                    MagicLinksConst.MagicTypeEvent => 1,
                    MagicLinksConst.MagicTypeEventVoid => 2,
                    _ => 0,
                };
                filtered = filtered.Where(v => v.magicType == wantedMagicType);
            }

            //Sort
            Dictionary<string, int> categoryPriority = config.categories
                .Select((category, index) => new { category, index })
                .ToDictionary(x => x.category, x => x.index);

            bool groupByCategory = sortMode == MagicLinksConst.SortDefault;
            List<DynamicVariable> sortedVariables = (sortMode switch
            {
                MagicLinksConst.SortNameAsc => filtered.OrderBy(v => v.vName, System.StringComparer.OrdinalIgnoreCase),
                MagicLinksConst.SortNameDesc => filtered.OrderByDescending(v => v.vName, System.StringComparer.OrdinalIgnoreCase),
                MagicLinksConst.SortNewest => filtered.OrderByDescending(v => SafeGetCreationTime(v.vPath)),
                MagicLinksConst.SortOldest => filtered.OrderBy(v => SafeGetCreationTime(v.vPath)),
                _ => filtered.OrderBy(v => categoryPriority.ContainsKey(v.category) ? categoryPriority[v.category] : int.MinValue),
            }).ToList();

            // Cache once instead of recomputing per variable
            List<string> allTypes = MagicLinksUtilities.GetAllTypes();

            bool showCategoryHeaders = groupByCategory && currentCategorySelected == MagicLinksConst.CategoryNone;

            // Pre-compute which variables actually pass every filter so headers
            // only show categories that have at least one visible variable.
            HashSet<string> visibleCategories = null;
            if (showCategoryHeaders)
            {
                visibleCategories = new HashSet<string>();
                foreach (DynamicVariable v in sortedVariables)
                {
                    visibleCategories.Add(string.IsNullOrEmpty(v.category) ? MagicLinksConst.CategoryNone : v.category);
                }
            }

            string lastCategory = null;

            foreach (DynamicVariable v in sortedVariables)
            {
                string categoryKey = string.IsNullOrEmpty(v.category) ? MagicLinksConst.CategoryNone : v.category;
                if (categoryKey != lastCategory)
                {
                    if (showCategoryHeaders && visibleCategories.Contains(categoryKey))
                    {
                        VisualElement newHeader = SafeInstantiate(variableHeader, "VariableHeader");
                        if (newHeader != null)
                        {
                            newHeader.Q<Label>("HeaderText").text = categoryKey;
                            ApplyHeaderColors(newHeader, config, categoryKey);
                            variablesContainer.Add(newHeader);
                        }
                    }

                    lastCategory = categoryKey;
                }
                
                string typeName = v.vLabelType;
                if (v.isList) typeName = $"List<{typeName}>";
                config.typesNamesPairs.Add(new MagicLinkTypeNamePair(v.IsVoid() ? string.Empty : typeName, v.vName));
                
                //Filter
                if (currentCategorySelected != MagicLinksConst.CategoryNone)
                {
                    if (v.category != currentCategorySelected) continue;
                }

                VisualElement newUIVariable = SafeInstantiate(variableUXML, "Variable");
                if (newUIVariable == null) continue;

                ApplyRowColor(newUIVariable, config, string.IsNullOrEmpty(v.category) ? MagicLinksConst.CategoryNone : v.category);
                SetupVariableFoldout(v, newUIVariable);
                AddInitialSelectorToVariableUI(v, newUIVariable);

                DropdownField field = newUIVariable.Q<DropdownField>(MagicLinksConst.SingleVariableType);

                if (v.IsVoid())
                {
                    field.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                }
                else
                {
                    foreach (string t in allTypes)
                    {
                        field.choices.Add(t);
                    }

                    field.index = field.choices.IndexOf(v.vLabelType);
                    field.RegisterValueChangedCallback((newType) =>
                    {
                        OnSingleVariableTypeChanged(v, newType.newValue);
                    });
                }

                Toggle listToggle = newUIVariable.Q<Toggle>(MagicLinksConst.SingleVariableIsList);
                Label isListLabel = newUIVariable.Q<Label>(MagicLinksConst.SingleVariableIsListLabel);

                if (v.IsVoid() == false)
                {
                    listToggle.SetValueWithoutNotify(v.isList);
                    listToggle.RegisterValueChangedCallback(evt => { OnIsListChanged(v, evt.newValue); });
                }
                else
                {
                    listToggle.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                    isListLabel.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
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

                // Orphans were already normalized to CategoryNone before rendering (FixOrphanCategories).
                string effective = string.IsNullOrEmpty(v.category) ? MagicLinksConst.CategoryNone : v.category;
                if (category.choices.IndexOf(effective) == -1) effective = MagicLinksConst.CategoryNone;

                category.SetValueWithoutNotify(effective);
                category.RegisterValueChangedCallback((c) => { OnSingleVariableCategoryChanged(v, c.newValue); });

                variablesContainer.Add(newUIVariable);
            }

            EditorUtility.SetDirty(config);
        }

        public static void OnSingleVariableCategoryChanged(DynamicVariable variable, string newCategory)
        {
            DynamicVariable variableToUpdate = JsonUtility.FromJson<DynamicVariable>(File.ReadAllText(variable.vPath));
            variableToUpdate.category = newCategory;
            File.WriteAllText(variable.vPath, JsonUtility.ToJson(variableToUpdate, true));

            AssetDatabase.ImportAsset(variable.vPath);
            UpdateVariablesUI();
        }

        public static void OnMagicTypeChanged(DynamicVariable variable, int newMagicType)
        {
            DynamicVariable variableToUpdate = JsonUtility.FromJson<DynamicVariable>(File.ReadAllText(variable.vPath));
            variableToUpdate.magicType = newMagicType;
            File.WriteAllText(variable.vPath, JsonUtility.ToJson(variableToUpdate, true));

            AssetDatabase.ImportAsset(variable.vPath);
            UpdateVariablesUI();
        }

        public static void OnIsListChanged(DynamicVariable variable, bool isList)
        {
            DynamicVariable variableToUpdate = JsonUtility.FromJson<DynamicVariable>(File.ReadAllText(variable.vPath));
            variableToUpdate.isList = isList;
            File.WriteAllText(variable.vPath, JsonUtility.ToJson(variableToUpdate, true));

            AssetDatabase.ImportAsset(variable.vPath);
            UpdateVariablesUI();
        }

        public static void AddInitialSelectorToVariableUI(DynamicVariable variable, VisualElement variableUI)
        {
            VisualElement container = variableUI.Q<VisualElement>(MagicLinksConst.SingleVariableInitialValueContainer);
            if (container == null) return;

            container.Clear();

            if (!MagicLinksInitialValue.IsSupported(variable.vLabelType, variable.isList, variable.magicType))
                return;

            VisualElement field = BuildInitialValueField(variable);
            if (field == null) return;

            field.style.flexGrow = 1;
            field.style.maxWidth = 240;
            container.Add(field);
        }

        // Dropdowns lose their selection when the UI rebuilds; restore from EditorPrefs the first time
        // we see them empty, otherwise just trust their current value.
        private static string RestoreDropdownValue(DropdownField field, string prefsKey, string fallback)
        {
            if (string.IsNullOrEmpty(field.value))
            {
                string stored = EditorPrefs.GetString(prefsKey, fallback);
                if (field.choices != null && field.choices.Contains(stored))
                    field.SetValueWithoutNotify(stored);
                else
                    field.SetValueWithoutNotify(fallback);
            }
            return field.value;
        }

        private static System.DateTime SafeGetCreationTime(string path)
        {
            try { return File.GetCreationTimeUtc(path); }
            catch { return System.DateTime.MinValue; }
        }

        // Defensive wrapper: Unity sometimes throws IndexOutOfRangeException in CloneSetupRecursively
        // when its UXML asset cache is out of sync (typical after edits while the window is open).
        // We log a clear hint instead of crashing the whole UI.
        private static VisualElement SafeInstantiate(VisualTreeAsset vta, string assetLabel)
        {
            if (vta == null)
            {
                Debug.LogError($"[MagicLinks] {assetLabel} UXML asset is null.");
                return null;
            }
            try
            {
                return vta.Instantiate();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MagicLinks] Failed to instantiate {assetLabel} UXML — Unity asset cache is likely stale. Right-click the .uxml file in the Project window and choose Reimport, or restart Unity. Inner: {e.Message}");
                return null;
            }
        }

        // Resets variables whose stored category no longer exists (e.g. user deleted it).
        // Performs file writes once per orphan WITHOUT triggering UI rebuilds — that would
        // re-enter UpdateVariablesUI while it's still iterating and corrupt Unity's UI tree.
        private static void FixOrphanCategories(List<DynamicVariable> variables)
        {
            MagicLinksConfiguration config = MagicLinksUtilities.GetConfiguration();
            HashSet<string> validCategories = new HashSet<string>(config.categories) { MagicLinksConst.CategoryNone };

            foreach (DynamicVariable v in variables)
            {
                string current = string.IsNullOrEmpty(v.category) ? MagicLinksConst.CategoryNone : v.category;
                if (validCategories.Contains(current)) continue;

                v.category = MagicLinksConst.CategoryNone;
                try
                {
                    File.WriteAllText(v.vPath, JsonUtility.ToJson(v, true));
                    AssetDatabase.ImportAsset(v.vPath);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[MagicLinks] Could not fix orphan category for '{v.vName}': {e.Message}");
                }
            }
        }

        private static void ApplyHeaderColors(VisualElement header, MagicLinksConfiguration config, string categoryKey)
        {
            // The variable category header has structure: outer VisualElement (.variableCategoryHeader)
            // with a Label child. The outer's background = main color, its left border = accent.
            VisualElement root = header is TemplateContainer tc && tc.childCount > 0 ? tc[0] : header;

            Color bg = config.variableHeaderColor;
            Color accent = config.variableHeaderAccent;

            MagicCategoryStyle style = MagicLinksAdvancedSettings.TryGetStyle(config, categoryKey);
            if (style != null && style.useCustomHeader)
            {
                bg = style.headerColor;
                // Derive accent from header color for custom headers (lighter shade).
                accent = Color.Lerp(bg, Color.white, 0.4f);
            }

            root.style.backgroundColor = bg;
            root.style.borderLeftColor = accent;
        }

        private static void ApplyRowColor(VisualElement variableUI, MagicLinksConfiguration config, string categoryKey)
        {
            MagicCategoryStyle style = MagicLinksAdvancedSettings.TryGetStyle(config, categoryKey);
            if (style == null || !style.useCustomRow) return;

            // Tint the inner Variable container so the row stands out.
            VisualElement variableRoot = variableUI.Q<VisualElement>("Variable");
            if (variableRoot != null) variableRoot.style.backgroundColor = style.rowColor;
        }

        private static void SetupVariableFoldout(DynamicVariable variable, VisualElement variableUI)
        {
            VisualElement settings = variableUI.Q<VisualElement>(MagicLinksConst.SingleVariableSettings);
            Button toggle = variableUI.Q<Button>(MagicLinksConst.SingleVariableExpandToggle);
            if (settings == null || toggle == null) return;

            string sessionKey = MagicLinksConst.ExpandSessionKeyPrefix + variable.vName;
            bool expanded = SessionState.GetBool(sessionKey, false);

            ApplyExpandedState(settings, toggle, expanded);

            toggle.clicked += () =>
            {
                bool newState = settings.style.display == DisplayStyle.None;
                SessionState.SetBool(sessionKey, newState);
                ApplyExpandedState(settings, toggle, newState);
            };
        }

        private static void ApplyExpandedState(VisualElement settings, Button toggle, bool expanded)
        {
            settings.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            toggle.text = expanded ? MagicLinksConst.ExpandedArrow : MagicLinksConst.CollapsedArrow;
        }

        private static VisualElement BuildInitialValueField(DynamicVariable variable)
        {
            MagicLinksInitialValue.TryParse(variable.vLabelType, variable.initialValue, out object parsed);

            switch (variable.vLabelType)
            {
                case MagicLinksConst.String:
                {
                    var f = new TextField();
                    f.SetValueWithoutNotify(parsed as string ?? string.Empty);
                    f.RegisterValueChangedCallback(e => UpdateDynamicVariableInitialValue(variable, e.newValue));
                    return f;
                }
                case MagicLinksConst.Bool:
                {
                    var f = new Toggle();
                    f.SetValueWithoutNotify(parsed is bool b && b);
                    f.RegisterValueChangedCallback(e => UpdateDynamicVariableInitialValue(variable, MagicLinksInitialValue.Format(e.newValue)));
                    return f;
                }
                case MagicLinksConst.Int:
                {
                    var f = new IntegerField();
                    f.SetValueWithoutNotify(parsed is int i ? i : 0);
                    f.RegisterValueChangedCallback(e => UpdateDynamicVariableInitialValue(variable, MagicLinksInitialValue.Format(e.newValue)));
                    return f;
                }
                case MagicLinksConst.Float:
                {
                    var f = new FloatField();
                    f.SetValueWithoutNotify(parsed is float fv ? fv : 0f);
                    f.RegisterValueChangedCallback(e => UpdateDynamicVariableInitialValue(variable, MagicLinksInitialValue.Format(e.newValue)));
                    return f;
                }
                case MagicLinksConst.Vector2:
                {
                    var f = new Vector2Field();
                    f.SetValueWithoutNotify(parsed is Vector2 v2 ? v2 : Vector2.zero);
                    f.RegisterValueChangedCallback(e => UpdateDynamicVariableInitialValue(variable, MagicLinksInitialValue.Format(e.newValue)));
                    return f;
                }
                case MagicLinksConst.Vector3:
                {
                    var f = new Vector3Field();
                    f.SetValueWithoutNotify(parsed is Vector3 v3 ? v3 : Vector3.zero);
                    f.RegisterValueChangedCallback(e => UpdateDynamicVariableInitialValue(variable, MagicLinksInitialValue.Format(e.newValue)));
                    return f;
                }
                case MagicLinksConst.Color:
                {
                    var f = new ColorField();
                    f.SetValueWithoutNotify(parsed is Color c ? c : Color.white);
                    f.RegisterValueChangedCallback(e => UpdateDynamicVariableInitialValue(variable, MagicLinksInitialValue.Format(e.newValue)));
                    return f;
                }
            }

            return null;
        }


        public static void UpdateDynamicVariableInitialValue(DynamicVariable variable, string initialValue)
        {
            DynamicVariable variableToUpdate = JsonUtility.FromJson<DynamicVariable>(File.ReadAllText(variable.vPath));
            variableToUpdate.initialValue = initialValue;
            // Keep the in-memory variable in sync so the inspector keeps its current value across rebuilds
            variable.initialValue = initialValue;
            File.WriteAllText(variable.vPath, JsonUtility.ToJson(variableToUpdate, true));

            AssetDatabase.ImportAsset(variable.vPath);
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
            AssetDatabase.DeleteAsset(path);
            UpdateVariablesUI();
        }
    }
}
#endif