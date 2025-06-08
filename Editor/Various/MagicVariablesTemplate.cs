#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MagicLinks.Observables;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MagicLinks
{
    [DefaultExecutionOrder(-1000)]
    public class MagicVariablesTemplate : MonoBehaviour
    {
        public static MagicVariablesTemplate Instance;

        private UIDocument _runtimeUI;
        
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
            
            //Create the runtime UI
            _runtimeUI = Instantiate(AssetDatabase.LoadAssetAtPath<UIDocument>(
                MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.RuntimeLinksUIPrefab)), transform);

            var root = _runtimeUI.rootVisualElement;
            var container = root.Q<VisualElement>("Container");
            var slider = root.Q<Slider>("WindowsSize");

            container.style.transformOrigin = new TransformOrigin(0, 0);

            float initialScale = slider.value;
            container.style.scale = new Scale(new Vector2(initialScale, initialScale));

            slider.RegisterValueChangedCallback(evt =>
            {
                float scale = evt.newValue;
                container.style.scale = new Scale(new Vector2(scale, scale));
            });
            
            //----------------------------------------------------------
            //----------------------------------------------------------
            //----------------------------------------------------------

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

            /*
            foreach (var pair in STRING)
            {
                AddLinkToRuntimeUI(pair, MagicLinksConst.String);
            }
            
            foreach (var pair in BOOL)
            {
                AddLinkToRuntimeUI(pair, MagicLinksConst.Bool);
            }
            
            foreach (var pair in INT)
            {
                AddLinkToRuntimeUI(pair, MagicLinksConst.Int);
            }
            
            foreach (var pair in FLOAT)
            {
                AddLinkToRuntimeUI(pair, MagicLinksConst.Float);
            }
            
            foreach (var pair in VECTOR2)
            {
                AddLinkToRuntimeUI(pair, MagicLinksConst.Vector2);
            }
            
            foreach (var pair in VECTOR3)
            {
                AddLinkToRuntimeUI(pair, MagicLinksConst.Vector3);
            }
            */
        }

        private void AddLinkToRuntimeUI<T>(KeyValuePair<string, MagicVariableObservable<T>> pair, string t)
        {
            VisualTreeAsset linkElement = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Path.Combine(MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.UXMLRuntimeLinkItemPath)));

            VisualElement newElement = linkElement.Instantiate();

            Label labelTitle = newElement.Q<Label>("LinkName");

            labelTitle.text = pair.Key;

            VisualTreeAsset field = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.GetRuntimeField(t)));
            VisualElement newField = field.Instantiate();

            newField.style.flexGrow = 1;
            
            if (t == MagicLinksConst.String)
            {
                TextField stringField = newField.Q<TextField>("Field");
                MagicVariableObservable<string> variable = pair.Value as MagicVariableObservable<string>;
                
                variable.OnValueChanged += v => { stringField.SetValueWithoutNotify(v); };
                
                stringField.RegisterValueChangedCallback((evt => variable.Value = evt.newValue));
            }
            else if (t == MagicLinksConst.Bool)
            {
                Toggle toggle = newField.Q<Toggle>("Field");
                MagicVariableObservable<bool> variable = pair.Value as MagicVariableObservable<bool>;
                
                variable.OnValueChanged += v => { toggle.SetValueWithoutNotify(v); };
                
                toggle.RegisterValueChangedCallback((evt => variable.Value = evt.newValue));
            }
            else if (t == MagicLinksConst.Int)
            {
                IntegerField integer = newField.Q<IntegerField>("Field");
                MagicVariableObservable<int> variable = pair.Value as MagicVariableObservable<int>;
                
                variable.OnValueChanged += v => { integer.SetValueWithoutNotify(v); };
                
                integer.RegisterValueChangedCallback((evt => variable.Value = evt.newValue));
            }
            else if (t == MagicLinksConst.Float)
            {
                FloatField floatField = newField.Q<FloatField>("Field");
                MagicVariableObservable<float> variable = pair.Value as MagicVariableObservable<float>;
                
                variable.OnValueChanged += v => { floatField.SetValueWithoutNotify(v); };
                
                floatField.RegisterValueChangedCallback((evt => variable.Value = evt.newValue));
            }
            else if (t == MagicLinksConst.Vector2)
            {
                InlineVector2Field vector2 = newField.Q<InlineVector2Field>("Field");
                MagicVariableObservable<Vector2> variable = pair.Value as MagicVariableObservable<Vector2>;
                
                variable.OnValueChanged += v => { vector2.SetValueWithoutNotify(v); };

                vector2.RegisterCallback<ChangeEvent<Vector2>>(evt => variable.Value = evt.newValue);
            }
            else if (t == MagicLinksConst.Vector3)
            {
                Vector3Field vector3 = newField.Q<Vector3Field>("Field");
                MagicVariableObservable<Vector3> variable = pair.Value as MagicVariableObservable<Vector3>;
                
                variable.OnValueChanged += v => { vector3.SetValueWithoutNotify(v); };
                
                vector3.RegisterValueChangedCallback((evt => variable.Value = evt.newValue));
            }
            
            newElement.Q<VisualElement>("RuntimeLinkItem").Add(newField);

            _runtimeUI.rootVisualElement.Q<ScrollView>("Container").contentContainer.Add(newElement);
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
                    Debug.LogError($"Magic Link Error : {t.name} is not a valid type - {e.Message}");
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
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace MagicLinks
{
    public class #NAME : MonoBehaviour
    {
        [ValueDropdown("GetNames")]
        public string reference;
        public UnityEvent#ETYPE onEventRaised = new UnityEvent#ETYPE();
        
        [SerializeField, HideInInspector] private MagicLinksConfiguration _configuration;
        
        #if UNITY_EDITOR
        void OnValidate()
        {
            _configuration = MagicLinksUtilities.GetConfiguration();
        }
        #endif

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
        
        private IEnumerable<string> GetNames()
        {
            if (_configuration == null || _configuration.typesNamesPairs == null)
                return new List<string>();
            
            List<string> names = new List<string>();

            foreach (MagicLinkTypeNamePair pair in _configuration.typesNamesPairs)
            {
                if(pair.mlType == "#TYPE") names.Add(pair.mlName);
            }

            return names;
        }
    }
}

//#SEPARATION
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace MagicLinks
{
    public class Void_EventListener : MonoBehaviour
    {
        [ValueDropdown("GetNames")]
        public string reference;
        public UnityEvent onEventRaised = new UnityEvent();
        
        [SerializeField, HideInInspector] private MagicLinksConfiguration _configuration;
        
        #if UNITY_EDITOR
        void OnValidate()
        {
            _configuration = MagicLinksUtilities.GetConfiguration();
        }
        #endif

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
        
        private IEnumerable<string> GetNames()
        {
            if (_configuration == null || _configuration.typesNamesPairs == null)
                return new List<string>();
            
            List<string> names = new List<string>();

            foreach (MagicLinkTypeNamePair pair in _configuration.typesNamesPairs)
            {
                if(pair.mlType == string.Empty) names.Add(pair.mlName);
            }

            return names;
        }
    }
}

*/