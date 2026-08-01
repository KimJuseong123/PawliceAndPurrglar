$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$localRoot = Join-Path $projectRoot "LocalAI"
$gateway = Join-Path $localRoot "runtime\gateway\paws-local-ai.exe"
$ollama = Join-Path $localRoot "runtime\ollama\ollama.exe"
$models = Join-Path $localRoot "models\ollama"
$sttModel = Join-Path $localRoot "models\faster-whisper-small"
$logs = Join-Path $localRoot "logs"
New-Item -ItemType Directory -Force -Path $logs | Out-Null

if (-not (Test-Path -LiteralPath $gateway) -or -not (Test-Path -LiteralPath $ollama)) {
    throw "Local AI runtime is incomplete. Run tools/setup-local-ai.ps1 first."
}

$env:OLLAMA_MODELS = $models
Start-Process -FilePath $ollama -ArgumentList "serve" -WorkingDirectory (Split-Path -Parent $ollama) -WindowStyle Hidden | Out-Null
$token = [Guid]::NewGuid().ToString("N")
Start-Process -FilePath $gateway -ArgumentList @(
    "--host", "127.0.0.1", "--port", "8765",
    "--ollama-host", "127.0.0.1", "--ollama-port", "11434",
    "--stt-model-path", ('"' + $sttModel + '"'), "--language", "ko",
    "--shutdown-token", $token, "--owner-pid", $PID
) -WorkingDirectory (Split-Path -Parent $gateway) -WindowStyle Hidden | Out-Null
Write-Host "Local AI services started on http://127.0.0.1:8765"
