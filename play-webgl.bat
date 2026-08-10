@echo off
setlocal EnableExtensions

set "ROOT=%~dp0"
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe"
set "WEBGL_ROOT=%ROOT%Builds\Playtest\WebGL"
set "WEBGL_PORT=8080"
set "VOICE_PORT=3000"
set "SKIP_BUILD=0"
set "SKIP_VOICE=0"

:parse_args
if "%~1"=="" goto args_done
if /I "%~1"=="--no-build" set "SKIP_BUILD=1"
if /I "%~1"=="--no-voice" set "SKIP_VOICE=1"
if /I "%~1"=="--port" (
    set "WEBGL_PORT=%~2"
    shift
)
shift
goto parse_args

:args_done
if not exist "%UNITY%" (
    echo [ERROR] Unity 6000.5.4f1 was not found:
    echo         %UNITY%
    exit /b 1
)

if "%SKIP_BUILD%"=="0" if not exist "%WEBGL_ROOT%\index.html" (
    echo [BUILD] Creating WebGL playtest...
    if not exist "%ROOT%Logs" mkdir "%ROOT%Logs"
    "%UNITY%" -batchmode -nographics -quit -projectPath "%ROOT%" -executeMethod PawliceAndPurrglar.Editor.PlaytestBuild.BuildWebGlPlaytest -logFile "%ROOT%Logs\webgl-playtest-build.log"
    if errorlevel 1 (
        echo [ERROR] Unity WebGL build failed. See Logs\webgl-playtest-build.log
        exit /b 1
    )
)

if not exist "%WEBGL_ROOT%\index.html" (
    echo [ERROR] WebGL build is missing: %WEBGL_ROOT%\index.html
    echo         Run without --no-build or build it from Unity first.
    exit /b 1
)

where node >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Node.js is required to serve the WebGL build.
    exit /b 1
)

start "Paws & Loot WebGL" /D "%ROOT%" cmd /k "set PORT=%WEBGL_PORT%&& set WEBGL_ROOT=%WEBGL_ROOT%&& node server\static-webgl.mjs"

if "%SKIP_VOICE%"=="0" (
    if not exist "%ROOT%server\node_modules" (
        echo [WARN] server\node_modules is missing. Voice server was not started.
        echo       Run: cd server ^&^& npm install
    ) else (
        start "Paws & Loot Voice API" /D "%ROOT%server" cmd /k "set PORT=%VOICE_PORT%&& npm run dev"
    )
)

timeout /t 2 /nobreak >nul
start "" "http://localhost:%WEBGL_PORT%/"
echo.
echo Paws ^& Loot is starting in your browser.
echo Game:  http://localhost:%WEBGL_PORT%/
echo Voice: http://localhost:%VOICE_PORT%/health
echo Close the WebGL and Voice API console windows to stop local services.
exit /b 0
