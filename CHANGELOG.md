# Changelog

## [1.0.5]

### Added
- Optional network synchronization with Netcode for GameObjects, enabled per link with the **Networked** toggle.
  Server-authoritative states and events, late join, ordered delivery. List variables are synchronized operation by operation.
- `SetAsync` and `TrySetAsync` (atomic compare-and-set on the server) on variables, with `MagicSetResult`.
- Async list operations (`AddAsync`, `RemoveAsync`, `RemoveAtAsync`, `SetAtAsync`, `ModifyAtAsync`, `ClearAsync`) and `FindIndex`.
- Validators on variables and lists: rules checked offline or on the server before accepting a write.
- `MagicLinksNetwork.IsOnline`, `IsAuthority`, `LocalClientId`, `OnSessionStarted`, `OnSessionEnded`.
- Per-field network modes for custom types (Value, Excluded, Reference) with edit-time validation and a build check.
- **Reset on leave** option for variables used as locks, **Remove items on leave** for lists of per-player entries.

### Unchanged
- Code reading or writing magic links. Existing links are not networked by default and behave as before.
