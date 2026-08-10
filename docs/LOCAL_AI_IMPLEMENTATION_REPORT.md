# Local AI implementation report

## 1. Existing structure survey

- Unity version: `6000.5.4f1` from `ProjectVersion.txt`.
- Build scenes retained: `Bootstrap.unity`, `Game.unity`, `Result.unity`.
- Existing `GameplayInputRouter` already owned the `V` key press/release flow.
- Existing WebGL/Fastify voice path was retained; Windows uses the new local path.
- Existing `CompanionCommandDispatcher` remains the behavior gate.
- Existing command enum remains authoritative: dog `Track`, `Search`, `Guard`,
  `Bark`; cat `Scout`, `Distract`, `Steal`, `Hide`; shared `Stay` and related
  commands.
- Existing scene, companion movement, and command execution systems were
  extended rather than replaced.

## 2. Implemented architecture

```text
V down/up
  -> Unity Microphone, 16 kHz mono, max 5 seconds
  -> PCM 16-bit WAV in Application.persistentDataPath/VoiceTemp
  -> LocalAI Gateway 127.0.0.1:<selected port>
  -> faster-whisper-small (CUDA int8_float16, CPU int8 fallback)
  -> Ollama qwen3:4b-instruct
  -> JSON schema and context validation
  -> VoiceCommandMapper
  -> DogBehaviorFilter
  -> existing CompanionCommandDispatcher
```

The Gateway implements `/health`, `/v1/voice-command`, and token/PID-protected
`/shutdown`. Qwen receives only the available command and target context and
cannot directly manipulate Unity transforms or navigation agents. `BITE` maps
to `NONE` because the current project command executor has no bite command.

`DogBehaviorProfile`, `DogBehaviorFilter`, and `DogBehaviorDecision` provide the
ScriptableObject-configurable distraction and deterministic-seed behavior layer.
The Unity HUD receives `Unavailable`, `Starting`, `Idle`, `Recording`,
`Encoding`, `Transcribing`, `Interpreting`, `Executing`, `Cooldown`, and `Error`
states plus transcript, interpretation, decision, and error callbacks.

## 3. Main changed areas

- `Assets/_Project/Scripts/Integration/Voice/LocalAI/`: configuration, health,
  HTTP upload, DTOs, process management, command context, mapping, validation,
  behavior filtering, and execution.
- `Assets/_Project/Scripts/Integration/Voice/VoiceCommandInput.cs` and
  `VoiceCaptureProvider.cs`: native microphone capture, WAV submission, cleanup,
  retry/error state handling, and WebGL compatibility.
- `Assets/_Project/Scripts/Integration/Voice/VoiceCommandJson.cs` and existing
  UI presenters: request/fallback metadata and public HUD state presentation.
- `Assets/_Project/Editor/LocalAI/`: installation validation, build processor,
  and Windows build menu/method.
- `LocalAI/service/`: FastAPI Gateway, STT service, Ollama client, interpreter,
  schemas, PyInstaller entry point, requirements, and unit tests.
- `tools/`: setup, verification, start, and stop PowerShell scripts.
- `LocalAI/config/local-ai.json`, license notices, `.gitignore`, and the four
  LocalAI documentation files.
- `Game.unity`: restored the existing authored canvas scale and added an
  independent project-services root for standalone scene configuration/logging.

## 4. Installation status

- faster-whisper: `Systran/faster-whisper-small` downloaded and verified.
- Qwen: Ollama tag `qwen3:4b-instruct` downloaded and verified.
- Ollama: official Windows runtime staged at `LocalAI/runtime/ollama/ollama.exe`.
- Gateway: PyInstaller Windows executable staged at
  `LocalAI/runtime/gateway/paws-local-ai.exe`.
- Installation verifier: passed.
- Gateway health probe: returned `ready`, STT `cuda`, and LLM `ready`. This is
  a service-readiness check, not speech recognition or gameplay validation.

## 5. Windows build result

- Unity version: `6000.5.4f1`
- Build target: `StandaloneWindows64`
- Development Build: false
- Script Debugging: false
- Build path: `Build/Windows/PawliceAndPurrglar.exe`
- Build result: `Succeeded`
- BuildReport: `totalErrors=0`, `totalWarnings=266`,
  `totalSize=151,701,407` bytes
- Full self-contained output, including models and runtime: `5,330,063,121`
  bytes (`4.96 GiB`)
- Build log: `Logs/windows-local-ai-build-final-startup.log`
- Build report: `Build/Windows/build-report.json`

The build warnings are existing Unity/project warnings reported by the
BuildReport; no C# compile errors or build errors were present.

## 6. Packaged LocalAI files

The build contains:

- `LocalAI/runtime/gateway/paws-local-ai.exe` and its `_internal` directory.
- `LocalAI/runtime/ollama/ollama.exe`.
- `LocalAI/models/faster-whisper-small/`.
- `LocalAI/models/ollama/` with the Qwen model blobs and manifest.
- `LocalAI/config/local-ai.json`.
- `LocalAI/licenses/` and `LocalAI/manifest.json`.
- Runtime-owned process output is written to `LocalAI/logs/gateway.log` and
  `LocalAI/logs/ollama.log`.

Large model/runtime directories remain ignored by Git and are not intended for
normal source commits.

## 7. Verification performed

- Unity compile/import check: passed with no C# compilation errors.
- Unity Edit Mode tests: `193/193` passed in
  `Logs/editmode-results-final-startup.xml`.
- Python syntax compilation: passed with `python -m compileall -q`.
- Python Gateway tests: `3/3` passed.
- Configuration JSON parsing and installation verification: passed.
- Windows Standalone BuildReport: succeeded with zero errors.

## 8. Known limitations and deferred checks

- No real microphone speech was recorded by this task.
- No WAV capture quality or silence-threshold tuning was performed.
- No Korean STT accuracy or Qwen interpretation accuracy evaluation was
  performed.
- No dog gameplay behavior or end-to-end play session was performed.
- GPU/CPU performance comparison, multiple microphone testing, and long-run
  stability testing were not performed.
- The self-contained output is large because both model families are bundled.
- The first setup requires Internet access to download dependencies, models,
  and the official Ollama runtime; later execution is local-only.
