#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;

namespace MagicLinks
{
    public class MagicLinks_Window : EditorWindow
    {
#if UNITY_EDITOR
        [AssetList(AutoPopulate = true)] private List<MagicVariableBase> magicVariables = new List<MagicVariableBase>();

        private string linksPath = "";
        private string customLinkType = "";

        private List<MagicVariableBase> variables = new List<MagicVariableBase>();
        private List<string> variablesValues = new List<string>();

        [MenuItem("Window/Magic Links")]
        public static void ShowWindow()
        {
            MagicLinks_Window window = GetWindow<MagicLinks_Window>("Magic Links");
            window.minSize = new Vector2(250, 150);
        }

        private void OnGUI()
        {
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 18;  // Taille du texte
            headerStyle.fontStyle = FontStyle.Bold;  // Style du texte (gras)
            headerStyle.alignment = TextAnchor.UpperCenter;
            headerStyle.wordWrap = true;

            GUILayout.Space(20);
            EditorGUILayout.LabelField("Custom Links", headerStyle);
            GUILayout.Space(20);

            linksPath = EditorGUILayout.TextField("Links Path", linksPath);
            customLinkType = EditorGUILayout.TextField("Custom Link Type", customLinkType);

            if (GUILayout.Button("Generate Custom Link"))
            {
                CreateCustomType();
            }

            GUILayout.Space(20);
            EditorGUILayout.LabelField("Variables (Debug)", headerStyle);
            GUILayout.Space(20);

            variables = AssetUtilities.GetAllAssetsOfType<MagicVariableBase>().ToList();

            foreach(MagicVariableBase b in variables)
            {
                b.OnGlobalValueChanged -= UpdateGUI;
            }

            variablesValues.Clear();
            foreach(MagicVariableBase b in variables)
            {
                if (b.GetValueAsObject() != null) variablesValues.Add(b.GetValueAsObject().ToString());
                else variablesValues.Add(string.Empty);

                b.OnGlobalValueChanged += UpdateGUI;
            }

            for(int x = 0; x < variables.Count; x++)
            {
                GUILayout.BeginHorizontal();
                variables[x] = (MagicVariableBase)EditorGUILayout.ObjectField(variables[x], typeof(MagicVariableBase), true);
                variablesValues[x] = EditorGUILayout.TextField(variablesValues[x].ToString());
                GUILayout.EndHorizontal();
            }
        }

        private void UpdateGUI()
        {
            Repaint();
        }

        private void CreateCustomType()
        {
            if(Directory.Exists(linksPath) == false)
            {
                Debug.LogError("Invalid Directory");
                return;
            }

            if (CheckClassExist(customLinkType) == false)
            {
                Debug.LogError("Invalid Class Name");
                return;
            }

            string variableClassName = "MagicVariable_" + customLinkType;
            string eventClassName = "MagicEvent" + customLinkType;

            string variablesPath = Path.Combine(linksPath, "Variables");
            string eventsPath = Path.Combine(linksPath, "Events");

            //Variable creation
            //----------------------------------------------------
            if (CheckClassExist(variableClassName) == false)
            {
                if (Directory.Exists(variablesPath) == false) Directory.CreateDirectory(variablesPath);
                AssetDatabase.Refresh();

                string variableContent = variableTemplate.Replace("{TYPE}", customLinkType);
                string variablePath = Path.Combine(variablesPath, variableClassName + ".cs");
                File.WriteAllText(variablePath, variableContent);
                AssetDatabase.Refresh();
            }

            //Event creation
            //----------------------------------------------------
            if (CheckClassExist(eventClassName) == false)
            {
                if (Directory.Exists(eventsPath) == false) Directory.CreateDirectory(eventsPath);
                AssetDatabase.Refresh();

                string eventContent = eventTemplate.Replace("{TYPE}", customLinkType);
                string eventPath = Path.Combine(eventsPath, eventClassName + ".cs");
                File.WriteAllText(eventPath, eventContent);
                AssetDatabase.Refresh();
            }
        }

        private bool CheckClassExist(string cN)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Any(t => t.Name == cN || t.FullName == cN);
        }

        //--------------------------------------------------------

        string variableTemplate = @"
        using UnityEngine;

        namespace MagicLinks
        {
            [CreateAssetMenu(menuName = ""MagicLinks/Variables/{TYPE}"", fileName = ""{TYPE}Variable"")]
            public class MagicVariable_{TYPE} : MagicVariable<{TYPE}> { }
        }";

        string eventTemplate = @"
        using UnityEngine;

        namespace MagicLinks
        {
            [CreateAssetMenu(menuName = ""MagicLinks/Events/{TYPE}"", fileName = ""{TYPE}Event"")]
            public class MagicEvent_{TYPE} : MagicEvent<{TYPE}> { }
        }";
#endif
    }
}
