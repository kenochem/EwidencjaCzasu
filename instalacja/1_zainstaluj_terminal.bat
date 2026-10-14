@echo off
rem ===========================================================================
rem  Ewidencja Czasu - instalacja TERMINALA (laptop przy wejsciu z czytnikiem)
rem  Uruchom na laptopie: prawy klik -> "Uruchom jako administrator".
rem
rem  Co robi:
rem   1. kompiluje program z kodu zrodlowego,
rem   2. ustawia zasilanie: nigdy nie usypiaj / nie hibernuj, zamkniecie klapy
rem      nic nie robi, porty USB nie sa usypiane (czytnik zawsze aktywny),
rem   3. ustawia godziny aktywnego uzytkowania Windows Update 6:00-23:00,
rem   4. tworzy folder danych i udostepnia go w sieci (dla panelu na innym PC),
rem   5. wlacza udostepnianie plikow w zaporze dla sieci prywatnej,
rem   6. uruchamia program (sam doda sie do autostartu).
rem ===========================================================================
setlocal
title Ewidencja Czasu - instalacja terminala

net session >nul 2>&1
if errorlevel 1 (
  echo.
  echo  Ten skrypt wymaga uprawnien administratora.
  echo  Kliknij go prawym przyciskiem i wybierz "Uruchom jako administrator".
  echo.
  pause
  exit /b 1
)

cd /d "%~dp0.."
set "ROOT=%CD%"
set "DATA=%USERPROFILE%\Documents\EwidencjaCzasu"

echo.
echo  Folder programu : %ROOT%
echo  Folder danych   : %DATA%
echo  Komputer        : %COMPUTERNAME%
echo  Uzytkownik      : %USERNAME%
echo.

echo [1/6] Kompilacja programu...
call "%ROOT%\zrodla\kompiluj.bat" /cicho
if errorlevel 1 (
  echo  Kompilacja nie powiodla sie - sprawdz komunikaty powyzej.
  pause
  exit /b 1
)

echo.
echo [2/6] Zasilanie: bez usypiania, klapa nic nie robi, USB zawsze aktywne...
powercfg /change standby-timeout-ac 0
powercfg /change standby-timeout-dc 0
powercfg /change hibernate-timeout-ac 0
powercfg /change hibernate-timeout-dc 0
powercfg /change monitor-timeout-ac 10
powercfg /change monitor-timeout-dc 5
rem zamkniecie klapy: 0 = nic nie rob
powercfg /setacvalueindex SCHEME_CURRENT SUB_BUTTONS LIDACTION 0
powercfg /setdcvalueindex SCHEME_CURRENT SUB_BUTTONS LIDACTION 0
rem selektywne wstrzymywanie USB: 0 = wylaczone (czytnik nie bedzie usypiany)
powercfg /setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0
powercfg /setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0
powercfg /setactive SCHEME_CURRENT

echo.
echo [3/6] Windows Update: godziny aktywnego uzytkowania 6:00-23:00...
reg add "HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" /v ActiveHoursStart /t REG_DWORD /d 6 /f >nul
reg add "HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" /v ActiveHoursEnd /t REG_DWORD /d 23 /f >nul
reg add "HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" /v SmartActiveHoursState /t REG_DWORD /d 0 /f >nul

echo.
echo [4/6] Folder danych i udostepnienie w sieci (\\%COMPUTERNAME%\EwidencjaCzasu)...
if not exist "%DATA%" mkdir "%DATA%"
net share EwidencjaCzasu /delete /y >nul 2>&1
net share EwidencjaCzasu="%DATA%" /GRANT:"%USERNAME%",CHANGE /REMARK:"Ewidencja Czasu - dane terminala"
if errorlevel 1 echo  UWAGA: nie udalo sie udostepnic folderu. Panel na innym komputerze nie bedzie mial dostepu.

echo.
echo [5/6] Zapora: udostepnianie plikow w sieci prywatnej...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-NetFirewallRule -Group '@FirewallAPI.dll,-28502' | Where-Object { $_.Profile -match 'Private' } | Enable-NetFirewallRule"
echo.
echo  Aby panel na innym komputerze widzial laptopa, siec firmowa musi byc ustawiona jako PRYWATNA.
choice /c TN /m " Czy ustawic obecna siec jako prywatna (tylko jesli to siec firmowa)"
if errorlevel 2 goto pomin_siec
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-NetConnectionProfile | Set-NetConnectionProfile -NetworkCategory Private"
:pomin_siec

echo.
echo [6/6] Uruchamianie programu (doda sie do autostartu)...
rem przez explorer, zeby program nie dzialal z uprawnieniami administratora
explorer.exe "%ROOT%\EwidencjaCzasu.exe"

echo.
echo ===========================================================================
echo  GOTOWE. Pozostale kroki (opisane w docs\LAPTOP.md):
echo.
echo   * Automatyczne logowanie po restarcie (bardzo wazne!):
echo       Win+R -^> netplwiz -^> odznacz "Aby uzywac tego komputera, uzytkownicy
echo       musza wprowadzic nazwe uzytkownika i haslo" -^> OK -^> podaj haslo.
echo   * Podlacz czytnik do laptopa i przyloz karte - powinno pojawic sie powitanie.
echo   * W panelu: Ustawienia -^> folder kopii zapasowej (np. Dysk Google).
echo.
echo  Dane do podlaczenia panelu na Twoim komputerze:
echo       nazwa laptopa : %COMPUTERNAME%
echo       uzytkownik    : %USERNAME%
echo       folder        : \\%COMPUTERNAME%\EwidencjaCzasu
echo ===========================================================================
echo.
pause
