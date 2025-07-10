#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicLinks.Observables;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

//STARTUSINGEDITOR
using UnityEditor;
//ENDUSINGEDITOR

namespace MagicLinks
{
    [DefaultExecutionOrder(-1000)]
    public class MagicVariablesTemplate : MonoBehaviour
    {
        public static MagicVariablesTemplate Instance;

        private UIDocument _runtimeUI;
        private VisualElement _runtimeContainer;
        private DropdownField _categoryDropdown;
        
        private string _currentCategory;
        private Dictionary<string, List<VisualElement>> _instantiatedLinks = new Dictionary<string, List<VisualElement>>();
        
        //VARIABLESLISTS
        
        public class VariableEntry
        {
            public string key;
            public string type;
            public int magicType;
            public bool isList;

            public VariableEntry(string key, string type, int magicType, bool isList)
            {
                this.key = key;
                this.type = type;
                this.magicType = magicType;
                this.isList = isList;
            }
        }

        private List<VariableEntry> initialVariables = new List<VariableEntry>();
        private void Awake()
        {
            if(Instance != null) Destroy(gameObject);
            
            Instance = this;
            
            
            //STARTUSINGEDITOR
            MagicLinksConfiguration config = MagicLinksUtilities.GetConfiguration();
            
            //Create the runtime UI
            if (config.enableRuntimeUI)
            {
                _runtimeUI = Instantiate(AssetDatabase.LoadAssetAtPath<UIDocument>(
                    MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.RuntimeLinksUIPrefab)), transform);

                _runtimeContainer = _runtimeUI.rootVisualElement.Q<ScrollView>("Container").contentContainer;

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

                // ------- CATEGORIES

                _categoryDropdown = root.Q<DropdownField>("Category");
                _categoryDropdown.choices.Clear();

                _categoryDropdown.choices.Add(MagicLinksConst.CategoryNone);

                foreach (var cName in config.categories)
                {
                    _categoryDropdown.choices.Add(cName);
                }

                _categoryDropdown.value = _categoryDropdown.choices[0];
                _categoryDropdown.RegisterValueChangedCallback(evt =>
                {
                    foreach (var pair in _instantiatedLinks)
                    {
                        foreach (var vElement in pair.Value)
                        {
                            bool isVisible = evt.newValue == MagicLinksConst.CategoryNone || evt.newValue == pair.Key;
                            vElement.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
                        }
                    }
                });
            }
            //ENDUSINGEDITOR
            
            //----------------------------------------------------------
            //----------------------------------------------------------
            //----------------------------------------------------------

            // Load the variables
            List<DynamicVariable> variables = new List<DynamicVariable>();
            foreach (var v in GetExistingVariables())
            {
                initialVariables.Add(new VariableEntry(v.vName, v.vLabelType.ToUpper(), v.magicType, v.isList));
            }

            // Feed the variables
            foreach (var entry in initialVariables)
            {
                if (entry.magicType == 2)
                {
                    /*VOID.Add(entry.key, new MagicEventVoidObservable());*/
                    continue;
                }

                string dictName = GetDictionaryName(entry);
                var field = GetType().GetField(dictName, BindingFlags.Public | BindingFlags.Instance);

                if (field == null)
                {
                    Debug.LogWarning($"Dictionnaire de type {entry.type} introuvable dans MagicVariables.");
                    continue;
                }

                var dict = field.GetValue(this);
                AddEntryToDictionary(entry, dict);
            }


            //STARTUSINGEDITOR
            if (config.enableRuntimeUI)
            {
                InstantiateRuntimeVariables();
            }
            
            MagicLinksUtilities.DisableFocusRecursive(_runtimeContainer);
            //ENDUSINGEDITOR
        }
        
        private string GetDictionaryName(VariableEntry entry)
        {
            string name = entry.type.ToUpper();
            if (entry.isList) name += MagicLinksConst.ListDict;
            if (entry.magicType == 1) name += MagicLinksConst.EventDict;
            return name;
        }
        
        //STARTUSINGEDITOR
        private void InstantiateRuntimeVariables()
        {
            List<(string key, object variable, string type)> mergedList = new();

            void MergeVariables<T>(Dictionary<string, MagicVariableObservable<T>> dict, string typeName)
            {
                foreach (var pair in dict)
                {
                    mergedList.Add((pair.Key, pair.Value, typeName));
                }
            }

            /*
            MergeVariables(STRING, MagicLinksConst.String);
            MergeVariables(BOOL, MagicLinksConst.Bool);
            MergeVariables(INT, MagicLinksConst.Int);
            MergeVariables(FLOAT, MagicLinksConst.Float);
            MergeVariables(VECTOR2, MagicLinksConst.Vector2);
            MergeVariables(VECTOR3, MagicLinksConst.Vector3);
            */
            
            Dictionary<string, string> nameToCategory = new();
            foreach (var v in GetExistingVariables())
            {
                nameToCategory[v.vName] = v.category;
            }

            foreach (var item in mergedList.OrderBy(x => nameToCategory.TryGetValue(x.key, out var cat) ? cat : ""))
            {
                string category = nameToCategory.TryGetValue(item.key, out var c) ? c : "";
                
                switch (item.type)
                {
                    case MagicLinksConst.String:
                        AddLinkToRuntimeUI(new KeyValuePair<string, MagicVariableObservable<string>>(item.key, (MagicVariableObservable<string>)item.variable), item.type, category);
                        break;
                    case MagicLinksConst.Bool:
                        AddLinkToRuntimeUI(new KeyValuePair<string, MagicVariableObservable<bool>>(item.key, (MagicVariableObservable<bool>)item.variable), item.type, category);
                        break;
                    case MagicLinksConst.Int:
                        AddLinkToRuntimeUI(new KeyValuePair<string, MagicVariableObservable<int>>(item.key, (MagicVariableObservable<int>)item.variable), item.type, category);
                        break;
                    case MagicLinksConst.Float:
                        AddLinkToRuntimeUI(new KeyValuePair<string, MagicVariableObservable<float>>(item.key, (MagicVariableObservable<float>)item.variable), item.type, category);
                        break;
                    case MagicLinksConst.Vector2:
                        AddLinkToRuntimeUI(new KeyValuePair<string, MagicVariableObservable<Vector2>>(item.key, (MagicVariableObservable<Vector2>)item.variable), item.type, category);
                        break;
                    case MagicLinksConst.Vector3:
                        AddLinkToRuntimeUI(new KeyValuePair<string, MagicVariableObservable<Vector3>>(item.key, (MagicVariableObservable<Vector3>)item.variable), item.type, category);
                        break;
                }
            }
        }
        
        private void AddLinkToRuntimeUI<T>(KeyValuePair<string, MagicVariableObservable<T>> pair, string t, string category)
        {

            if (category != _currentCategory)
            {
                if (category != string.Empty)
                {
                    VisualTreeAsset headerElement = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                        Path.Combine(MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.UXMLRuntimeLinkHeaderPath)));

                    VisualElement newHeader = headerElement.Instantiate();
                    newHeader.Q<Label>("HeaderName").text = category;
                    _runtimeContainer.Add(newHeader);
                    
                    AddInstantiatedToList(category, newHeader);
                }

                _currentCategory = category;
            }

            VisualTreeAsset linkElement = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                Path.Combine(MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.UXMLRuntimeLinkItemPath)));

            VisualElement newElement = linkElement.Instantiate();
            Label labelTitle = newElement.Q<Label>("LinkName");
            labelTitle.text = pair.Key;

            VisualTreeAsset field = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.GetRuntimeField(t)));
            VisualElement newField = field.Instantiate();
            newField.style.flexGrow = 1;

            void BindField<TField, TValue>(string query, Action<TField, TValue> setValue, Action<TField, EventCallback<ChangeEvent<TValue>>> bindCallback)
                where TField : VisualElement
            {
                var fieldElement = newField.Q<TField>(query);
                MagicVariableObservable<TValue> variable = pair.Value as MagicVariableObservable<TValue>;
                variable.OnValueChanged += v => setValue(fieldElement, v);
                setValue(fieldElement, variable.Value);
                bindCallback(fieldElement, evt => variable.Value = evt.newValue);
            }

            switch (t)
            {
                case MagicLinksConst.String:
                    BindField<TextField, string>("Field", (f, v) => f.SetValueWithoutNotify(v), (f, cb) => f.RegisterValueChangedCallback(cb));
                    break;
                case MagicLinksConst.Bool:
                    BindField<Toggle, bool>("Field", (f, v) => f.SetValueWithoutNotify(v), (f, cb) => f.RegisterValueChangedCallback(cb));
                    break;
                case MagicLinksConst.Int:
                    BindField<IntegerField, int>("Field", (f, v) => f.SetValueWithoutNotify(v), (f, cb) => f.RegisterValueChangedCallback(cb));
                    break;
                case MagicLinksConst.Float:
                    BindField<FloatField, float>("Field", (f, v) => f.SetValueWithoutNotify(v), (f, cb) => f.RegisterValueChangedCallback(cb));
                    break;
                case MagicLinksConst.Vector2:
                    BindField<InlineVector2Field, Vector2>("Field", (f, v) => f.SetValueWithoutNotify(v), (f, cb) => f.RegisterCallback(cb));
                    break;
                case MagicLinksConst.Vector3:
                    BindField<InlineVector3Field, Vector3>("Field", (f, v) => f.SetValueWithoutNotify(v), (f, cb) => f.RegisterCallback(cb));
                    break;
            }

            newElement.Q<VisualElement>("RuntimeLinkItem").Add(newField);
            _runtimeContainer.Add(newElement);
            
            AddInstantiatedToList(category, newElement);
        }

        private void AddInstantiatedToList(string category, VisualElement element)
        {
            if (_instantiatedLinks.ContainsKey(category) ==false)
                _instantiatedLinks.Add(category, new List<VisualElement>());
            
            _instantiatedLinks[category].Add(element);
        }
        //ENDUSINGEDITOR

        private void AddEntryToDictionary(VariableEntry entry, object dict)
        {
            var dictType = dict.GetType();

            var valueWrapperType = GetMagicType(entry.magicType == 1);

            var innerValueType = dictType.GetGenericArguments()[1].GetGenericArguments()[0];

            object initialValue = null;

            if (innerValueType.IsGenericType && innerValueType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = innerValueType.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(elementType);
                initialValue = Activator.CreateInstance(listType); // new List<T>()
            }

            object valueInstance = null;
            if (initialValue != null)
            {
                var ctor = valueWrapperType.MakeGenericType(innerValueType)
                    .GetConstructor(new Type[] { innerValueType });

                if (ctor != null) valueInstance = ctor.Invoke(new object[] { initialValue });
                else valueInstance = Activator.CreateInstance(valueWrapperType.MakeGenericType(innerValueType));
            }
            else
            {
                valueInstance = Activator.CreateInstance(valueWrapperType.MakeGenericType(innerValueType));
            }

            var addMethod = dictType.GetMethod("Add");
            addMethod.Invoke(dict, new object[] { entry.key, valueInstance });
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

namespace MagicLinks
{
    public class #NAME : MonoBehaviour
    {
        [SerializeField]
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
        
        public IEnumerable<string> GetNames()
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

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(#NAME))]
    public class #NAMEEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var listener = (#NAME)target;
            var names = new List<string>(listener.GetNames());
            var referenceProp = serializedObject.FindProperty("reference");

            int index = Mathf.Max(0, names.IndexOf(referenceProp.stringValue));
            if (names.Count > 0)
            {
                index = UnityEditor.EditorGUILayout.Popup("Reference", index, names.ToArray());
                referenceProp.stringValue = names[index];
            }
            else
            {
                referenceProp.stringValue = UnityEditor.EditorGUILayout.TextField("Reference", referenceProp.stringValue);
            }

            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("onEventRaised"));
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif

}

//#SEPARATION
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace MagicLinks
{
    public class Void_EventListener : MonoBehaviour
    {
        [SerializeField]
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
        
        public IEnumerable<string> GetNames()
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

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(Void_EventListener))]
    public class Void_EventListenerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var listener = (Void_EventListener)target;
            var names = new List<string>(listener.GetNames());
            var referenceProp = serializedObject.FindProperty("reference");

            int index = Mathf.Max(0, names.IndexOf(referenceProp.stringValue));
            if (names.Count > 0)
            {
                index = UnityEditor.EditorGUILayout.Popup("Reference", index, names.ToArray());
                referenceProp.stringValue = names[index];
            }
            else
            {
                referenceProp.stringValue = UnityEditor.EditorGUILayout.TextField("Reference", referenceProp.stringValue);
            }

            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("onEventRaised"));
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif

}

*/