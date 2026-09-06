@echo off
REM Unity 6000.3.10f1 batchmode compile verification.
REM Adjust UNITY_PATH if Unity is installed elsewhere.
set UNITY_PATH=E:\UnityInstallation\6000.3.10f1\Editor\Unity.exe

if not exist "%UNITY_PATH%" (
    echo [ERROR] Unity not found at %UNITY_PATH%
    exit /b 2
)

echo [1/3] Running Unity batch compile...
"%UNITY_PATH%" -quit -batchmode -nographics -projectPath "%~dp0." -logFile compile.log
set UNITY_EXIT=%errorlevel%

echo [2/3] Checking for compile errors...
findstr /C:": error CS" compile.log > nul
if %errorlevel% equ 0 (
    echo [ERROR] Compilation failed! Check compile.log:
    findstr /C:": error CS" compile.log
    exit /b 1
)

findstr /C:"Aborting batchmode" compile.log > nul
if %errorlevel% equ 0 (
    echo [ERROR] Unity aborted batchmode - fatal error. Check compile.log.
    exit /b 3
)

if not %UNITY_EXIT% equ 0 (
    echo [ERROR] Unity exited with code %UNITY_EXIT%. Check compile.log.
    exit /b 4
)

echo [3/3] Compilation CLEAN (0 errors).
exit /b 0
