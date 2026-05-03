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
        private DropdownField _sortDropdown;
        private DropdownField _magicTypeDropdown;
        private TextField _searchField;

        private string _currentCategory;
        private Dictionary<string, List<VisualElement>> _instantiatedLinks = new Dictionary<string, List<VisualElement>>();
        
        //VARIABLESLISTS
        
        public class VariableEntry
        {
            public string key;
            public string type;
            public string labelType;
            public string initialValueRaw;
            public int magicType;
            public bool isList;
            public string category;
            public string path;

            public VariableEntry(string key, string type, string labelType, string initialValueRaw, int magicType, bool isList, string category, string path)
            {
                this.key = key;
                this.type = type;
                this.labelType = labelType;
                this.initialValueRaw = initialValueRaw;
                this.magicType = magicType;
                this.isList = isList;
                this.category = category;
                this.path = path;
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

                // ------- CATEGORY filter

                _categoryDropdown = root.Q<DropdownField>("Category");
                _categoryDropdown.choices.Clear();
                _categoryDropdown.choices.Add(MagicLinksConst.CategoryNone);
                foreach (var cName in config.categories)
                    _categoryDropdown.choices.Add(cName);
                _categoryDropdown.value = _categoryDropdown.choices[0];
                _categoryDropdown.RegisterValueChangedCallback(_ => RebuildRuntimeList());

                // ------- SORT
                _sortDropdown = root.Q<DropdownField>("Sort");
                if (_sortDropdown != null)
                {
                    if (string.IsNullOrEmpty(_sortDropdown.value)) _sortDropdown.value = MagicLinksConst.SortDefault;
                    _sortDropdown.RegisterValueChangedCallback(_ => RebuildRuntimeList());
                }

                // ------- MAGIC TYPE filter
                _magicTypeDropdown = root.Q<DropdownField>("MagicTypeFilter");
                if (_magicTypeDropdown != null)
                {
                    if (string.IsNullOrEmpty(_magicTypeDropdown.value)) _magicTypeDropdown.value = MagicLinksConst.MagicTypeFilterAll;
                    _magicTypeDropdown.RegisterValueChangedCallback(_ => RebuildRuntimeList());
                }

                // ------- SEARCH
                _searchField = root.Q<TextField>("Search");
                if (_searchField != null)
                {
                    _searchField.RegisterValueChangedCallback(_ => RebuildRuntimeList());
                }
            }
            //ENDUSINGEDITOR
            
            //----------------------------------------------------------
            //----------------------------------------------------------
            //----------------------------------------------------------

            // Load the variables once and reuse
            _cachedExistingVariables = GetExistingVariables();
            foreach (var v in _cachedExistingVariables)
            {
                initialVariables.Add(new VariableEntry(v.vName, v.vLabelType.ToUpper(), v.vLabelType, v.initialValue, v.magicType, v.isList, v.category, v.vPath));
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
                PreloadRuntimeVTAs();
                RebuildRuntimeList();
            }
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

        private static readonly HashSet<string> _runtimeBaseTypes = new HashSet<string>
        {
            MagicLinksConst.String, MagicLinksConst.Bool, MagicLinksConst.Int,
            MagicLinksConst.Float, MagicLinksConst.Vector2, MagicLinksConst.Vector3,
        };

        private void PreloadRuntimeVTAs()
        {
            _runtimeHeaderVTA = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.UXMLRuntimeLinkHeaderPath));
            _runtimeLinkVTA = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.UXMLRuntimeLinkItemPath));
        }

        private void RebuildRuntimeList()
        {
            if (_runtimeContainer == null) return;

            _runtimeContainer.Clear();
            _instantiatedLinks.Clear();
            _currentCategory = null;

            // Read filter/sort values
            string search = (_searchField != null ? _searchField.value : string.Empty)?.Trim() ?? string.Empty;
            string sort = _sortDropdown != null && !string.IsNullOrEmpty(_sortDropdown.value)
                ? _sortDropdown.value
                : MagicLinksConst.SortDefault;
            string magicTypeFilter = _magicTypeDropdown != null && !string.IsNullOrEmpty(_magicTypeDropdown.value)
                ? _magicTypeDropdown.value
                : MagicLinksConst.MagicTypeFilterAll;
            string categoryFilter = _categoryDropdown != null && !string.IsNullOrEmpty(_categoryDropdown.value)
                ? _categoryDropdown.value
                : MagicLinksConst.CategoryNone;

            // Filter
            IEnumerable<VariableEntry> filtered = initialVariables;
            filtered = filtered.Where(e =>
                !e.isList &&
                (e.magicType == 2 || _runtimeBaseTypes.Contains(e.labelType)));

            if (!string.IsNullOrEmpty(search))
                filtered = filtered.Where(e => !string.IsNullOrEmpty(e.key)
                    && e.key.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0);

            if (magicTypeFilter != MagicLinksConst.MagicTypeFilterAll)
            {
                int wantedMagicType = magicTypeFilter switch
                {
                    MagicLinksConst.MagicTypeEvent => 1,
                    MagicLinksConst.MagicTypeEventVoid => 2,
                    _ => 0,
                };
                filtered = filtered.Where(e => e.magicType == wantedMagicType);
            }

            if (categoryFilter != MagicLinksConst.CategoryNone)
                filtered = filtered.Where(e => e.category == categoryFilter);

            // Sort
            List<VariableEntry> sorted = (sort switch
            {
                MagicLinksConst.SortNameAsc => filtered.OrderBy(e => e.key, System.StringComparer.OrdinalIgnoreCase),
                MagicLinksConst.SortNameDesc => filtered.OrderByDescending(e => e.key, System.StringComparer.OrdinalIgnoreCase),
                MagicLinksConst.SortNewest => filtered.OrderByDescending(e => SafeGetCreationTime(e.path)),
                MagicLinksConst.SortOldest => filtered.OrderBy(e => SafeGetCreationTime(e.path)),
                _ => filtered.OrderBy(e => string.IsNullOrEmpty(e.category) ? MagicLinksConst.CategoryNone : e.category, System.StringComparer.OrdinalIgnoreCase),
            }).ToList();

            // Lazy category headers — only when sort=Default and no category filter active.
            bool showCategoryHeaders = sort == MagicLinksConst.SortDefault
                                       && categoryFilter == MagicLinksConst.CategoryNone;
            HashSet<string> visibleCategories = null;
            if (showCategoryHeaders)
            {
                visibleCategories = new HashSet<string>();
                foreach (var e in sorted)
                    visibleCategories.Add(string.IsNullOrEmpty(e.category) ? MagicLinksConst.CategoryNone : e.category);
            }

            string lastCategory = null;
            foreach (var entry in sorted)
            {
                string categoryKey = string.IsNullOrEmpty(entry.category) ? MagicLinksConst.CategoryNone : entry.category;
                if (showCategoryHeaders && categoryKey != lastCategory && visibleCategories.Contains(categoryKey))
                {
                    AddCategoryHeader(categoryKey);
                    lastCategory = categoryKey;
                }
                else if (!showCategoryHeaders)
                {
                    lastCategory = categoryKey;
                }

                switch (entry.magicType)
                {
                    case 0: DispatchVariable(entry, categoryKey); break;
                    case 1: DispatchEvent(entry, categoryKey); break;
                    case 2: DispatchVoidEvent(entry, categoryKey); break;
                }
            }
        }

        private static System.DateTime SafeGetCreationTime(string path)
        {
            try { return string.IsNullOrEmpty(path) ? System.DateTime.MinValue : System.IO.File.GetCreationTimeUtc(path); }
            catch { return System.DateTime.MinValue; }
        }

        private void AddCategoryHeader(string category)
        {
            VisualElement newHeader = _runtimeHeaderVTA.Instantiate();
            newHeader.Q<Label>("HeaderName").text = category;
            _runtimeContainer.Add(newHeader);
            AddInstantiatedToList(category, newHeader);
            _currentCategory = category;
        }

        private object ResolveObservable(VariableEntry entry)
        {
            string dictName = GetDictionaryName(entry);
            var dictField = GetCachedDictField(dictName);
            if (dictField == null) return null;
            var dict = dictField.GetValue(this);
            if (dict == null) return null;

            var dictType = dict.GetType();
            if (!_tryGetValueCache.TryGetValue(dictType, out var tryGet))
            {
                tryGet = dictType.GetMethod("TryGetValue");
                _tryGetValueCache[dictType] = tryGet;
            }
            object[] args = new object[] { entry.key, null };
            if (!(bool)tryGet.Invoke(dict, args)) return null;
            return args[1];
        }

        private void DispatchVariable(VariableEntry entry, string category)
        {
            object obs = ResolveObservable(entry);
            if (obs == null) return;

            switch (entry.labelType)
            {
                case MagicLinksConst.String:
                    AddVariableLinkRuntime(entry.key, (MagicVariableObservable<string>)obs, entry.labelType, category); break;
                case MagicLinksConst.Bool:
                    AddVariableLinkRuntime(entry.key, (MagicVariableObservable<bool>)obs, entry.labelType, category); break;
                case MagicLinksConst.Int:
                    AddVariableLinkRuntime(entry.key, (MagicVariableObservable<int>)obs, entry.labelType, category); break;
                case MagicLinksConst.Float:
                    AddVariableLinkRuntime(entry.key, (MagicVariableObservable<float>)obs, entry.labelType, category); break;
                case MagicLinksConst.Vector2:
                    AddVariableLinkRuntime(entry.key, (MagicVariableObservable<Vector2>)obs, entry.labelType, category); break;
                case MagicLinksConst.Vector3:
                    AddVariableLinkRuntime(entry.key, (MagicVariableObservable<Vector3>)obs, entry.labelType, category); break;
            }
        }

        private void DispatchEvent(VariableEntry entry, string category)
        {
            object obs = ResolveObservable(entry);
            if (obs == null) return;

            switch (entry.labelType)
            {
                case MagicLinksConst.String:
                    AddEventLinkRuntime(entry.key, (MagicEventObservable<string>)obs, entry.labelType, category); break;
                case MagicLinksConst.Bool:
                    AddEventLinkRuntime(entry.key, (MagicEventObservable<bool>)obs, entry.labelType, category); break;
                case MagicLinksConst.Int:
                    AddEventLinkRuntime(entry.key, (MagicEventObservable<int>)obs, entry.labelType, category); break;
                case MagicLinksConst.Float:
                    AddEventLinkRuntime(entry.key, (MagicEventObservable<float>)obs, entry.labelType, category); break;
                case MagicLinksConst.Vector2:
                    AddEventLinkRuntime(entry.key, (MagicEventObservable<Vector2>)obs, entry.labelType, category); break;
                case MagicLinksConst.Vector3:
                    AddEventLinkRuntime(entry.key, (MagicEventObservable<Vector3>)obs, entry.labelType, category); break;
            }
        }

        private void DispatchVoidEvent(VariableEntry entry, string category)
        {
            // VOID dict is generated; resolve via reflection to keep the template compiling.
            var dictField = GetCachedDictField("VOID");
            if (dictField == null) return;
            var dict = dictField.GetValue(this);
            if (dict == null) return;

            var dictType = dict.GetType();
            if (!_tryGetValueCache.TryGetValue(dictType, out var tryGet))
            {
                tryGet = dictType.GetMethod("TryGetValue");
                _tryGetValueCache[dictType] = tryGet;
            }
            object[] args = new object[] { entry.key, null };
            if (!(bool)tryGet.Invoke(dict, args)) return;

            AddVoidEventLinkRuntime(entry.key, (MagicEventVoidObservable)args[1], category);
        }

        private VisualElement BuildFieldVisual(string typeLabel)
        {
            if (!_runtimeFieldVTACache.TryGetValue(typeLabel, out var fieldVTA))
            {
                fieldVTA = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.GetRuntimeField(typeLabel)));
                _runtimeFieldVTACache[typeLabel] = fieldVTA;
            }
            VisualElement newField = fieldVTA.Instantiate();
            newField.style.flexGrow = 1;
            return newField;
        }

        private void AddVariableLinkRuntime<T>(string key, MagicVariableObservable<T> variable, string typeLabel, string category)
        {
            VisualElement newElement = _runtimeLinkVTA.Instantiate();
            newElement.Q<Label>("LinkName").text = key;

            VisualElement newField = BuildFieldVisual(typeLabel);
            BindVariableField(newField, variable, typeLabel);

            newElement.Q<VisualElement>("RuntimeLinkItem").Add(newField);
            _runtimeContainer.Add(newElement);
            AddInstantiatedToList(category, newElement);
        }

        private void BindVariableField<T>(VisualElement newField, MagicVariableObservable<T> variable, string typeLabel)
        {
            void Bind<TField, TValue>(string query, Action<TField, TValue> setValue,
                Action<TField, EventCallback<ChangeEvent<TValue>>> bindCallback)
                where TField : VisualElement
            {
                var f = newField.Q<TField>(query);
                var v = variable as MagicVariableObservable<TValue>;
                v.OnValueChanged += val => setValue(f, val);
                setValue(f, v.Value);
                bindCallback(f, evt => v.Value = evt.newValue);
            }

            switch (typeLabel)
            {
                case MagicLinksConst.String:
                    Bind<TextField, string>("Field", (f, v) => f.SetValueWithoutNotify(v), (f, cb) => f.RegisterValueChangedCallback(cb)); break;
                case MagicLinksConst.Bool:
                    Bind<Toggle, bool>("Field", (f, v) => f.SetValueWithoutNotify(v), (f, cb) => f.RegisterValueChangedCallback(cb)); break;
                case MagicLinksConst.Int:
                    Bind<IntegerField, int>("Field", (f, v) => f.SetValueWithoutNotify(v), (f, cb) => f.RegisterValueChangedCallback(cb)); break;
                case MagicLinksConst.Float:
                    Bind<FloatField, float>("Field", (f, v) => f.SetValueWithoutNotify(v), (f, cb) => f.RegisterValueChangedCallback(cb)); break;
                case MagicLinksConst.Vector2:
                    Bind<InlineVector2Field, Vector2>("Field", (f, v) => f.SetValueWithoutNotify(v), (f, cb) => f.RegisterCallback(cb)); break;
                case MagicLinksConst.Vector3:
                    Bind<InlineVector3Field, Vector3>("Field", (f, v) => f.SetValueWithoutNotify(v), (f, cb) => f.RegisterCallback(cb)); break;
            }
        }

        private void AddEventLinkRuntime<T>(string key, MagicEventObservable<T> ev, string typeLabel, string category)
        {
            VisualElement newElement = _runtimeLinkVTA.Instantiate();
            VisualElement row = newElement.Q<VisualElement>("RuntimeLinkItem");
            row.AddToClassList("eventRow");

            Label nameLabel = newElement.Q<Label>("LinkName");
            nameLabel.text = key;

            VisualElement newField = BuildFieldVisual(typeLabel);
            Func<T> getValue = BuildEventValueGetter<T>(newField, typeLabel);

            Button raiseBtn = new Button(() =>
            {
                if (getValue == null) return;
                ev.Raise(getValue());
                FlashRaise(row);
            }) { text = "Raise" };
            raiseBtn.AddToClassList("raiseButton");

            row.Add(newField);
            row.Add(raiseBtn);
            _runtimeContainer.Add(newElement);
            AddInstantiatedToList(category, newElement);
        }

        private static Func<T> BuildEventValueGetter<T>(VisualElement newField, string typeLabel)
        {
            switch (typeLabel)
            {
                case MagicLinksConst.String:
                    var sf = newField.Q<TextField>("Field");
                    return () => (T)(object)sf.value;
                case MagicLinksConst.Bool:
                    var bf = newField.Q<Toggle>("Field");
                    return () => (T)(object)bf.value;
                case MagicLinksConst.Int:
                    var inf = newField.Q<IntegerField>("Field");
                    return () => (T)(object)inf.value;
                case MagicLinksConst.Float:
                    var ff = newField.Q<FloatField>("Field");
                    return () => (T)(object)ff.value;
                case MagicLinksConst.Vector2:
                    var v2 = newField.Q<InlineVector2Field>("Field");
                    return () => (T)(object)v2.value;
                case MagicLinksConst.Vector3:
                    var v3 = newField.Q<InlineVector3Field>("Field");
                    return () => (T)(object)v3.value;
            }
            return null;
        }

        private void AddVoidEventLinkRuntime(string key, MagicEventVoidObservable ev, string category)
        {
            VisualElement newElement = _runtimeLinkVTA.Instantiate();
            VisualElement row = newElement.Q<VisualElement>("RuntimeLinkItem");
            row.AddToClassList("eventRow");
            row.AddToClassList("voidEventRow");

            newElement.Q<Label>("LinkName").text = key;

            Button raiseBtn = new Button(() => { ev.Raise(); FlashRaise(row); }) { text = "Raise" };
            raiseBtn.AddToClassList("raiseButton");
            raiseBtn.style.flexGrow = 1;

            row.Add(raiseBtn);
            _runtimeContainer.Add(newElement);
            AddInstantiatedToList(category, newElement);
        }

        private static void FlashRaise(VisualElement row)
        {
            row.AddToClassList("eventRowFlash");
            row.schedule.Execute(() => row.RemoveFromClassList("eventRowFlash")).StartingIn(180);
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
            else if (entry.magicType == 0
                     && MagicLinksInitialValue.IsSupported(entry.labelType, entry.isList, entry.magicType)
                     && MagicLinksInitialValue.TryParse(entry.labelType, entry.initialValueRaw, out var parsed)
                     && parsed != null
                     && innerValueType.IsInstanceOfType(parsed))
            {
                initialValue = parsed;
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