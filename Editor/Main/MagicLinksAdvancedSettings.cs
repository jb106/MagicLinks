#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MagicLinks
{
    public static class MagicLinksAdvancedSettings
    {
        public static void Build()
        {
            Foldout root = MagicLinkEditor.Instance.rootVisualElement
                .Q<Foldout>(MagicLinksConst.AdvancedSettingsFoldout);
            if (root == null) return;

            root.Clear();

            MagicLinksConfiguration config = MagicLinksUtilities.GetConfiguration();

            // ---- Default header colors (used when a category has no custom override) ----
            root.Add(MakeSectionLabel("Default header"));

            ColorField defaultBg = MakeColorField("Background", config.variableHeaderColor, c =>
            {
                config.variableHeaderColor = c;
                EditorUtility.SetDirty(config);
                MagicLinksInternalVar.UpdateVariablesUI();
            });
            root.Add(defaultBg);

            ColorField defaultAccent = MakeColorField("Accent (border)", config.variableHeaderAccent, c =>
            {
                config.variableHeaderAccent = c;
                EditorUtility.SetDirty(config);
                MagicLinksInternalVar.UpdateVariablesUI();
            });
            root.Add(defaultAccent);

            // ---- Per-category styles ----
            // Always offer a slot for each existing category, plus the implicit "None".
            root.Add(MakeSectionLabel("Per-category colors"));

            BuildCategoryStyleRow(root, config, MagicLinksConst.CategoryNone);
            foreach (string cat in config.categories)
                BuildCategoryStyleRow(root, config, cat);
        }

        private static void BuildCategoryStyleRow(VisualElement parent, MagicLinksConfiguration config, string categoryName)
        {
            MagicCategoryStyle style = GetOrCreateStyle(config, categoryName);

            VisualElement box = new VisualElement();
            box.style.flexDirection = FlexDirection.Column;
            box.style.marginLeft = 6;
            box.style.marginRight = 6;
            box.style.marginTop = 4;
            box.style.marginBottom = 4;
            box.style.paddingTop = 4;
            box.style.paddingBottom = 4;
            box.style.paddingLeft = 6;
            box.style.paddingRight = 6;
            box.style.backgroundColor = new Color(0, 0, 0, 0.18f);
            box.style.borderTopLeftRadius = 3;
            box.style.borderTopRightRadius = 3;
            box.style.borderBottomLeftRadius = 3;
            box.style.borderBottomRightRadius = 3;

            Label title = new Label(categoryName);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 2;
            box.Add(title);

            // Header override
            Toggle headerToggle = new Toggle("Custom header") { value = style.useCustomHeader };
            headerToggle.RegisterValueChangedCallback(evt =>
            {
                style.useCustomHeader = evt.newValue;
                EditorUtility.SetDirty(config);
                MagicLinksInternalVar.UpdateVariablesUI();
            });
            box.Add(headerToggle);

            ColorField headerColor = MakeColorField("  Header color", style.headerColor, c =>
            {
                style.headerColor = c;
                EditorUtility.SetDirty(config);
                MagicLinksInternalVar.UpdateVariablesUI();
            });
            box.Add(headerColor);

            // Row override
            Toggle rowToggle = new Toggle("Custom row tint") { value = style.useCustomRow };
            rowToggle.RegisterValueChangedCallback(evt =>
            {
                style.useCustomRow = evt.newValue;
                EditorUtility.SetDirty(config);
                MagicLinksInternalVar.UpdateVariablesUI();
            });
            box.Add(rowToggle);

            ColorField rowColor = MakeColorField("  Row tint", style.rowColor, c =>
            {
                style.rowColor = c;
                EditorUtility.SetDirty(config);
                MagicLinksInternalVar.UpdateVariablesUI();
            });
            box.Add(rowColor);

            parent.Add(box);
        }

        public static MagicCategoryStyle GetOrCreateStyle(MagicLinksConfiguration config, string categoryName)
        {
            MagicCategoryStyle style = config.categoryStyles.FirstOrDefault(s => s.category == categoryName);
            if (style == null)
            {
                style = new MagicCategoryStyle { category = categoryName };
                config.categoryStyles.Add(style);
                EditorUtility.SetDirty(config);
            }
            return style;
        }

        public static MagicCategoryStyle TryGetStyle(MagicLinksConfiguration config, string categoryName)
        {
            return config.categoryStyles.FirstOrDefault(s => s.category == categoryName);
        }

        private static Label MakeSectionLabel(string text)
        {
            Label l = new Label(text);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.unityTextAlign = TextAnchor.UpperLeft;
            l.style.marginLeft = 4;
            l.style.marginTop = 6;
            l.style.marginBottom = 2;
            l.style.opacity = 0.85f;
            return l;
        }

        private static ColorField MakeColorField(string label, Color initial, System.Action<Color> onChange)
        {
            ColorField f = new ColorField(label) { value = initial };
            f.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            return f;
        }
    }
}
#endif
