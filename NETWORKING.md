# Magic Links – Network mode

Magic Links can sync your links (variables, lists, events) between players with **Netcode for GameObjects 2.x**.

- Check **Networked** on a link in the MagicLinkEditor. Your code stays the same.
- The **server (or host) owns the value**. When a client writes, it sends a request.
- **No session running?** Networked links act like local ones.
- **Netcode not installed?** Everything stays local.

## Setup

Install **Netcode for GameObjects 2.x**, then check **Networked** on the links you want to sync.

## Writing a value

On a client, writing a value **sends a request** to the server.
The local value only changes when the server sends it back.

So **don't read a value right after writing it**: on a client, you get the old one.

```csharp
// On a client, lever.Value is still false here: the gate never opens
lever.Value = true;
if (lever.Value) OpenGate();
```

**React in `OnValueChanged` instead.** It runs on every player once the new value arrives:

```csharp
    private void OnEnable()  => Lever.OnValueChanged += OnLeverChanged;
    private void OnDisable() => Lever.OnValueChanged -= OnLeverChanged;

    private void OnLeverChanged(bool pulled)
    {
        if (pulled) OpenGate();
    }
```

The same mistake with numbers:

```csharp
// On a client, both lines read the old value (100): the result is 110, not 120
gold.Value += 10;
gold.Value += 10;

// Correct is this
gold.Value += 20;
```

> On the host and offline, values change immediately, so this mistake only shows up on clients.

## Example: a chest only one player can open

P1 and P2 play together. There is one chest, and only one player at a time can have it open.

`ChestUser` is an `INT` variable with an initial value of `-1` (nobody), **Networked**.

### The trap

```csharp
// P1 and P2 click at the same time.
// Both see -1, both write their id, both open the chest.
if (chest.Value == -1) chest.Value = myId;
```

### Fix 1: `SetIfAsync`

"Set my id **if** it's still -1." The server checks and writes in one step, so only one player wins.

```csharp
var chest = MagicVariables.INT["ChestUser"];
int myId = (int)MagicLinksNetwork.LocalClientId;

if (await chest.SetIfAsync(expected: -1, value: myId) == MagicWriteResult.Accepted)
{
    OpenChestUI(); 
}
else
{
    ShowMessage("Someone is already looting");
}

// Closing
await chest.SetIfAsync(expected: myId, value: -1);
```

P1's request reaches the server first and is accepted. When P2's request arrives, the value is no longer -1, so P2 gets `Refused`.

> Check **Reset when writer leaves** on `ChestUser`: if P1 disconnects with the chest open, it goes back to `-1`.

### Fix 2: `WriteRule`

`SetIfAsync` only protects the code that uses it. If some other script does `chest.Value = 5`, nothing stops it.

A **`WriteRule`** is a rule the server checks **before every write** on that link, whatever code made it. If it returns `false`, the write is refused.

```csharp
// Register it on EVERY machine at startup (any machine can become the host)
chest.WriteRule = (fromClientId, current, value) =>
{
    if (value == -1) return current == (int)fromClientId;   // only the player inside can close it
    return current == -1;                                   // can only open it if nobody is inside
};
```

Now a plain write is safe, and `SetAsync` tells you if it went through:

```csharp
if (await chest.SetAsync(myId) == MagicWriteResult.Accepted)
{
    OpenChestUI();
}
```

With this rule, P2 can't open the chest while P1 is inside, and can't close it on P1 either.

### Which one?

- **`SetIfAsync`**: the condition is in the call. It only protects that call.
- **`WriteRule`**: the condition is on the variable. Every write goes through it, from any script.

`SetIfAsync` needs no setup, and each call can check something different. A `WriteRule` is one fixed rule per variable that you register on every machine, so keep it for rules that must never be broken.

---

## Knowing if your write worked

`OnValueChanged` tells everyone **that the value changed**.
The `Async` methods tell **you** whether **your** write was accepted.

You only need the `Async` methods when a write can be refused (by `SetIfAsync` or a `WriteRule`). A refused write never raises `OnValueChanged`.

| `MagicWriteResult` | Meaning |
|---|---|
| `Accepted` | Applied, and already visible on your side |
| `Refused` | The condition or the `WriteRule` said no |
| `Disconnected` | The session ended before the server answered |

Every write has an `Async` version: `SetAsync`, `SetIfAsync`, `AddAsync`, `RemoveAsync`, `RemoveAtAsync`, `SetAtAsync`, `ModifyAtAsync`, `ClearAsync`.

---

## Wait until the session is ready

A client can't write while it's still connecting. Those writes are ignored, with a warning.

```csharp
// Too early on a client
void Start() => MagicVariables.INT["Gold"].Value = 0;

// This is correct
MagicLinksNetwork.OnSessionReady += () => MagicVariables.INT["Gold"].Value = 0;
```

| API | |
|---|---|
| `MagicLinksNetwork.IsOnline` | `true` during a session |
| `MagicLinksNetwork.IsServerOrOffline` | `true` on the host, the server, or offline |
| `MagicLinksNetwork.LocalClientId` | This player's id (`0` on the host) |
| `MagicLinksNetwork.OnSessionReady` | This machine can now write |
| `MagicLinksNetwork.OnSessionEnded` | The session stopped. Links keep their last value and become local again. |

---

## Events

```csharp
MagicEvents.VECTOR3["Explosion"].Raise(position);
```

Every player receives it **exactly once, in the same order**. That includes the player who raised it, after a round trip to the server.

```csharp
// WRONG : The effect plays twice for the player who raised it
MagicEvents.VECTOR3["Explosion"].Raise(position);
PlayExplosion(position);

// CORRECT : Only play it in the listener
MagicEvents.VECTOR3["Explosion"].OnEventRaised += PlayExplosion;
```

---

## Lists

Lists are synced **one operation at a time** (`Add`, `Remove`, `SetAt`…).
If P1 and P2 drop an item at the same time, both items end up in the list.

```csharp
var loot = MagicVariables.STRING_LIST["SharedLoot"];

loot.Add("Sword");   // P1
loot.Add("Shield");  // P2, at the same time → Sword, Shield
```

**Don't keep an index around**: when someone removes an item, the others move.

```csharp
// WRONG : If "Sword" was picked up, index 0 is now "Shield"
loot.SetAt(0, "Golden Sword");

// CORRECT
int index = loot.FindIndex(item => item == "Sword");
if (index >= 0) loot.SetAt(index, "Golden Sword");
```

- **Remove player's items when they leave** (off by default): when checked, the items a player added are removed when they disconnect. Handy for one line per player.
- On a client, `ModifyAt` changes a **copy**: your item only changes once the server accepts it.
- Lists can have a `WriteRule` too: `(fromClientId, change, items) => bool`. `change` has the `Operation`, `Index` and `Item`, and `items` is the list before the change. On those lists, use `SetAt` rather than `ModifyAt` (offline, `ModifyAt` skips the rule).

## Custom types

To send a change, **assign the value again**:

```csharp
// WRONG : Only changes your local copy
MagicVariables.BOSSSTATE["Boss"].Value.hp -= 10;

// CORRECT
var boss = MagicVariables.BOSSSTATE["Boss"].Value;
boss.hp -= 10;
MagicVariables.BOSSSTATE["Boss"].Value = boss;
```

In **Custom Types > Network fields**, pick a mode for each public field:

| Mode | For |
|---|---|
| `Auto` | Default: sent if possible |
| `Value` | Numbers, strings, enums, vectors, lists, structs… |
| `Excluded` | Never sent (AudioSource, callbacks…) |
| `Reference` | A GameObject or component with a spawned `NetworkObject` |

- Only the fields that changed are sent. If P1 changes `hp` while P2 changes `phase`, neither overwrites the other.
- A received value is a **new instance**. Don't network a class that other scripts keep a reference to. Prefer **structs**.
- A link with an error is **not synced at all**, and the build fails.
