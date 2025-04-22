/*
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Codice.Client.BaseCommands.WkStatus.Printers;
using UnityEditor;
using UnityEditorInternal.Profiling.Memory.Experimental;
using UnityEngine;
using UnityEngine.UIElements;

public class MagicLinkEditor : EditorWindow
{
    private VisualTreeAsset m_VisualTreeAsset;

    private const string FoldersPath = "Assets/CustomFolders";

    private MagicLinkEditorData _data;

    [MenuItem("Window/MagicLinkEditor")]
    public static void ShowExample()
    {
        MagicLinkEditor wnd = GetWindow<MagicLinkEditor>();
        wnd.titleContent = new GUIContent("MagicLinkEditor");
    }

    public void CreateGUI()
    {
        GetDataObject();

        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // Instantiate UXML

        m_VisualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/MagicLinks/Editor/MagicLinkEditor.uxml");
        Debug.Log(m_VisualTreeAsset);

        VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
        root.Add(labelFromUXML);

        //Create folder button
        Button createFolderButton = root.Q<Button>("CreateFolderButton");

        if(createFolderButton != null)
        {
            createFolderButton.clicked += CreateFolderButton_onClick;
        }

        UpdateFolders();

        EditorApplication.projectChanged += OnProjectChanged;

        Debug.Log("Update");
    }

    private void CreateFolderButton_onClick()
    {
        TextField createFolderName = rootVisualElement.Q<TextField>("CreateFolderInput");

        if (createFolderName != null && createFolderName.text != string.Empty)
        {
            DirectoryInfo[] directories = GetFolders();

            foreach(DirectoryInfo d in directories)
            {
                if(d.Name == createFolderName.text)
                {
                    Debug.LogError("Folder with this name already exist");
                    return;
                }
            }

            string newDirectoryPath = Path.Combine(FoldersPath, createFolderName.text);
            Directory.CreateDirectory(newDirectoryPath);
            AssetDatabase.Refresh();
        }
    }

    private void OnProjectChanged()
    {
        UpdateFolders();
        UpdateAssets();
    }

    private void UpdateFolders()
    {
        ScrollView scrollView = rootVisualElement.Q<ScrollView>("FolderList");

        if (scrollView != null)
        {
            scrollView.Clear();
            _data.allFoldersPath.Clear();

            VisualTreeAsset itemUXML = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/MagicLinks/Editor/Folder.uxml");

            DirectoryInfo[] folders = GetFolders();

            for (int x = 0; x < folders.Length; x++)
            {
                //Add folder path
                string path = folders[x].FullName;
                _data.allFoldersPath.Add(path);

                VisualElement newItem = itemUXML.Instantiate();

                newItem.Q<Label>("FolderTitle").text = folders[x].Name;
                scrollView.Add(newItem);

                VisualElement folderRoot = newItem.Q<VisualElement>("Folder");

                int folderIndex = x;

                //Connect the select action
                folderRoot.RegisterCallback<ClickEvent>(e => 
                {
                    SelectFolder(folderIndex);
                });

                //Select the folder if it was selected before
                if(_data.currentFolderSelectedIndex == folderIndex) SelectFolder(folderIndex);

                //Connect the delete folder button
                Button deleteFolderButton = newItem.Q<Button>("DeleteFolderButton");

                if(deleteFolderButton != null)
                {
                    deleteFolderButton.clicked += () => { DeleteFolder(path); };
                }
            }
        }
    }

    private void SelectFolder(int index)
    {
        _data.currentFolderSelectedIndex = index;
        ScrollView scrollView = rootVisualElement.Q<ScrollView>("FolderList");

        List<VisualElement> children = scrollView.Children().ToList();

        for(int x = 0; x < children.Count; x++)
        {
            VisualElement folder = children[x].Q<VisualElement>("Folder");

            if (index == x) folder.AddToClassList("folder-selected");
            else folder.RemoveFromClassList("folder-selected");
        }

        UpdateAssets();
    }

    private DirectoryInfo[] GetFolders()
    {
        List<DirectoryInfo> infos = new List<DirectoryInfo>();
        string[] directories = Directory.GetDirectories(FoldersPath);

        for(int x = 0; x < directories.Length; x++)
        {
            infos.Add(new DirectoryInfo(directories[x]));
        }

        return infos.ToArray();
    }

    private void DeleteFolder(string path)
    {
        Debug.Log(Directory.Exists(path));
        if(Directory.Exists(path))
        {
            Directory.Delete(path, true);

            string metaFilePath = path + ".meta";
            if(File.Exists(metaFilePath))
                File.Delete(metaFilePath);

            AssetDatabase.Refresh();
        }
    }

    //---------------------------------------

    private void UpdateAssets()
    {
        ScrollView scrollView = rootVisualElement.Q<ScrollView>("AssetsList");
        scrollView.Clear();

        string assetsPath = _data.allFoldersPath[_data.currentFolderSelectedIndex];

        if (Directory.Exists(assetsPath))
        {
            VisualTreeAsset itemUXML = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/MagicLinks/Editor/Asset.uxml");

            string[] files = Directory.GetFiles(assetsPath);

            foreach(string file in files)
            {
                if (Path.GetExtension(file) != ".asset") continue;

                Object asset = GetAsset(file);

                if (CheckAssetIsValid(asset) == false) continue;

                VisualElement newAsset = itemUXML.Instantiate();

                //Drag feature
                //DragAndDropManipulator manipulator = new(newAsset);

                string variableType = " (" + asset.GetType().Name.ToString() + ")";

                newAsset.Q<Label>("AssetName").text = Path.GetFileNameWithoutExtension(file) + variableType;
                scrollView.Add(newAsset);
            }
        }
    }

    private Object GetAsset(string path)
    {
        string relativePath = Path.GetRelativePath(Application.dataPath, path).Replace("\\", "/");
        relativePath = "Assets/" + relativePath;
        return AssetDatabase.LoadAssetAtPath<Object>(relativePath);
    }

    private bool CheckAssetIsValid(Object asset)
    {
        if (asset.GetType().BaseType == null) return false;

        return asset.GetType().BaseType.GetCustomAttributes(typeof(MagicLinksMarker), false).Any();
    }

    //--------------------------------------

    private void GetDataObject()
    {
        string path = "Assets/Editor/MagicLinkEditorData.asset";
        _data = AssetDatabase.LoadAssetAtPath<MagicLinkEditorData>(path);

        if (_data == null)
        {
            if(Directory.Exists(Path.GetDirectoryName(path)) == false) Directory.CreateDirectory(Path.GetDirectoryName(path));

            _data = CreateInstance<MagicLinkEditorData>();
            AssetDatabase.CreateAsset(_data, path);
            AssetDatabase.SaveAssets();
        }
    }
}
*/