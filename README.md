# Magic Links

**This tool is designed with UI Toolkit**

Magic Links is a Unity package that exposes variables and events as assets. These "links" live in your project's `Resources/MagicLinks` folder so they can be referenced without a direct script dependency.
<img src="https://github.com/user-attachments/assets/7415d0ef-49a5-46a3-b2d3-0633c53338c4" alt="Description" width="600"/>


## Installing

1. Use **Package Manager > Install Package from Git URL** :  ```https://github.com/jb106/MagicLinks.git```
2. Open **Window > MagicLinkEditor** in the Unity editor.
<img src="https://github.com/user-attachments/assets/a14d197b-8796-487c-8b24-1d170d65a762" alt="image 1" width="300"/>



## Creating Variables

Use the **MagicLinkEditor** window to create new links:

1. Enter a variable name in the `name...` field and press **Create Magic Element**.
2. A new JSON file is stored in `Assets/Resources/MagicLinks/Links/`.
3. Choose its type, category and whether it is a variable or event.

<img src="https://github.com/user-attachments/assets/36e14eef-77c2-460e-9423-429f9bb27fdb" alt="image 2" width="500"/>
</br>
<img src="https://github.com/user-attachments/assets/3a74ccc2-6a20-455b-85a4-da396f6090cb" alt="image 3" width="500"/>

Custom types can also be added from the editor. Once a type or variable is created the generator will update the `MagicLinksManager` script and listener scripts under `Assets/Resources/MagicLinks`.

<img src="https://github.com/user-attachments/assets/fed3c6e8-994a-48f0-8d7a-bb9e11b7541a" alt="image 4" width="200"/>

## Script Generation

Scripts are generated from `Editor/Various/MagicVariablesTemplate.cs`.
When you refresh scripts, the generator populates dictionaries for every defined type and event in `MagicLinksManager` and creates listener components.

Generated scripts appear in `Assets/Resources/MagicLinks/`:

- `MagicLinksManager.cs` – contains dictionaries for all magic variables and events.
- `EventsListeners/` and `VariablesListeners/` – listener components for hooking up behaviours in the scene.

## Runtime UI

If **Enable Runtime UI** is checked in the editor window, a prefab from `Runtime/RuntimeLinks.prefab` is instantiated at runtime. This UI lets you view and edit variables live. Use the category filter to show only variables from a given category.

<img src="https://github.com/user-attachments/assets/7bd9ca3f-f043-4e6c-873a-d61b805ecfd1" alt="image 5" width="200"/>

## Example

After creating a variable `PlayerHealth` of type `int` you can access it from code:

```csharp
MagicVariables.INT["PlayerHealth"].Value = 10;
```

Listening for an event named `OnJump` of type `bool` can be done using the generated listener component or directly:

```csharp
MagicEvents.BOOL["OnJump"].OnEventRaised += HandleJump;
```

## List Modification Methods

SetAt Example — Replace the element at the specified index with a new instance
```csharp
var players = MagicVariables.PLAYER_LIST["Players"];
var newPlayer = new Player {
    Name      = "Alice",
    MoveSpeed = 6.5f,
    Attack    = 12
};
players.SetAt(0, newPlayer);
```

ModifyAt — Modify a specific field of the element at a given index
```csharp
var players = MagicVariables.PLAYER_LIST["Players"];
players.ModifyAt(1, p => {
    p.MoveSpeed += 1.0f; // increase movement speed by 1
});
```

Modify — Find an instance by condition and modify one or more of its fields
```csharp
var players = MagicVariables.PLAYER_LIST["Players"];
players.Modify(
    match:  p => p.Name == "Bob",
    update: p => {
        p.Attack = 20; // set attack value to 20
    }
);
```

## Credits
- Dev [jb106](https://github.com/jb106)
- Icons design [MarineLeBorgne](https://github.com/MarineLeBorgne)
