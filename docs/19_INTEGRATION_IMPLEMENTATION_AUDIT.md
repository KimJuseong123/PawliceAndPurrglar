# Integration implementation audit

Date: 2026-07-31

This pass implements the requested core-stabilization scope for the police-and-
thief vertical slice. It preserves the existing network/input architecture and
uses deterministic standalone behavior where the production WebGL voice service
is not available.

## Implemented

- Four independent tool slots with selected-slot replication and `1`-`4`
  selection.
- Press-and-hold throw charging, ballistic trajectory preview, obstacle-aware
  flight timing, direct-hit stun, landing effects, and placed-tool behavior.
- Rock, Banana, GlueTrap, SensorLight, Bone, TunaCan, RubberChicken, and
  NoiseCan effect profiles.
- `E` context interaction and `G` companion petting separation.
- Eight companion commands routed through the existing dispatcher:
  `DOG_SCENT_TRACK`, `DOG_GUARD`, `DOG_CHASE`, `DOG_BITE`,
  `CAT_CLIMB_ROOF`, `CAT_SCRATCH`, `CAT_SCREAM`, and `CAT_FIND_HIDEOUT`.
- Deterministic exact/partial/misunderstood command matching.
- Standalone `V` mock voice path using `MockTranscript`, with the required
  Recording/Transcribing/Interpreting/Accepted/Confused/Failed HUD states.
- Player and animal speech bubbles and companion status icons with explicit
  status priority.
- Local validation prefabs for Git LFS pointer assets, including skinned
  character renderers, named limbs, furnished interiors, wall faces, and
  reusable collision geometry.
- Runtime-safe raccoon visibility, interior partition hiding, cutaway selection,
  role switching, and HUD wiring.

## Standalone controls

| Action | Control |
|---|---|
| Move/camera | Existing project controls |
| Interact | `E` |
| Pet companion | `G` while looking at the owned companion |
| Select tool slot | `1`-`4` |
| Throw a throwable | Hold `F` or left mouse button, release to throw |
| Companion debug command | `Shift+1`-`Shift+4` |
| Mock voice command | `V`, using `MockTranscript` |

## Verification

- Unity C# compilation: passed with no compiler errors in the final build log.
- Windows build: passed with three scenes.
- Build output: `Builds/Playtest/Windows/PawliceAndPurrglar.exe`
- Build log: `Logs/codex-windows-build-final.log`
- EditMode: **173 total, 173 passed, 0 failed**.
- PlayMode: **142 total, 142 passed, 0 failed**.
- Test results:
  - `Logs/TestResults/codex-editmode-final.xml`
  - `Logs/TestResults/codex-playmode-final-7.xml`

## Deferred next phase

The supported voice path in this build is deterministic mock voice. Full WebGL
microphone/STT/LLM adapter validation, production asset registry replacement,
and the broader inventory/minimap/door/window pass remain follow-up work.
