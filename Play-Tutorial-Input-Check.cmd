@echo off
setlocal
set "QA_EXE=%~dp0Builds\Windows-6000.3.21f1-tut-11b-vat-card-r2\EvolutionShop.exe"
set "QA_PROFILE=%~dp0Builds\TutorialInputCheck"
if not exist "%QA_EXE%" (
  echo The native tutorial QA build is missing. See AGENTS.md.
  pause
  exit /b 1
)
if not exist "%QA_PROFILE%" mkdir "%QA_PROFILE%"
if not exist "%QA_PROFILE%\native-qa-profile.txt" echo Evolution Shop isolated native QA>"%QA_PROFILE%\native-qa-profile.txt"
set "QA_WIDTH=1280"
set "QA_HEIGHT=720"
if /i "%~1"=="1080" set "QA_WIDTH=1920"
if /i "%~1"=="1080" set "QA_HEIGHT=1080"
start "Evolution Shop tutorial input check" "%QA_EXE%" -evolutionNativeQa "%QA_PROFILE%" -screen-width %QA_WIDTH% -screen-height %QA_HEIGHT% -screen-fullscreen 0 -logFile "%QA_PROFILE%\player.log"
endlocal
