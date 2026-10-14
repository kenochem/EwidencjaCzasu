@echo off
rem ===========================================================================
rem  Ewidencja Czasu - IMPORT danych na laptopie (terminalu)
rem  Uruchom na laptopie po 1_zainstaluj_terminal.bat.
rem  Domyslnie szuka pliku EwidencjaCzasu_dane.zip na Pulpicie;
rem  mozna tez przeciagnac plik ZIP na ten skrypt.
rem
rem  UWAGA: obecne dane na laptopie zostana zastapione danymi z archiwum
rem  (wczesniej sa zapisywane w kopii obok).
rem ===========================================================================
setlocal
title Ewidencja Czasu - import danych
cd /d "%~dp0.."
set "ROOT=%CD%"
set "DATA=%USERPROFILE%\Documents\EwidencjaCzasu"
set "ZIP=%~1"
if "%ZIP%"=="" set "ZIP=%USERPROFILE%\Desktop\EwidencjaCzasu_dane.zip"

if not exist "%ZIP%" (
  echo  Nie znaleziono pliku: %ZIP%
  echo  Skopiuj EwidencjaCzasu_dane.zip na Pulpit albo przeciagnij go na ten skrypt.
  pause
  exit /b 1
)

echo  Archiwum : %ZIP%
echo  Cel      : %DATA%
echo.
echo  Obecne dane na laptopie zostana zastapione danymi z archiwum.
choice /c TN /m " Kontynuowac"
if errorlevel 2 exit /b 0

taskkill /im EwidencjaCzasu.exe /f >nul 2>&1
timeout /t 2 /nobreak >nul

if exist "%DATA%\odbicia.csv" (
  echo  Zapisywanie kopii obecnych danych...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%DATA%\*' -DestinationPath ('%DATA%_przed_importem_' + (Get-Date -Format 'yyyyMMdd_HHmm') + '.zip') -Force"
)

if not exist "%DATA%" mkdir "%DATA%"
echo  Rozpakowywanie...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Expand-Archive -Path '%ZIP%' -DestinationPath '%DATA%' -Force"
if errorlevel 1 (
  echo  Nie udalo sie rozpakowac archiwum.
  pause
  exit /b 1
)

rem na laptopie program ma sie uruchamiac z Windows; folder kopii ze starego komputera nie pasuje - do ustawienia w panelu
powershell -NoProfile -ExecutionPolicy Bypass -Command "$f='%DATA%\ustawienia.txt'; if (Test-Path $f) { $t=[IO.File]::ReadAllText($f); $t=$t -replace 'autostart=0','autostart=1'; $t=$t -replace '(?m)^folder_kopii=[^\r\n]*','folder_kopii='; [IO.File]::WriteAllText($f, $t, (New-Object Text.UTF8Encoding($true))) }"
rem znacznik pracy ze starego komputera nie dotyczy laptopa
if exist "%DATA%\ostatnio_aktywny.txt" del "%DATA%\ostatnio_aktywny.txt"

echo  Uruchamianie programu...
explorer.exe "%ROOT%\EwidencjaCzasu.exe"
echo.
echo  GOTOWE. Dane zaimportowane. Pamietaj ustawic folder kopii zapasowej
echo  w panelu: Ustawienia -^> Kopia zapasowa.
echo.
pause
