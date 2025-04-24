#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MagicLinks.Observables;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MagicLinks
{
    [DefaultExecutionOrder(-1000)]
    public class MagicVariablesTemplate : MonoBehaviour
    {
        public static MagicVariablesTemplate Instance;
        
        //VARIABLESLISTS
        
        public class VariableEntry
        {
            public string key;
            public string type;
            public int magicType;

            public VariableEntry(string key, string type, int magicType)
            {
                this.key = key;
                this.type = type;
                this.magicType = magicType;
            }
        }

        private List<VariableEntry> initialVariables = new List<VariableEntry>();
        private void Awake()
        {
            if(Instance != null) Destroy(gameObject);
            
            Instance = this;

            // Load the variables
            List<DynamicVariable> variables = new List<DynamicVariable>();
            foreach (var v in GetExistingVariables())
            {
                initialVariables.Add(new VariableEntry(v.vName, v.vLabelType.ToUpper(), v.magicType));
            }

            // Feed the variables
            foreach (var entry in initialVariables)
            {
                if (entry.magicType == 2)
                {
                    /*VOID.Add(entry.key, new MagicEventVoidObservable());*/
                }
                else
                {
                    string dictName = entry.type.ToUpper() + (entry.magicType == 1 ?  MagicLinksConst.EventDict : "");
                    FieldInfo field = GetType().GetField(dictName, BindingFlags.Public | BindingFlags.Instance);

                    if (field != null)
                    {
                        var dict = field.GetValue(this);
                        Type dictType = dict.GetType();

                        Type keyType = dictType.GetGenericArguments()[0];
                        Type expectedWrapperType = GetMagicType(entry.magicType == 1);
                        Type valueInnerType = dictType.GetGenericArguments()[1].GetGenericArguments()[0]; // T dans MagicVariableObservable<T> ou MagicEventObservable<T>

                        Type constructedType = expectedWrapperType.MakeGenericType(valueInnerType);
                        object defaultValue = Activator.CreateInstance(constructedType);

                        MethodInfo addMethod = dictType.GetMethod("Add");
                        addMethod.Invoke(dict, new object[] { entry.key, defaultValue });
                    }
                    else
                    {
                        Debug.LogWarning($"Dictionnaire de type {entry.type} introuvable dans MagicVariables.");
                    }
                }
            }
        }

        private Type GetMagicType(bool isEvent)
        {
            if (isEvent) return typeof(MagicEventObservable<>);
            return typeof(MagicVariableObservable<>);
        }
        
        private List<DynamicVariable> GetExistingVariables()
        {
            List<DynamicVariable> existingVariables = new List<DynamicVariable>();
            
            TextAsset[] variables = Resources.LoadAll<TextAsset>(MagicLinksConst.VariablesResourcesPath);

            foreach(TextAsset t in variables)
            {
                try
                {
                    DynamicVariable v = JsonUtility.FromJson<DynamicVariable>(t.text);
                    existingVariables.Add(v);
                }
                catch (System.Exception e)
                {
                    //Debug.LogError(e);
                }
            }
            
            return existingVariables;
        }
    }
    /*

    static class MagicLinksBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Init()
        {
            if (UnityEngine.Object.FindFirstObjectByType<MagicLinksManager>() == null)
            {
                GameObject obj = new GameObject("MagicLinksManager");
                UnityEngine.Object.DontDestroyOnLoad(obj);
                obj.AddComponent<MagicLinksManager>();
            }
        }
    }

    public static class MagicVariables
    {
        //MAGICVARIABLESGETTER
    }

    public static class MagicEvents
    {
        //MAGICEVENTSGETTER
    }
    */
}
#endif

//#SEPARATION
/*
using UnityEngine;
using UnityEngine.Events;
namespace MagicLinks
{
    public class #NAME : MonoBehaviour
    {
        public string reference;
        public UnityEvent#ETYPE onEventRaised = new UnityEvent#ETYPE();

        private void OnEnable()
        {
            #LINK.#DICT[reference].#KIND += OnRaised;
        }
        
        private void OnDisable()
        {
            #LINK.#DICT[reference].#KIND -= OnRaised;
        }

        private void OnRaised(#TYPE i)
        {
            onEventRaised.Invoke(i);
        }
    }
}

//#SEPARATION
using UnityEngine;
using UnityEngine.Events;

namespace MagicLinks
{
    public class Void_EventListener : MonoBehaviour
    {
        public string reference;
        public UnityEvent onEventRaised = new UnityEvent();

        private void OnEnable()
        {
            MagicEvents.VOID[reference].OnEventRaised += OnRaised;
        }
        
        private void OnDisable()
        {
            MagicEvents.VOID[reference].OnEventRaised -= OnRaised;
        }

        private void OnRaised()
        {
            onEventRaised.Invoke();
        }
    }
}

*/