# Magic Links

Magic Links is a Unity package that exposes variables and events as assets. These "links" live in your project's `Resources/MagicLinks` folder so they can be referenced without a direct script dependency.

## Installing

1. Copy this repository into your project's `Packages` folder or add it as a package reference.
2. Open **Window > MagicLinkEditor** in the Unity editor.

## Creating Variables

Use the **MagicLinkEditor** window to create new links:

1. Enter a variable name in the `name...` field and press **Create Magic Element**.
2. A new JSON file is stored in `Assets/Resources/MagicLinks/Links/`.
3. Choose its type, category and whether it is a variable or event.

Custom types can also be added from the editor. Once a type or variable is created the generator will update the `MagicLinksManager` script and listener scripts under `Assets/Resources/MagicLinks`.

## Script Generation

Scripts are generated from `Editor/Various/MagicVariablesTemplate.cs`.
When you refresh scripts, the generator populates dictionaries for every defined type and event in `MagicLinksManager` and creates listener components.

Generated scripts appear in `Assets/Resources/MagicLinks/`:

- `MagicLinksManager.cs` – contains dictionaries for all magic variables and events.
- `EventsListeners/` and `VariablesListeners/` – listener components for hooking up behaviours in the scene.

## Runtime UI

If **Enable Runtime UI** is checked in the editor window, a prefab from `Runtime/RuntimeLinks.prefab` is instantiated at runtime. This UI lets you view and edit variables live. Use the category filter to show only variables from a given category.

## Example

After creating a variable `PlayerHealth` of type `int` you can access it from code:

```csharp
MagicVariables.INT["PlayerHealth"].Value = 10;
```

Listening for an event named `OnJump` of type `bool` can be done using the generated listener component or directly:

```csharp
MagicEvents.BOOL["OnJump"].OnEventRaised += HandleJump;
```
