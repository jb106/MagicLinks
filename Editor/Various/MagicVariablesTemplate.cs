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
            public string category;

            public VariableEntry(string key, string type, int magicType, bool isList, string category)
            {
                this.key = key;
                this.type = type;
                this.magicType = magicType;
                this.isList = isList;
                this.category = category;
            }
        }

        private List<VariableEntry> initialVariables = new List<VariableEntry>();

        private readonly Dictionary<string, FieldInfo> _dictFieldCache = new Dictionary<string, FieldInfo>();
        private readonly Dictionary<Type, MethodInfo> _tryGetValueCache = new Dictionary<Type, MethodInfo>();
        private readonly Dictionary<Type, MethodInfo> _resetMethodCache = new Dictionary<Type, MethodInfo>();
        private readonly Dictionary<Type, MethodInfo> _addMethodCache = new Dictionary<Type, MethodInfo>();

        private List<DynamicVariable> _cachedExistingVariables;
        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

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

            // Load the variables once and reuse
            _cachedExistingVariables = GetExistingVariables();
            foreach (var v in _cachedExistingVariables)
            {
                initialVariables.Add(new VariableEntry(v.vName, v.vLabelType.ToUpper(), v.magicType, v.isList, v.category));
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
                var field = GetCachedDictField(dictName);

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

        private FieldInfo GetCachedDictField(string dictName)
        {
            if (_dictFieldCache.TryGetValue(dictName, out var cached)) return cached;
            var field = GetType().GetField(dictName, BindingFlags.Public | BindingFlags.Instance);
            _dictFieldCache[dictName] = field;
            return field;
        }

        public IEnumerable<string> GetVariableKeys()
        {
            foreach (var entry in initialVariables)
                if (entry.magicType == 0) yield return entry.key;
        }

        public IEnumerable<string> GetVariableKeysByCategory(string category)
        {
            foreach (var entry in initialVariables)
                if (entry.magicType == 0 && entry.category == category) yield return entry.key;
        }

        public void ResetAll()
        {
            foreach (var entry in initialVariables)
            {
                if (entry.magicType != 0) continue;
                ResetEntry(entry);
            }
        }

        public void ResetCategory(string category)
        {
            foreach (var entry in initialVariables)
            {
                if (entry.magicType != 0 || entry.category != category) continue;
                ResetEntry(entry);
            }
        }

        public void ResetVariable(string key)
        {
            var entry = initialVariables.Find(e => e.key == key && e.magicType == 0);
            if (entry == null)
            {
                Debug.LogWarning($"[MagicLinks] Variable '{key}' not found.");
                return;
            }
            ResetEntry(entry);
        }

        private void ResetEntry(VariableEntry entry)
        {
            string dictName = GetDictionaryName(entry);
            var field = GetCachedDictField(dictName);
            if (field == null) return;

            var dict = field.GetValue(this);
            var dictType = dict.GetType();

            if (!_tryGetValueCache.TryGetValue(dictType, out var tryGet))
            {
                tryGet = dictType.GetMethod("TryGetValue");
                _tryGetValueCache[dictType] = tryGet;
            }

            object[] args = new object[] { entry.key, null };
            bool found = (bool)tryGet.Invoke(dict, args);
            if (!found || args[1] == null) return;

            var valueType = args[1].GetType();
            if (!_resetMethodCache.TryGetValue(valueType, out var resetMethod))
            {
                resetMethod = valueType.GetMethod("Reset");
                _resetMethodCache[valueType] = resetMethod;
            }

            resetMethod?.Invoke(args[1], null);
        }

        //STARTUSINGEDITOR
        private VisualTreeAsset _runtimeHeaderVTA;
        private VisualTreeAsset _runtimeLinkVTA;
        private readonly Dictionary<string, VisualTreeAsset> _runtimeFieldVTACache = new Dictionary<string, VisualTreeAsset>();

        private void InstantiateRuntimeVariables()
        {
            // Pre-load VTAs once instead of per-link
            _runtimeHeaderVTA = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.UXMLRuntimeLinkHeaderPath));
            _runtimeLinkVTA = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.UXMLRuntimeLinkItemPath));

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
            // Reuse the list already loaded in Awake instead of re-reading from Resources
            var sourceVars = _cachedExistingVariables ?? GetExistingVariables();
            foreach (var v in sourceVars)
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
                    VisualElement newHeader = _runtimeHeaderVTA.Instantiate();
                    newHeader.Q<Label>("HeaderName").text = category;
                    _runtimeContainer.Add(newHeader);

                    AddInstantiatedToList(category, newHeader);
                }

                _currentCategory = category;
            }

            VisualElement newElement = _runtimeLinkVTA.Instantiate();
            Label labelTitle = newElement.Q<Label>("LinkName");
            labelTitle.text = pair.Key;

            if (!_runtimeFieldVTACache.TryGetValue(t, out var field))
            {
                field = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.GetRuntimeField(t)));
                _runtimeFieldVTACache[t] = field;
            }
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

            var valueWrapperType = GetMagicType(entry.magicType == 1, entry.isList);

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

            if (!_addMethodCache.TryGetValue(dictType, out var addMethod))
            {
                addMethod = dictType.GetMethod("Add");
                _addMethodCache[dictType] = addMethod;
            }
            addMethod.Invoke(dict, new object[] { entry.key, valueInstance });
        }
        
        private Type GetMagicType(bool isEvent, bool isList)
        {
            if (isEvent)
                return typeof(MagicEventObservable<>);
            else
                return isList ? typeof(MagicListVariableObservable<>) : typeof(MagicVariableObservable<>);
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

        public static IEnumerable<string> GetVariableKeys() => MagicLinksManager.Instance.GetVariableKeys();
        public static IEnumerable<string> GetVariableKeysByCategory(string category) => MagicLinksManager.Instance.GetVariableKeysByCategory(category);
        public static void ResetAll() => MagicLinksManager.Instance.ResetAll();
        public static void ResetCategory(string category) => MagicLinksManager.Instance.ResetCategory(category);
        public static void ResetVariable(string key) => MagicLinksManager.Instance.ResetVariable(key);
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