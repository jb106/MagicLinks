#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

[DefaultExecutionOrder(-1000)]
public class MagicVariablesTemplate : MonoBehaviour
{
    public static MagicVariablesTemplate Instance;
    
    //VARIABLESLISTS
    
    public class VariableEntry
    {
        public string key;
        public string type;
        public bool isEvent;

        public VariableEntry(string key, string type, bool isEvent)
        {
            this.key = key;
            this.type = type;
            this.isEvent = isEvent;
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
            initialVariables.Add(new VariableEntry(v.vName, v.vLabelType.ToUpper(), v.isEvent));
        }

        // Feed the variables
        foreach (var entry in initialVariables)
        {
            
            string dictName = entry.type.ToUpper() + (entry.isEvent ?  MagicLinksUtilities.EventDict : "");
            FieldInfo field = GetType().GetField(dictName, BindingFlags.Public | BindingFlags.Instance);

            if (field != null)
            {
                var dict = field.GetValue(this);
                Type dictType = dict.GetType();

                Type keyType = dictType.GetGenericArguments()[0];
                Type expectedWrapperType = GetMagicType(entry.isEvent);
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

    private Type GetMagicType(bool isEvent)
    {
        if (isEvent) return typeof(MagicEventObservable<>);
        return typeof(MagicVariableObservable<>);
    }
    
    private List<DynamicVariable> GetExistingVariables()
    {
        List<DynamicVariable> existingVariables = new List<DynamicVariable>();
        
        TextAsset[] variables = Resources.LoadAll<TextAsset>(MagicLinksUtilities.ResourcesVariablesPath);

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

public class MagicEventObservable<T>
{
    public event Action<T> OnEventRaised;

    public void Raise(T v)
    {
        OnEventRaised?.Invoke(v);
    }
}

public class MagicVariableObservable<T>
{
    private T _value;

    public T Value
    {
        get => _value;
        set
        {
            if (!Equals(_value, value))
            {
                _value = value;
                OnValueChanged?.Invoke(_value);
            }
        }
    }

    public event Action<T> OnValueChanged;

    public MagicVariableObservable() => _value = default;
    public MagicVariableObservable(T initialValue) => _value = initialValue;
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

#endif