$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$localRoot = Join-Path $projectRoot "LocalAI"
$required = @(
    (Join-Path $localRoot "config\local-ai.json"),
    (Join-Path $localRoot "manifest.json"),
    (Join-Path $localRoot "runtime\gateway\paws-local-ai.exe"),
    (Join-Path $localRoot "runtime\ollama\ollama.exe"),
    (Join-Path $localRoot "models\faster-whisper-small"),
    (Join-Path $localRoot "models\ollama"),
    (Join-Path $projectRoot "THIRD_PARTY_NOTICES.md"),
    (Join-Path $projectRoot "Licenses\faster-whisper-MIT.txt"),
    (Join-Path $projectRoot "Licenses\faster-whisper-small-MIT.txt"),
    (Join-Path $projectRoot "Licenses\Qwen3-Apache-2.0.txt"),
    (Join-Path $projectRoot "Licenses\Ollama-license.txt")
)

$missing = @($required | Where-Object { -not (Test-Path -LiteralPath $_) })
if ($missing.Count -gt 0) {
    Write-Error ("Local AI installation is incomplete:`n" + ($missing -join "`n"))
    exit 1
}

Write-Host "Local AI installation verified."
