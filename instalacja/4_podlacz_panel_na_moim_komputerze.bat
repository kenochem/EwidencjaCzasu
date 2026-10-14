@echo off
rem ===========================================================================
rem  Ewidencja Czasu - PANEL na Twoim komputerze (dane z laptopa przez siec)
rem  Uruchom na swoim komputerze (nie na laptopie). Uprawnienia administratora
rem  nie sa potrzebne.
rem
rem  Co robi:
rem   1. kompiluje program (jesli trzeba),
rem   2. zamyka dotychczasowy terminal na tym komputerze i wylacza jego autostart,
rem   3. zapamietuje haslo do laptopa (Menedzer poswiadczen Windows),
rem   4. tworzy skroty "Ewidencja Czasu - panel" na Pulpicie i w autostarcie.
rem  Panel nie obsluguje czytnika - pokazuje i poprawia dane z laptopa.
rem ===========================================================================
setlocal
title Ewidencja Czasu - panel na tym komputerze
cd /d "%~dp0.."
set "ROOT=%CD%"

echo.
echo  Dane laptopa wypisal skrypt 1_zainstaluj_terminal.bat na koncu instalacji.
echo.
set /p LAPTOP= Nazwa laptopa (np. LAPTOP-AB12CD):
set /p LUSER= Nazwa uzytkownika na laptopie:
if "%LAPTOP%"=="" exit /b 1
if "%LUSER%"=="" exit /b 1
set "SHARE=\\%LAPTOP%\EwidencjaCzasu"

echo.
echo  Podaj haslo uzytkownika %LUSER% na laptopie (zostanie zapamietane w Windows):
cmdkey /add:%LAPTOP% /user:%LAPTOP%\%LUSER% /pass
echo.
echo  Sprawdzanie dostepu do %SHARE% ...
if not exist "%SHARE%\pracownicy.csv" (
  echo  Brak dostepu do %SHARE%
  echo  Sprawdz: czy laptop jest wlaczony, w tej samej sieci, siec jest prywatna,
  echo  a nazwa, uzytkownik i haslo sa poprawne. Szczegoly: docs\LAPTOP.md
  pause
  exit /b 1
)
echo  OK - dane laptopa sa dostepne.

if not exist "%ROOT%\EwidencjaCzasu.exe" (
  call "%ROOT%\zrodla\kompiluj.bat" /cicho
  if errorlevel 1 ( pause & exit /b 1 )
)

echo  Zamykanie terminala na tym komputerze i wylaczanie jego autostartu...
taskkill /im EwidencjaCzasu.exe /f >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v EwidencjaCzasu /f >nul 2>&1

echo  Tworzenie skrotow...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$w = New-Object -ComObject WScript.Shell; foreach ($dir in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Startup'))) { $s = $w.CreateShortcut((Join-Path $dir 'Ewidencja Czasu - panel.lnk')); $s.TargetPath = '%ROOT%\EwidencjaCzasu.exe'; $s.Arguments = '/panel /dane:%SHARE%'; $s.WorkingDirectory = '%ROOT%'; $s.Description = 'Ewidencja Czasu - panel (dane z %LAPTOP%)'; $s.Save() }"

echo  Uruchamianie panelu...
start "" "%ROOT%\EwidencjaCzasu.exe" /panel /dane:%SHARE%
echo.
echo  GOTOWE. Panel uruchamia sie razem z Windows (ikonka przy zegarku),
echo  skrot "Ewidencja Czasu - panel" jest tez na Pulpicie.
echo.
pause
