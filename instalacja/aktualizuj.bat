@echo off
rem ===========================================================================
rem  Ewidencja Czasu - AKTUALIZACJA programu z GitHuba
rem  Dziala na laptopie (terminal) i na komputerze z panelem.
rem  Pobiera najnowszy kod (git pull), kompiluje i uruchamia program ponownie.
rem  Dane NIE sa zmieniane - leza w osobnym folderze.
rem ===========================================================================
setlocal
title Ewidencja Czasu - aktualizacja
cd /d "%~dp0.."
set "ROOT=%CD%"

where git >nul 2>&1
if errorlevel 1 goto bez_gita
if not exist "%ROOT%\.git" goto bez_gita

echo  Pobieranie najnowszej wersji z GitHuba...
git -C "%ROOT%" pull --ff-only
if errorlevel 1 (
  echo  Nie udalo sie pobrac zmian - sprawdz polaczenie z internetem.
  pause
  exit /b 1
)
goto kompiluj

:bez_gita
echo  Git nie jest zainstalowany (albo folder nie jest klonem repozytorium).
echo  Pobierz najnowsza wersje: https://github.com/kenochem/EwidencjaCzasu
echo  (Code -^> Download ZIP), rozpakuj ja NA obecny folder programu,
echo  zastepujac pliki, a potem uruchom ten skrypt ponownie.
choice /c TN /m " Czy kod jest juz podmieniony i skompilowac teraz"
if errorlevel 2 exit /b 0

:kompiluj
echo  Kompilacja...
call "%ROOT%\zrodla\kompiluj.bat" /cicho
if errorlevel 1 ( pause & exit /b 1 )

rem uruchom w tym samym trybie: panel (jesli jest skrot panelu w autostarcie) albo terminal
set "PANEL=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\Ewidencja Czasu - panel.lnk"
if exist "%PANEL%" (
  start "" "%PANEL%"
) else (
  explorer.exe "%ROOT%\EwidencjaCzasu.exe"
)
echo.
echo  GOTOWE - program zaktualizowany i uruchomiony.
echo.
pause
