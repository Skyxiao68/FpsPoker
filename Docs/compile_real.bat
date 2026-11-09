@echo off
setlocal
set PROJ=D:\Users\Ben\Documents\School\GD yr3\FpsPoker
set UNITY=D:\Users\Ben\Documents\unity\Installer\6000.6.0f1\Editor\Unity.exe
if not exist "%UNITY%" (
  echo UNITY_NOT_FOUND "%UNITY%"
  exit /b 2
)
if not exist "%PROJ%" (
  echo PROJECT_NOT_FOUND
  exit /b 3
)
if not exist "%PROJ%\Logs" mkdir "%PROJ%\Logs"
"%UNITY%" -batchmode -quit -projectPath "%PROJ%" -logFile "%PROJ%\Logs\batch-port.log"
echo UNITY_EXIT=%ERRORLEVEL%
