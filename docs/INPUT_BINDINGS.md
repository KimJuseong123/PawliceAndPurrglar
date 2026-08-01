# Paws & Loot input audit

## Production mapping

| Input | Action | Owner |
|---|---|---|
| `1`–`4` | Select item quick slot | `GameplayInputRouter` → `QuickSlotKeyboardInput` |
| `Shift+1`–`Shift+4` | Animal command shortcut | `GameplayInputRouter` → `CompanionCommandKeyboardInput` |
| `V` press/release | Start/stop voice recording | `GameplayInputRouter` → `VoiceCommandInput` |
| `Escape` | Cancel recording or close the current modal | `GameplayInputRouter` |
| `Tab` | Open/close inventory | `GameplayInputRouter` → `RoleAwareHudController` |
| `E` | Current contextual interaction | `GameplayInputRouter` → `PlayerInteractionInput` |

The HUD obtains labels from this mapping and does not use debug strings such as
`COMMAND [1/2]`.

## Conflicts found and resolved

- Input System `Previous` previously used keyboard `1`; its keyboard binding
  was removed.
- Input System `Next` previously used keyboard `2`; its keyboard binding was
  removed.
- Input System `Sprint` previously used `LeftShift`; its keyboard binding was
  removed because `LeftShift` is the animal-command modifier.
- Offline quick-slot and companion-command components previously read the
  keyboard independently. They now consume router events.
- `PlayerInteractionInput` previously read `E` independently. It now consumes
  the router event.
- `VoiceCommandInput` previously read `V` independently. It now receives the
  router event and supports Escape cancellation.
- Network mode still reads action edges in `NetworkInputBridge`; it disables
  the offline keyboard consumers before sending the same logical actions to
  the host, so the two paths do not execute twice.

## Legacy bindings retained for compatibility

`F`, `Q`, and `G` remain in older gameplay/network paths until their gameplay
features are migrated. They are not shown as primary HUD bindings. `Space`
and movement keys remain movement controls.
