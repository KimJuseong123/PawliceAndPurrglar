$ErrorActionPreference = "Stop"

$serviceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent (Split-Path -Parent $serviceRoot)
$venvPython = Join-Path $projectRoot "LocalAI\.venv\Scripts\python.exe"
$distRoot = Join-Path $projectRoot "LocalAI\runtime\gateway"
$buildRoot = Join-Path $projectRoot "LocalAI\service\build"
$specRoot = Join-Path $projectRoot "LocalAI\service\build"

if (-not (Test-Path -LiteralPath $venvPython)) {
    throw "Python virtual environment is missing. Run tools/setup-local-ai.ps1 first."
}

if (Test-Path -LiteralPath $distRoot) {
    Get-ChildItem -LiteralPath $distRoot -Force | Remove-Item -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $distRoot | Out-Null

& $venvPython -m PyInstaller --noconfirm --clean --onedir `
    --name paws-local-ai `
    --distpath $distRoot `
    --workpath $buildRoot `
    --specpath $specRoot `
    --paths $serviceRoot `
    --collect-all faster_whisper `
    --collect-all numpy `
    (Join-Path $serviceRoot "entry.py")

if ($LASTEXITCODE -ne 0) {
    throw "PyInstaller failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath (Join-Path $distRoot "paws-local-ai\paws-local-ai.exe"))) {
    throw "Gateway executable was not produced."
}

$packagedRoot = Join-Path $distRoot "paws-local-ai"
Get-ChildItem -LiteralPath $packagedRoot -Force | Move-Item -Destination $distRoot -Force
Remove-Item -LiteralPath $packagedRoot -Recurse -Force

Write-Host "Gateway packaged under $distRoot\paws-local-ai.exe"
