# Windows Local AI setup

## First installation

Run PowerShell from the repository root:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\tools\setup-local-ai.ps1
.\tools\verify-local-ai-installation.ps1
```

The setup downloads the faster-whisper model and the exact Ollama tag
`qwen3:4b-instruct`, then packages the Gateway. The download requires Internet
access only during installation. Models and runtime binaries are stored under
`LocalAI/` and are ignored by Git.

## Manual service control

```powershell
.\tools\start-local-ai.ps1
Invoke-RestMethod http://127.0.0.1:8765/health
.\tools\stop-local-ai.ps1
```

Unity Player builds resolve configuration relative to the executable folder.
The Editor resolves it relative to the repository root.

## Build

After setup, use the Unity menu `PawliceAndPurrglar > Build > Build Windows Standalone
with Local AI`, or invoke the corresponding Editor method in batch mode. The
default output is `Build/Windows/`. The build fails when required runtime,
model, configuration, or license files are missing. `-allowMissingLocalAi` is
for an explicitly AI-disabled development build only.
