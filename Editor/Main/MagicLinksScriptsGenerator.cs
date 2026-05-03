#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace MagicLinks
{
    public static class MagicLinksScriptsGenerator
    {
        private static string _cachedTemplate;

        private static string GetTemplate()
        {
            if (_cachedTemplate != null) return _cachedTemplate;
            _cachedTemplate = File.ReadAllText(
                MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.MagicVariablesTemplate));
            return _cachedTemplate;
        }

        public static void GenerateMagicVariablesScript(bool ifMissing)
        {
            MagicLinksUtilities.CreateVariablesFolder();

            string newClassPath = Path.Combine(MagicLinksConst.ConfigurationPath,
                MagicLinksConst.MagicVariableClassName + ".cs");

            if (ifMissing && File.Exists(newClassPath)) return;

            string classContent = GetMagicVariablesScriptContent();

            if (File.Exists(newClassPath) && File.ReadAllText(newClassPath) == classContent) return;

            File.WriteAllText(newClassPath, classContent);

            AssetDatabase.ImportAsset(newClassPath);
        }

        public static string GetMagicVariablesScriptContent()
        {
            var allTypes = MagicLinksUtilities.GetAllTypes();

            var sb = new System.Text.StringBuilder(GetTemplate());

            sb.Replace("MagicVariablesTemplate", MagicLinksConst.MagicVariableClassName);

            var variables = new System.Text.StringBuilder();
            foreach (string customType in allTypes)
            {
                AppendDict(variables, MagicLinksConst.VariableDictTemplate, customType, customType.ToUpper());
                AppendDict(variables, MagicLinksConst.ListVariableDictTemplate, customType, customType.ToUpper() + MagicLinksConst.ListDict);
            }

            variables.Append("\n \n");

            foreach (string customType in allTypes)
            {
                AppendDict(variables, MagicLinksConst.EventDictTemplate, customType,
                    customType.ToUpper() + MagicLinksConst.EventDict);
                AppendDict(variables, MagicLinksConst.ListEventDictTemplate, customType,
                    customType.ToUpper() + MagicLinksConst.ListDict + MagicLinksConst.EventDict);
            }

            variables.Append(MagicLinksConst.EventVoidDictTemplate);

            sb.Replace("//VARIABLESLISTS", variables.ToString());
            sb.Replace("/*", string.Empty);
            sb.Replace("*/", string.Empty);
            sb.Replace("#if UNITY_EDITOR", string.Empty);
            sb.Replace("#endif", string.Empty);

            sb.Replace("//STARTUSINGEDITOR", "#if UNITY_EDITOR");
            sb.Replace("//ENDUSINGEDITOR", "#endif");

            //Generate Getters
            var variablesGetter = new System.Text.StringBuilder();
            foreach (string customType in allTypes)
            {
                AppendDict(variablesGetter, MagicLinksConst.VariableGetterTemplate, customType, customType.ToUpper());
                AppendDict(variablesGetter, MagicLinksConst.ListVariableGetterTemplate, customType, customType.ToUpper() + MagicLinksConst.ListDict);
            }

            sb.Replace("//MAGICVARIABLESGETTER", variablesGetter.ToString());

            var eventsGetter = new System.Text.StringBuilder();
            foreach (string customType in allTypes)
            {
                string eventGetter = GetDict(MagicLinksConst.EventGetterTemplate, customType,
                    customType.ToUpper() + MagicLinksConst.EventDict);

                eventGetter = eventGetter.Replace("SHORT", customType.ToUpper());

                eventsGetter.Append(eventGetter);

                string listEventGetter = GetDict(MagicLinksConst.ListEventGetterTemplate, customType,
                    customType.ToUpper() + MagicLinksConst.ListDict + MagicLinksConst.EventDict);

                listEventGetter = listEventGetter.Replace("SHORT", customType.ToUpper() + MagicLinksConst.ListDict);

                eventsGetter.Append(listEventGetter);
            }

            eventsGetter.Append(MagicLinksConst.EventVoidGetterTemplate);

            sb.Replace("//MAGICEVENTSGETTER", eventsGetter.ToString());

            return sb.ToString().Split(MagicLinksConst.TemplateListenerSeparation)[0];
        }

        private static void AppendDict(System.Text.StringBuilder target, string dict, string t, string n)
        {
            int start = target.Length;
            target.Append(dict);
            target.Replace("TYPE", t, start, target.Length - start);
            target.Replace("NAME", n, start, target.Length - start);
            target.Append('\n');
        }

        public static string GetDict(string dict, string t, string n)
        {
            string newDict = dict;
            newDict = newDict.Replace("TYPE", t);
            newDict = newDict.Replace("NAME", n);
            newDict += "\n";
            return newDict;
        }

        //----------------------------------------

        public static void ClearListeners(bool everything)
        {
            MagicLinksUtilities.CreateListenersFolder();

            var allTypes = MagicLinksUtilities.GetAllTypes();

            HashSet<string> eventsListenersScriptsName = new HashSet<string>();
            HashSet<string> variablesListenersScriptsName = new HashSet<string>();

            foreach (string t in allTypes)
            {
                eventsListenersScriptsName.Add(MagicLinksUtilities.GetEventListenerName(t) + ".cs");
                variablesListenersScriptsName.Add(MagicLinksUtilities.GetVariableListenerName(t) + ".cs");
            }

            bool anyDeleted = false;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string file in Directory.GetFiles(MagicLinksConst.EventsListenersPath))
                {
                    if (CheckDeleteFile(file, everything, eventsListenersScriptsName)) anyDeleted = true;
                }

                foreach (string file in Directory.GetFiles(MagicLinksConst.VariablesListenersPath))
                {
                    if (CheckDeleteFile(file, everything, variablesListenersScriptsName)) anyDeleted = true;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (anyDeleted) AssetDatabase.Refresh();
        }

        public static bool CheckDeleteFile(string file, bool everything, HashSet<string> toCompare)
        {
            if (Path.GetExtension(file) != ".cs") return false;

            string fileName = Path.GetFileName(file);

            if (everything == false && fileName == MagicLinksUtilities.GetEventListenerName("Void") + ".cs") return false;

            if (everything == false && toCompare.Contains(fileName)) return false;

            File.Delete(file);
            string metaPath = file + ".meta";
            if (File.Exists(metaPath)) File.Delete(metaPath);
            return true;
        }

        public static void GenerateListenersScripts()
        {
            GenerateListenersScripts(false);
        }

        public static void GenerateListenersScripts(bool ifMissing)
        {
            MagicLinksUtilities.CreateListenersFolder();

            string template = GetTemplate();
            template = template.Replace("/*", string.Empty);
            template = template.Replace("*/", string.Empty);

            string[] separated = template.Split(MagicLinksConst.TemplateListenerSeparation);

            bool anyWritten = false;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string t in MagicLinksUtilities.GetAllTypes())
                {
                    //Event Listeners
                    string eventClassName = MagicLinksUtilities.GetEventListenerName(t);
                    string eventListenerPath = Path.Combine(MagicLinksConst.EventsListenersPath, eventClassName + ".cs");
                    if (WriteIfChanged(eventListenerPath,
                            CreateListenerContent(separated[1], eventClassName, t, "MagicEvents", "OnEventRaised"),
                            ifMissing))
                        anyWritten = true;

                    //Variable Listeners
                    string variableClassName = MagicLinksUtilities.GetVariableListenerName(t);
                    string variableListenerPath = Path.Combine(MagicLinksConst.VariablesListenersPath, variableClassName + ".cs");
                    if (WriteIfChanged(variableListenerPath,
                            CreateListenerContent(separated[1], variableClassName, t, "MagicVariables", "OnValueChanged"),
                            ifMissing))
                        anyWritten = true;
                }

                //Event Listener VOID
                string voidClassName = MagicLinksUtilities.GetEventListenerName("Void");
                string voidListenerPath = Path.Combine(MagicLinksConst.EventsListenersPath, voidClassName + ".cs");
                if (WriteIfChanged(voidListenerPath, separated[2], ifMissing)) anyWritten = true;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (anyWritten) AssetDatabase.Refresh();
        }

        private static bool WriteIfChanged(string path, string content, bool ifMissing)
        {
            if (File.Exists(path))
            {
                if (ifMissing) return false;
                if (File.ReadAllText(path) == content) return false;
            }
            File.WriteAllText(path, content);
            return true;
        }

        public static string CreateListenerContent(string template, string n, string t, string link, string kind)
        {
            template = template.Replace("#NAME", MagicLinksUtilities.GetStringWithFirstUpperCaseLetter(n));
            template = template.Replace("#LINK", link);
            template = template.Replace("#KIND", kind);
            template = template.Replace("#ETYPE", "<" + t + ">");
            template = template.Replace("#TYPE", t);
            template = template.Replace("#DICT", t.ToUpper());

            return template;
        }
    }
}
#endif