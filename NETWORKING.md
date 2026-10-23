# Magic Links – Networking guide

This guide covers everything about synchronizing magic links in multiplayer with **Netcode for GameObjects (NGO)**.
The short version is in the README.

- [Overview](#overview)
- [Setup](#setup)
- [How it works](#how-it-works)
- [Variables](#variables)
- [Events](#events)
- [Lists](#lists)
- [When several players act at the same time](#when-several-players-act-at-the-same-time)
- [Server rules (validators)](#server-rules-validators)
- [Session and disconnections](#session-and-disconnections)
- [Custom types](#custom-types)
- [Per-player data (PlayerData pattern)](#per-player-data-playerdata-pattern)
- [What not to synchronize](#what-not-to-synchronize)
- [Generated code and build](#generated-code-and-build)
- [Troubleshooting](#troubleshooting)

---

## Overview

Each link (variable, list or event) can be marked as **Networked**. Code using it does not change:

```csharp
MagicVariables.BOOL["DoorOpen"].Value = true;            // any player
MagicVariables.BOOL["DoorOpen"].OnValueChanged += Open;   // raised on every player
MagicEvents.VECTOR3["Explosion"].Raise(position);        // received once by every player
```

- **Offline** (no session started), a networked link behaves exactly like a local one.
- **Without NGO installed**, nothing network related is compiled and every link stays local.
- The **server** (or host) is the only one that commits values. Everything else is a request.

---

## Setup

1. Install **Netcode for GameObjects 2.x**. The package adds the `MAGICLINKS_NETCODE` scripting define automatically
   (and removes it if NGO is uninstalled).
2. Put a `NetworkManager` in your boot scene.
3. Add **MagicLinks/Magic Links Network Manager** on the same GameObject. If you forget, it is added automatically to
   `NetworkManager.Singleton` when a scene loads, as soon as one link is networked.
4. The `MagicLinksManager` must exist when the session starts and stay alive during the whole session.
5. In the MagicLinkEditor, expand a variable (arrow on the left of its row) and check **Networked**.
   A `NET` badge appears on the row. A red `!` badge means the link has errors: hover it to see them.

---

## How it works

- Every write is sent to the server, which applies writes **one at a time**, in the order it receives them, then
  sends the result to every player.
- Everything goes through **one reliable ordered channel**: every player receives the changes in the exact order the
  server applied them (states, list changes, events and answers to requests).
- On a **client**, `Value = x` does not change the local value immediately: it changes when the server's value comes
  back (one round trip). On the **host** and **offline**, it is immediate.
- A player joining during the game receives the current value of every networked variable and list.
- A client built with different networked links than the server is refused at connection with an explicit error.

---

## Variables

```csharp
var door = MagicVariables.BOOL["DoorOpen"];

door.Value = true;                        // request, the value changes when the server confirms
await door.SetAsync(true);                // same, but completes once the value is applied locally
await door.TrySetAsync(false, true);      // only if the server's value is still false (see below)
```

`SetAsync` and `TrySetAsync` return a `MagicSetResult`:

| Result | Meaning |
|---|---|
| `Success` | The server applied the value, and it is already visible locally. |
| `Rejected` | The server refused (condition not met, validator refused, write filter). |
| `Disconnected` | The session ended before the server answered. Nothing can be assumed. |

**Reset on leave**: when the client who last wrote the variable disconnects, the server resets it to its value at
session start. Useful for variables used as locks.

---

## Events

```csharp
MagicEvents.STRING["Chat"].Raise("Hello");
MagicEvents.VOID["Alarm"].Raise();
```

- Every raise reaches every player **exactly once**, in the same order everywhere.
- The player who raises it **does not** receive it immediately: the server sends it back to everyone, including them.
  That is what guarantees "once, in order" for everyone.

---

## Lists

List variables are synchronized **operation by operation**: `Add`, `Remove`, `RemoveAt`, `SetAt`, `ModifyAt`,
`Modify`, `Clear`. Two players adding an item at the same time both get their item in the list.

Each operation has an `Async` version that tells if the server accepted it:

```csharp
var inventory = MagicVariables.INT_LIST["Inventory"];

inventory.Add(3);
MagicSetResult result = await inventory.SetAtAsync(0, 5);
int index = inventory.FindIndex(i => i == 5);
```

- An operation carries the item the player saw at that index. If the list changed in the meantime, the server finds
  the item again instead of touching the wrong one. If the item is gone, the operation is rejected.
- `ModifyAt` on a client modifies a **copy**: the local item only changes once the server accepts it.
- `OnItemAdded`, `OnItemRemoved`, `OnItemChanged`, `OnCleared` are raised on every player, like offline.
- **Remove items on leave**: when a client disconnects, the server removes the items **that client added**.
  Ideal for one entry per player.
- List **events** (`MagicEvents.INT_LIST`) are sent as a whole list.

---

## When several players act at the same time

Never check then write:

```csharp
// WRONG: two players can both see -1 and both take the module
if (module.Value == -1) module.Value = myId;
```

Use `TrySetAsync`: the check and the write happen together on the server, exactly one player succeeds.

```csharp
var module = MagicVariables.INT["Module_User"];   // initial value -1, Networked, Reset on leave

if (await module.TrySetAsync(expected: -1, value: (int)MagicLinksNetwork.LocalClientId) == MagicSetResult.Success)
    OpenModule();
else
    ShowMessage("Module already in use");

// When closing
await module.TrySetAsync(expected: (int)MagicLinksNetwork.LocalClientId, value: -1);
```

For rules that involve several values (for example "nobody else has this interface open" in a list of players),
use a validator.

---

## Server rules (validators)

A validator is a rule checked **where the value is committed** before accepting a write: offline locally, or on the
server for the writes of every player (the host included). A refused write is ignored, and the `Async` versions
return `Rejected`. If a validator throws, the write is refused and the exception is logged.

Register validators **on every machine at startup** (only the server uses them, but any machine can become host).

**Variables**: `(clientId, current, value) => bool`

```csharp
MagicVariables.INT["TeamScore"].Validator = (clientId, current, value) => value >= current;   // score never goes down
```

**Lists**: `(clientId, change, items) => bool`

- `change.Operation`: `Add`, `RemoveAt`, `SetAt` or `Clear`
- `change.Index`: affected index (for `Add`, the index the item will have)
- `change.Item`: new item (`Add`, `SetAt`), removed item (`RemoveAt`)
- `items`: the list before the change

```csharp
players.Validator = (clientId, change, items) =>
{
    if (change.Operation != MagicListOperation.SetAt) return true;
    if (items[change.Index].clientId != clientId) return false;          // a machine only edits its own players
    if (string.IsNullOrEmpty(change.Item.currentInterface)) return true; // closing is always allowed
    return items.Where((p, i) => i != change.Index)
                .All(p => p.currentInterface != change.Item.currentInterface);
};
```

Keep your local checks too (to grey out a button immediately), but the validator has the last word.

On validated lists, prefer `SetAt` to `ModifyAt`: offline, `ModifyAt` changes the item in place and cannot be
validated. In a session, `ModifyAt` is validated like a `SetAt`.

`MagicLinksNetworkManager.ClientWriteFilter` is a coarser alternative: `(clientId, linkName) => bool`, checked on the
server for every write coming from a client.

---

## Session and disconnections

| API | Description |
|---|---|
| `MagicLinksNetwork.IsOnline` | True during a session. |
| `MagicLinksNetwork.IsAuthority` | True offline or on the server/host. |
| `MagicLinksNetwork.LocalClientId` | Id of this machine (0 offline and on the host). |
| `MagicLinksNetwork.OnSessionStarted` | This machine can write: at start on the server, once connected and synchronized on a client. |
| `MagicLinksNetwork.OnSessionEnded` | The session stopped, for any reason. Parameter: disconnect reason. |

- An awaiting `SetAsync`, `TrySetAsync` or list `Async` call **always** completes: with `Disconnected` if the session
  ends first. No timeout: as long as the connection is alive, the server always answers.
- While a client is still connecting, writes and events are ignored with a warning (`Async` calls return
  `Disconnected`) instead of being applied locally and silently replaced by the server's state.
- When the session ends, networked links keep their last value and become local again.
- When a client disconnects, **Reset on leave** variables it wrote are reset and **Remove items on leave** lists lose
  the items it added.

---

## Custom types

A custom type (class or struct) can be networked like any other link.

**Where**: left panel of the MagicLinkEditor > **Custom Types** > under the type, **Network fields**.
This foldout only appears for types that have **public fields** (properties are ignored).

For each public field, choose a mode:

| Mode | Meaning |
|---|---|
| `Auto` (default) | Sent if possible. `readonly` and `[NonSerialized]` fields are excluded. |
| `Value` | Sent as is: numbers, strings, enums, vectors, colors, lists, arrays, other custom types, `INetworkSerializable`. |
| `Excluded` | Never sent: each player keeps their own value. |
| `Reference` | A `GameObject` or component with a spawned `NetworkObject`. Each player receives their own instance of it. |

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

Fields that cannot be sent without a choice (scene objects, callbacks, dictionaries, interfaces...) are reported in
the editor. A networked link with errors is **not synchronized at all**, the errors are logged when the session
starts, and the build fails.

Rules:

- The type needs a public parameterless constructor (for a class) and must be public.
- Assign a new value to notify a change: `link.Value.oxygen = 3` only changes your local copy.
  Assigning the same instance after modifying it sends it in full.
- When a client writes, only the fields that changed are sent: two players changing `oxygen` and `cargo` at the same
  time do not overwrite each other. On the same field, the last write received by the server wins.
- Prefer **structs** for small data: no reference identity problems, field by field comparison.
- A received value is a **new instance**. Do not network a type that other scripts keep references to
  (see the PlayerData pattern below).

---

## Per-player data (PlayerData pattern)

Do not network a big "hub" class like a `PlayerData` holding cameras, inputs and callbacks: a received value is a new
instance, which would break every reference to it. Keep it **local** on each machine, with one entry per connected
player, and network a small struct next to it.

### 1. The shared state

```csharp
public struct PlayerNetState
{
    public ulong clientId;        // the machine
    public int localIndex;        // the player on that machine (several local players per machine)
    public int characterIndex;
    public PlayerPresence presence;
    public string currentInterface;
    public bool isFainted;
}
```

In the MagicLinkEditor: add the custom type `PlayerNetState`, then a variable `PlayersNet` of that type with
**List**, **Networked** and **Remove items on leave**. Register the validator shown above.

### 2. The player object (NGO)

- Player prefab with a `NetworkObject` and a `NetworkTransform` (Owner authority), registered in the network prefabs.
- NGO spawns one player object per machine automatically. For several local players, each machine asks the server to
  spawn one object per local player with `SpawnWithOwnership(clientId)`.

### 3. Building PlayerData on every machine

In the player object's `OnNetworkSpawn`, create its `PlayerData` and add it to your local list of players:

- `playerTransform` = this object, so every system (creatures included) gets every player's position;
- `clientId` = `OwnerClientId`, `playerIndex` = local index;
- `IsOwner`: one of your players (camera, inputs, UI); otherwise a remote player (model and data only).

In `OnNetworkDespawn`, remove it from the list. The server removes its `PlayersNet` line automatically.

### 4. Linking PlayerData to its shared state

Once `MagicLinksNetwork.OnSessionStarted` is raised and the object is spawned, each machine adds one line per local
player:

```csharp
await players.AddAsync(new PlayerNetState { clientId = MagicLinksNetwork.LocalClientId, localIndex = i });
```

Then give `PlayerData` access to its line. Lines are identified by `clientId` + `localIndex`, not by position
(positions change when someone leaves):

```csharp
static MagicListVariableObservable<PlayerNetState> Players => MagicVariables.PLAYERNETSTATE_LIST["PlayersNet"];

int NetIndex => Players.FindIndex(p => p.clientId == clientId && p.localIndex == playerIndex);

public PlayerNetState Net => Players.Value[NetIndex];
public Task<MagicSetResult> SetNet(PlayerNetState state) => Players.SetAtAsync(NetIndex, state);

// Existing conditions keep working
public string currentInterfaceOpened => Net.currentInterface;
public bool isFainted => Net.isFainted;
```

Writing goes through `SetNet`, which is where the server decides:

```csharp
var state = playerData.Net;
state.currentInterface = "Radio";
if (await playerData.SetNet(state) == MagicSetResult.Success) OpenRadio();
```

Subscribe to `PlayersNet.OnItemChanged` to update what you show of remote players.

---

## What not to synchronize

| Data | Where |
|---|---|
| Position, rotation, anything that changes every frame | `NetworkTransform` / NGO |
| Inputs, camera, UI, callbacks | Nowhere: local to each player |
| Private data (oxygen, inventory of one player) | Local, or an NGO `NetworkVariable` readable by the owner only |
| One-shot effects (emotes, explosions) | A networked **event**, not a state |
| Big collections that change very often | A dedicated system in the game |

---

## Generated code and build

- The network code is generated in `Assets/Resources/MagicLinks/Network/MagicLinksNetwork.cs`, separately from
  `MagicLinksManager.cs`. It is regenerated when a networked link, a type or a field mode changes, which triggers a
  recompilation (only when NGO is installed and the content really changed).
- Network ids follow the alphabetical order of the link names.
- A build fails if a networked link has errors, or if the network script is out of date (click **Refresh Scripts**).

---

## Troubleshooting

| Symptom | Cause |
|---|---|
| `Net` grayed out in the editor | NGO is not installed: networked links stay local. |
| No **Network fields** under a custom type | The type has no public fields, or is not compiled yet (Refresh Scripts). |
| Red `!` on a variable | The type has fields that cannot be sent: hover the badge, fix them in Network fields. |
| "written while the client is not connected yet" | Writes before `OnSessionStarted`: wait for it. |
| "do not match the server" at connection | Client and server built with different networked links: rebuild both. |
| Value set on a client but not visible right away | Normal: it is visible once the server confirms (use `SetAsync` to wait for it). |
| Change inside an object not sent | Assign the value again (`link.Value = obj`) instead of only modifying a field. |
| "No MagicLinksManager in the scene" | The manager must exist when the session starts. |
