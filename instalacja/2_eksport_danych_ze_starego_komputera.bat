@echo off
rem ===========================================================================
rem  Ewidencja Czasu - EKSPORT danych ze starego komputera
rem  Uruchom na komputerze, na ktorym program dzialal do tej pory.
rem
rem  Co robi:
rem   1. zamyka program i wylacza jego autostart na tym komputerze
rem      (od teraz odbicia rejestruje laptop),
rem   2. pakuje folder danych do pliku EwidencjaCzasu_dane.zip na Pulpicie.
rem  Plik ZIP przenies na laptopa (pendrive / dysk sieciowy / Dysk Google)
rem  i uruchom tam 3_import_danych_na_laptopie.bat.
rem ===========================================================================
setlocal
title Ewidencja Czasu - eksport danych
set "DATA=%USERPROFILE%\Documents\EwidencjaCzasu"
set "ZIP=%USERPROFILE%\Desktop\EwidencjaCzasu_dane.zip"

if not exist "%DATA%\odbicia.csv" (
  echo  Nie znaleziono danych w: %DATA%
  pause
  exit /b 1
)

echo  Program zostanie zamkniety, a jego autostart na tym komputerze wylaczony.
choice /c TN /m " Kontynuowac"
if errorlevel 2 exit /b 0

taskkill /im EwidencjaCzasu.exe /f >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v EwidencjaCzasu /f >nul 2>&1
rem zeby program nie dopisal sie ponownie do autostartu, gdyby ktos go tu uruchomil
powershell -NoProfile -ExecutionPolicy Bypass -Command "$f='%DATA%\ustawienia.txt'; if (Test-Path $f) { $t=[IO.File]::ReadAllText($f); [IO.File]::WriteAllText($f, ($t -replace 'autostart=1','autostart=0'), (New-Object Text.UTF8Encoding($true))) }"

echo  Pakowanie danych...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%DATA%\*' -DestinationPath '%ZIP%' -Force"
if errorlevel 1 (
  echo  Nie udalo sie utworzyc archiwum.
  pause
  exit /b 1
)

echo.
echo  GOTOWE: %ZIP%
echo  Przenies ten plik na Pulpit laptopa i uruchom tam 3_import_danych_na_laptopie.bat.
echo.
pause
