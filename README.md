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

## Reset & Iteration

### Reset all variables to their default value

Resets every magic variable to its type default (`0`, `false`, `null`, empty list…). Events are not affected.

```csharp
MagicVariables.ResetAll();
```

### Reset a single variable

```csharp
MagicVariables.ResetVariable("PlayerHealth");
MagicVariables.ResetVariable("Inventory"); // works on list variables too
```

### Reset all variables in a category

Variables are assigned a category in the MagicLinkEditor. You can reset an entire category at once:

```csharp
MagicVariables.ResetCategory("Combat");
```

### Iterate over all variable keys

```csharp
foreach (string key in MagicVariables.GetVariableKeys())
{
    Debug.Log(key);
}
```

### Iterate over variable keys filtered by category

```csharp
foreach (string key in MagicVariables.GetVariableKeysByCategory("Combat"))
{
    Debug.Log(key);
}
```

## Networking

Requires **Netcode for GameObjects 2.x**. Without it, everything stays local. Full guide: [NETWORKING.md](NETWORKING.md).

1. Expand a variable in the MagicLinkEditor and check **Networked** (a `NET` badge appears on the row).
2. Add **Magic Links Network Manager** on your `NetworkManager` (added automatically if missing).

Code using the links does not change. The server validates every write: on a client, the value changes once the server sends it back. Offline, networked links behave like local ones.

When only one player can do something, use `TrySetAsync`:

```csharp
var result = await MagicVariables.INT["ModuleUser"].TrySetAsync(expected: -1, value: myId);
if (result == MagicSetResult.Success) OpenModule();
```

List variables are synchronized operation by operation (`Add`, `Remove`, `SetAt`, `ModifyAt`…): two players adding an item at the same time both get it in the list. Each operation has an `Async` version (`AddAsync`, `SetAtAsync`…) that tells if the server accepted it. Check **Remove items on leave** to remove the items a client added when it disconnects.

`MagicLinksNetwork.LocalClientId` is the id of this machine (0 offline and on the host). `MagicLinksNetwork.OnSessionStarted` is raised once this machine can write.

### Server rules (validators)

A validator is a rule checked where the value is committed (offline, or on the server for every player) before accepting a write. Register it on every machine, at startup:

```csharp
var players = MagicVariables.PLAYERNETSTATE_LIST["PlayersNet"];

players.Validator = (clientId, change, items) =>
{
    if (change.Operation != MagicListOperation.SetAt) return true;
    if (items[change.Index].clientId != clientId) return false;          // a machine only edits its own players
    if (string.IsNullOrEmpty(change.Item.currentInterface)) return true; // closing is always allowed
    return items.Where((p, i) => i != change.Index)
                .All(p => p.currentInterface != change.Item.currentInterface);
};

// Opening an interface
var me = players.Value[myIndex];
me.currentInterface = "Radio";
if (await players.SetAtAsync(myIndex, me) == MagicSetResult.Success) OpenRadio();
```

Variables have the same `Validator` property: `(clientId, current, value) => bool`. On validated lists, prefer `SetAt` to `ModifyAt`: offline, `ModifyAt` changes the item in place and cannot be validated.

### Custom types

A custom type can be networked like any other link. Under the type in the MagicLinkEditor, open **Network fields** and choose a mode for each public field:

- `Auto` (default): sent if possible. `readonly` and `[NonSerialized]` fields are excluded.
- `Value`: sent as is. Works with numbers, strings, enums, vectors, colors, lists, arrays and other custom types.
- `Excluded`: never sent, each player keeps their own value.
- `Reference`: for a `GameObject` or a component with a spawned `NetworkObject`. Each player receives their own instance of that object.

Example:

```csharp
public class ShipStatus
{
    public float oxygen;            // Auto -> Value
    public int hull;                // Auto -> Value
    public List<ItemStack> cargo;   // Auto -> Value (ItemStack is a struct)
    public GameObject pilot;        // Reference: the pilot's player object
    public AudioSource alarm;       // Excluded: local sound only
    public Action onDamaged;        // Excluded: callbacks cannot be sent
}
```

Until `pilot`, `alarm` and `onDamaged` have a mode, the editor shows an error and the link is not synchronized.

To change a value, assign a new one:

```csharp
var ship = MagicVariables.SHIPSTATUS["Ship"].Value;
ship.oxygen -= 5;
MagicVariables.SHIPSTATUS["Ship"].Value = ship;   // sent to everyone
```

Changing `ship.oxygen` without assigning `Value` only changes your local copy. When a client writes, only the changed fields are sent: two players changing `oxygen` and `cargo` at the same time do not overwrite each other.

The type needs a public parameterless constructor (for a class), and only public fields are sent.

## Credits
- Dev [jb106](https://github.com/jb106)
- Icons design [MarineLeBorgne](https://github.com/MarineLeBorgne)
