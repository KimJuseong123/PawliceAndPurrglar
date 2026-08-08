# Paws & Loot input audit

## Production mapping

| Input | Action | Owner |
|---|---|---|
| `1`–`4` | Select item quick slot | `GameplayInputRouter` → `QuickSlotKeyboardInput` |
| `Ctrl+1`–`Ctrl+4` | Animal command shortcut | `GameplayInputRouter` → `CompanionCommandKeyboardInput` |
| `V` hold | Push-to-talk voice recording | `GameplayInputRouter` → `VoiceCommandInput` |
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
- This table said `Shift+1`–`Shift+4` and `V` press/release for a long time while
  the router read **Ctrl** and never wired the release at all. Recording ran to
  the five-second cap no matter how briefly the key was held. Fixed in the router;
  the table now matches the code.

## Voice recording

`V` is push-to-talk. Letting go ends the recording, and a release under 0.7s
records up to that floor first — the capture rejects anything under 250ms, so a
tap would otherwise be a failure rather than a short command. The cap is
`VoiceConfig.maximumUtteranceSeconds` (5s).

Only the role this machine plays can open the microphone
(`VoiceCommandInput.CanCaptureLocally`). A component exists for both roles on both
machines, and Windows hands a capture device to one client — starting both left one
capture receiving silence.
- Network mode still reads action edges in `NetworkInputBridge`; it disables
  the offline keyboard consumers before sending the same logical actions to
  the host, so the two paths do not execute twice.

## Legacy bindings retained for compatibility

`F`, `Q`, and `G` remain in older gameplay/network paths until their gameplay
features are migrated. They are not shown as primary HUD bindings. `Space`
and movement keys remain movement controls.
