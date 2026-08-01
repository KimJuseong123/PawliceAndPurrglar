$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot

try {
    Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:8765/shutdown" -TimeoutSec 3 | Out-Null
} catch {
    Write-Warning "Gateway shutdown request failed; no unrelated process was terminated."
}

$runtimeRoot = (Resolve-Path (Join-Path $projectRoot "LocalAI\runtime")).Path
Get-Process -Name paws-local-ai,ollama -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -like "$runtimeRoot*" } |
    Stop-Process -Force
