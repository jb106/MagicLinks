#if UNITY_EDITOR
using UnityEditor;
using Sirenix.Utilities.Editor;
#endif

using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing.Printing;

namespace MagicLinks
{
    public class MagicLinks_Window : EditorWindow
    {
#if UNITY_EDITOR
        private string linksPath = "";
        private string customLinkType = "";

        private bool generateVariable = true;
        private bool generateEvent = true;

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

            generateVariable = EditorGUILayout.Toggle("Generate Variable", generateVariable);
            generateEvent = EditorGUILayout.Toggle("Generate Event", generateEvent);

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
            if(linksPath == string.Empty || Directory.Exists(linksPath) == false)
            {
                Debug.LogError("Invalid Directory");
                return;
            }

            if (customLinkType == string.Empty || CheckClassExist(customLinkType) == false)
            {
                Debug.LogError("Invalid Class Name");
                return;
            }

            string upperCaseName = CapitalizeFirstLetter(customLinkType);

            string variableClassName = "MagicVariable_" + upperCaseName;
            string eventClassName = "MagicEvent_" + upperCaseName;
            string variableListenerClassName = "MagicVariableListener_" + upperCaseName;

            string variablesPath = Path.Combine(linksPath, "Variables");
            string eventsPath = Path.Combine(linksPath, "Events");
            string variablesListenerPath = Path.Combine(linksPath, "VariablesListener");

            //Variable creation
            //----------------------------------------------------
            if (generateVariable)
            {
                CreateScript(variablesPath, variableClassName, upperCaseName, customLinkType, variableTemplate);
                CreateScript(variablesListenerPath, variableListenerClassName, upperCaseName, customLinkType, variableListenerTemplate);
            }

            //Event creation
            //----------------------------------------------------
            if (generateEvent)
            {
                CreateScript(eventsPath, eventClassName, upperCaseName, customLinkType, eventTemplate);
            }
        }

        private bool CheckClassExist(string cN)
        {
            var baseTypes = typeof(object).Assembly.GetTypes()
            .Where(t => t.IsPrimitive || t == typeof(string) || t == typeof(decimal));

            foreach (var type in baseTypes)
            {
                string alias = typeAliases.ContainsKey(type) ? typeAliases[type] : type.ToString();
                if (cN == alias) return true;
            }

            return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Any(t => t.Name == cN || t.FullName == cN);
        }

        private void CreateScript(string path, string className, string labelType, string realType, string template)
        {
            if (CheckClassExist(className)) return;

            if (Directory.Exists(path) == false) Directory.CreateDirectory(path);
            AssetDatabase.Refresh();

            Debug.Log(labelType);
            Debug.Log(realType);

            string content = template.Replace("{TYPELABEL}", labelType).Replace("{TYPE}", realType);
            content = content.Replace("{TYPE}", realType);

            string finalPath = Path.Combine(path, className + ".cs");
            File.WriteAllText(finalPath, content);

            AssetDatabase.Refresh();
        }

        //--------------------------------------------------------

        string variableTemplate = @"
        using UnityEngine;

        namespace MagicLinks
        {
            [CreateAssetMenu(menuName = ""MagicLinks/Variables/{TYPELABEL}"", fileName = ""{TYPELABEL}Variable"")]
            public class MagicVariable_{TYPELABEL} : MagicVariable<{TYPE}> { }
        }";

        string eventTemplate = @"
        using UnityEngine;

        namespace MagicLinks
        {
            [CreateAssetMenu(menuName = ""MagicLinks/Events/{TYPELABEL}"", fileName = ""{TYPELABEL}Event"")]
            public class MagicEvent_{TYPELABEL} : MagicEvent<{TYPE}> { }
        }";

        string variableListenerTemplate = @"
        using UnityEngine;

        namespace MagicLinks
        {
            public class MagicVariableListener_{TYPELABEL} : MagicVariableListener<{TYPE}>
            {
                [SerializeField] private MagicVariable_{TYPELABEL} _variable;
                protected override MagicVariable<{TYPE}> Variable => _variable;
            }
        }
        ";

        private readonly Dictionary<Type, string> typeAliases = new Dictionary<Type, string>
        {
            { typeof(bool), "bool" },
            { typeof(byte), "byte" },
            { typeof(sbyte), "sbyte" },
            { typeof(char), "char" },
            { typeof(decimal), "decimal" },
            { typeof(double), "double" },
            { typeof(float), "float" },
            { typeof(int), "int" },
            { typeof(uint), "uint" },
            { typeof(long), "long" },
            { typeof(ulong), "ulong" },
            { typeof(short), "short" },
            { typeof(ushort), "ushort" },
            { typeof(string), "string" },
            { typeof(object), "object" }
        };

        private string CapitalizeFirstLetter(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return char.ToUpper(input[0]) + input.Substring(1);
        }
#endif
    }
}
