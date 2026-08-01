$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$localRoot = Join-Path $projectRoot "LocalAI"
$venvRoot = Join-Path $localRoot ".venv"
$venvPython = Join-Path $venvRoot "Scripts\python.exe"
$modelRoot = Join-Path $localRoot "models\faster-whisper-small"
$ollamaRoot = Join-Path $localRoot "models\ollama"
$ollamaRuntime = Join-Path $localRoot "runtime\ollama"
$gatewayRuntime = Join-Path $localRoot "runtime\gateway"
$configRoot = Join-Path $localRoot "config"
$logsRoot = Join-Path $localRoot "logs"

foreach ($directory in @($localRoot, $venvRoot, $modelRoot, $ollamaRoot, $ollamaRuntime, $gatewayRuntime, $configRoot, $logsRoot)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

$pythonCommand = Get-Command python -ErrorAction SilentlyContinue
if ($null -eq $pythonCommand) {
    throw "Python 3.12+ was not found on PATH. Install Python and rerun this script."
}
$python = $pythonCommand.Source

if (-not (Test-Path -LiteralPath $venvPython)) {
    & $python -m venv $venvRoot
    if ($LASTEXITCODE -ne 0) { throw "Python virtual environment creation failed." }
}

& $venvPython -m pip install --upgrade pip
& $venvPython -m pip install -r (Join-Path $localRoot "service\requirements.txt")
if ($LASTEXITCODE -ne 0) { throw "Python dependency installation failed." }

if (-not (Get-ChildItem -LiteralPath $modelRoot -Force | Select-Object -First 1)) {
    & $venvPython -c "from huggingface_hub import snapshot_download; snapshot_download(repo_id='Systran/faster-whisper-small', local_dir=r'$modelRoot')"
    if ($LASTEXITCODE -ne 0) { throw "faster-whisper-small download failed." }
}

$ollamaExecutable = Join-Path $ollamaRuntime "ollama.exe"
if (-not (Test-Path -LiteralPath $ollamaExecutable)) {
    $archive = Join-Path $env:TEMP "paws-ollama-windows.zip"
    Invoke-WebRequest -UseBasicParsing -Uri "https://ollama.com/download/ollama-windows-amd64.zip" -OutFile $archive
    Expand-Archive -LiteralPath $archive -DestinationPath $ollamaRuntime -Force
    Remove-Item -LiteralPath $archive -Force
}
if (-not (Test-Path -LiteralPath $ollamaExecutable)) {
    throw "Official Ollama Windows runtime was not found after extraction."
}

$oldModels = $env:OLLAMA_MODELS
$env:OLLAMA_MODELS = $ollamaRoot
$ollamaProcess = Start-Process -FilePath $ollamaExecutable -ArgumentList "serve" -WorkingDirectory $ollamaRuntime -WindowStyle Hidden -PassThru
try {
    $ready = $false
    for ($index = 0; $index -lt 60 -and -not $ready; $index++) {
        Start-Sleep -Milliseconds 500
        try {
            Invoke-RestMethod -Uri "http://127.0.0.1:11434/api/tags" -TimeoutSec 2 | Out-Null
            $ready = $true
        } catch { }
    }
    if (-not $ready) { throw "Ollama did not become ready on 127.0.0.1:11434." }
    & $ollamaExecutable pull "qwen3:4b-instruct"
    if ($LASTEXITCODE -ne 0) { throw "qwen3:4b-instruct download failed." }
}
finally {
    $env:OLLAMA_MODELS = $oldModels
    if ($ollamaProcess -and -not $ollamaProcess.HasExited) { $ollamaProcess.Kill() }
}

& (Join-Path $localRoot "service\build-service.ps1")
if ($LASTEXITCODE -ne 0) { throw "Gateway packaging failed." }

$gatewayExecutable = Join-Path $gatewayRuntime "paws-local-ai.exe"
if (-not (Test-Path -LiteralPath $gatewayExecutable)) {
    throw "Gateway executable is missing after packaging."
}

$manifest = [ordered]@{
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    gateway = "paws-local-ai"
    sttModel = "Systran/faster-whisper-small"
    ollamaModel = "qwen3:4b-instruct"
    pythonVersion = (& $venvPython --version).Trim()
    selfContained = $true
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $localRoot "manifest.json") -Encoding UTF8

Write-Host "Local AI installation completed."
Write-Host "Gateway: $gatewayExecutable"
Write-Host "STT model: $modelRoot"
Write-Host "Ollama models: $ollamaRoot"
