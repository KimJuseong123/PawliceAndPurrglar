# Local AI manual test guide

The following checks are intentionally left for the developer because this
implementation task does not perform microphone or gameplay testing.

1. Run `Build/Windows/PawliceAndPurrglar.exe`.
2. Wait for the Local AI status to reach `ready`.
3. Start a police game.
4. Hold `V` and speak a short Korean command.
5. Release `V`, or keep holding until the five-second limit closes capture.
6. Confirm the HUD shows recording, encoding, transcription, interpretation,
   execution, and cooldown states.
7. Confirm the original transcript and interpreted command are shown.
8. Confirm the dog receives the command through its normal command behavior.
9. Repeat with an unknown target and confirm no action is dispatched.
10. Repeat with no microphone permission and confirm normal keyboard play still
    works.

When Unity starts the local processes, Gateway and Ollama logs are written to
`Build/Windows/LocalAI/logs/`. Unity
logs are under the usual `%LOCALAPPDATA%\Unity\Editor\Editor.log` location.
The local temporary WAV folder is the Player's
`Application.persistentDataPath\VoiceTemp\` directory and is deleted after
processing by default.
