#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class MagicVariablesTemplate : MonoBehaviour
{
    public static MagicVariablesTemplate Instance;
    
    //VARIABLESLISTS
    
    public class VariableEntry
    {
        public string key; //Nom de la variable
        public string type; // Type de la variable

        public VariableEntry(string key, string type)
        {
            this.key = key;
            this.type = type;
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
            initialVariables.Add(new VariableEntry(v.vName, v.vLabelType.ToUpper()));
        }

        // Feed the variables
        foreach (var entry in initialVariables)
        {
            string dictName = entry.type.ToUpper();
            FieldInfo field = GetType().GetField(dictName, BindingFlags.Public | BindingFlags.Instance);

            if (field != null)
            {
                var dict = field.GetValue(this);
                Type dictType = dict.GetType();

                Type wrapperType = dictType.GetGenericArguments()[1];

                // On récupère le type T de MagicVariableObservable<T>
                Type innerType = wrapperType.IsGenericType &&
                                 wrapperType.GetGenericTypeDefinition() == typeof(MagicVariableObservable<>)
                    ? wrapperType.GetGenericArguments()[0]
                    : wrapperType;

                // Crée une instance de MagicVariableObservable<T>
                object defaultValue = Activator.CreateInstance(
                    typeof(MagicVariableObservable<>).MakeGenericType(innerType)
                );

                MethodInfo addMethod = dictType.GetMethod("Add");
                addMethod.Invoke(dict, new object[] { entry.key, defaultValue });
            }
            else
            {
                Debug.LogWarning($"Dictionnaire de type {entry.type} introuvable dans MagicVariables.");
            }
        }
    }
    
    private object GetDefault(Type t)
    {
        if (t.IsValueType)
            return Activator.CreateInstance(t);
        return null;
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
 
public static class MagicVariablesBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Init()
    {
        if (UnityEngine.Object.FindFirstObjectByType<MagicVariables>() == null)
        {
            GameObject obj = new GameObject("MagicVariables");
            UnityEngine.Object.DontDestroyOnLoad(obj);
            obj.AddComponent<MagicVariables>();
        }
    }
}

*/
#endif