#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace MagicLinks
{
    public static class MagicLinksScriptsGenerator
    {
        public static void GenerateMagicVariablesScript(bool ifMissing)
        {
            string classContent = GetMagicVariablesScriptContent();

            MagicLinksUtilities.CreateVariablesFolder();

            string newClassPath = Path.Combine(MagicLinksConst.ConfigurationPath,
                MagicLinksConst.MagicVariableClassName + ".cs");

            if (ifMissing && File.Exists(newClassPath)) return;

            File.WriteAllText(newClassPath, classContent);

            AssetDatabase.Refresh();
        }

        public static string GetMagicVariablesScriptContent()
        {
            string classContent =
                File.ReadAllText(MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.MagicVariablesTemplate));

            classContent = classContent.Replace("MagicVariablesTemplate", MagicLinksConst.MagicVariableClassName);

            string variables = string.Empty;

            var listTypes = new HashSet<string>();
            foreach (var v in MagicLinksInternalVar.GetExistingVariables())
            {
                if (!v.IsEvent() && v.isList)
                    listTypes.Add(v.vLabelType);
            }

            foreach (string customType in MagicLinksUtilities.GetAllTypes())
            {
                if (customType.StartsWith("List", StringComparison.Ordinal))
                    continue;

                variables += GetDict(MagicLinksConst.VariableDictTemplate, customType, customType.ToUpper());
            }

            foreach (string listType in listTypes)
            {
                variables += GetDict(MagicLinksConst.ListDictTemplate, listType, listType.ToUpper() + MagicLinksConst.ListSuffix);
            }

            variables += "\n \n";

            foreach (string customType in MagicLinksUtilities.GetAllTypes())
            {
                if (customType.StartsWith("List", StringComparison.Ordinal))
                    continue;

                variables += GetDict(MagicLinksConst.EventDictTemplate, customType,
                    customType.ToUpper() + MagicLinksConst.EventDict);
            }

            variables += MagicLinksConst.EventVoidDictTemplate;

            classContent = classContent.Replace("//VARIABLESLISTS", variables);
            classContent = classContent.Replace("/*", string.Empty);
            classContent = classContent.Replace("*/", string.Empty);
            classContent = classContent.Replace("#if UNITY_EDITOR", string.Empty);
            classContent = classContent.Replace("#endif", string.Empty);

            classContent = classContent.Replace("//STARTUSINGEDITOR", "#if UNITY_EDITOR");
            classContent = classContent.Replace("//ENDUSINGEDITOR", "#endif");

            //Generate Getters
            string variablesGetter = string.Empty;
            foreach (string customType in MagicLinksUtilities.GetAllTypes())
            {
                if (customType.StartsWith("List", StringComparison.Ordinal))
                    continue;

                variablesGetter += GetDict(MagicLinksConst.VariableGetterTemplate, customType, customType.ToUpper());
            }

            foreach (string listType in listTypes)
            {
                variablesGetter += GetDict(MagicLinksConst.VariableGetterTemplate, $"List<{listType}>", listType.ToUpper() + MagicLinksConst.ListSuffix);
            }

            classContent = classContent.Replace("//MAGICVARIABLESGETTER", variablesGetter);

            string eventsGetter = string.Empty;
            foreach (string customType in MagicLinksUtilities.GetAllTypes())
            {
                if (customType.StartsWith("List", StringComparison.Ordinal))
                    continue;

                string eventGetter = GetDict(MagicLinksConst.EventGetterTemplate, customType,
                    customType.ToUpper() + MagicLinksConst.EventDict);

                eventGetter = eventGetter.Replace("SHORT", customType.ToUpper());

                var regex = new Regex(Regex.Escape(MagicLinksConst.EventDict));
                eventsGetter = regex.Replace(eventsGetter, MagicLinksConst.EventDict, 1);

                eventsGetter += eventGetter;
            }

            eventsGetter += MagicLinksConst.EventVoidGetterTemplate;

            classContent = classContent.Replace("//MAGICEVENTSGETTER", eventsGetter);

            return classContent.Split(MagicLinksConst.TemplateListenerSeparation)[0];
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

            List<string> eventsListenersScriptsName = new List<string>();
            List<string> variablesListenersScriptsName = new List<string>();

            foreach (string t in MagicLinksUtilities.GetAllTypes())
            {
                if (t.StartsWith("List", StringComparison.Ordinal))
                    continue;

                eventsListenersScriptsName.Add(MagicLinksUtilities.GetEventListenerName(t) + ".cs");
            }

            string[] eventsFiles = Directory.GetFiles(MagicLinksConst.EventsListenersPath);

            foreach (string file in eventsFiles)
            {
                CheckDeleteFile(file, everything, eventsListenersScriptsName);
            }
            
            //-------------------------------------
            
            foreach (string t in MagicLinksUtilities.GetAllTypes())
            {
                variablesListenersScriptsName.Add(MagicLinksUtilities.GetVariableListenerName(t) + ".cs");
            }

            string[] variablesFiles = Directory.GetFiles(MagicLinksConst.VariablesListenersPath);

            foreach (string file in variablesFiles)
            {
                CheckDeleteFile(file, everything, variablesListenersScriptsName);
            }

            AssetDatabase.Refresh();
        }

        public static void CheckDeleteFile(string file, bool everything, List<string> toCompare) 
        {
            if (Path.GetExtension(file) != ".cs") return;

            string fileName = Path.GetFileName(file);
                
            if(everything == false && fileName == MagicLinksUtilities.GetEventListenerName("Void") + ".cs") return;

            if (everything == false && toCompare.Contains(fileName)) return;

            File.Delete(file);
        }

        public static void GenerateListenersScripts()
        {
            MagicLinksUtilities.CreateListenersFolder();

            string template = File.ReadAllText(MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.MagicVariablesTemplate));
            template = template.Replace("/*", string.Empty);
            template = template.Replace("*/", string.Empty);

            string[] separated = template.Split(MagicLinksConst.TemplateListenerSeparation);
            
            foreach (string t in MagicLinksUtilities.GetAllTypes())
            {
                if (!t.StartsWith("List", StringComparison.Ordinal))
                {
                    //Event Listeners
                    string eventClassName = MagicLinksUtilities.GetEventListenerName(t);

                    string eventListenerPath = Path.Combine(MagicLinksConst.EventsListenersPath, eventClassName + ".cs");
                    string eventListenerContent = CreateListenerContent(separated[1], eventClassName, t, "MagicEvents", "OnEventRaised");
                    File.WriteAllText(eventListenerPath, eventListenerContent);
                }

                //Variable Listeners
                string variableClassName = MagicLinksUtilities.GetVariableListenerName(t);

                string variableListenerPath = Path.Combine(MagicLinksConst.VariablesListenersPath, variableClassName + ".cs");
                string variableListenerContent = CreateListenerContent(separated[1], variableClassName, t, "MagicVariables", "OnValueChanged");
                File.WriteAllText(variableListenerPath, variableListenerContent);
            }
            
            //Event Listener VOID
            string voidClassName = MagicLinksUtilities.GetEventListenerName("Void");
            string voidListenerPath = Path.Combine(MagicLinksConst.EventsListenersPath, voidClassName + ".cs");

            File.WriteAllText(voidListenerPath, separated[2]);

            AssetDatabase.Refresh();
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