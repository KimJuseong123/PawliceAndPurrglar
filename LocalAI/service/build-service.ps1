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

# The venv is layered on a conda base whose CPython extensions load their DLLs
# from `Library\bin` — `ffi-8.dll` for `_ctypes`, plus lzma, bz2, expat and
# sqlite3. PyInstaller only searches PATH, so building from a shell where conda
# is not activated produces an exe that starts and dies on
# `ImportError: DLL load failed while importing _ctypes`. It reports the missing
# DLLs as **warnings** and exits 0, so the build looks like it worked.
$basePrefix = (& $venvPython -c "import sys; print(sys.base_prefix)").Trim()
foreach ($dllDirectory in @(
    (Join-Path $basePrefix "Library\bin"),
    (Join-Path $basePrefix "DLLs"),
    $basePrefix)) {
    if (Test-Path -LiteralPath $dllDirectory) {
        $env:PATH = "$dllDirectory;$env:PATH"
    }
}

# Built into a staging folder, then swapped in. Deleting the working gateway
# before PyInstaller runs means any build failure leaves no gateway at all, and
# the runtime is gitignored so there is nothing to restore from.
$stagingRoot = Join-Path $projectRoot "LocalAI\service\build\stage"
if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null
$finalRoot = $distRoot
$distRoot = $stagingRoot

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

# Refuse a build whose ctypes extension never made it in. PyInstaller downgrades
# an unresolved `ffi-8.dll` to a warning and still exits 0, and the resulting exe
# dies on its first import.
$ctypesInInternal = Test-Path -LiteralPath (Join-Path $packagedRoot "_internal\_ctypes.pyd")
$ctypesAtRoot = Test-Path -LiteralPath (Join-Path $packagedRoot "_ctypes.pyd")
if (-not $ctypesInInternal -and -not $ctypesAtRoot) {
    throw "The packaged gateway has no _ctypes extension; uvicorn will not import."
}

# Swap in only now that the artifact looks whole.
if (Test-Path -LiteralPath $finalRoot) {
    Get-ChildItem -LiteralPath $finalRoot -Force | Remove-Item -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $finalRoot | Out-Null
Get-ChildItem -LiteralPath $packagedRoot -Force | Move-Item -Destination $finalRoot -Force
Remove-Item -LiteralPath $stagingRoot -Recurse -Force

Write-Host "Gateway packaged under $finalRoot\paws-local-ai.exe"
