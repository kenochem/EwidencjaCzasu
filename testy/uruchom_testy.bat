@echo off
rem Kompiluje i uruchamia testy (logika czytnika, kalendarz, normy, raporty, kopie).
rem Testy pracuja na folderze tymczasowym – nie dotykaja prawdziwych danych.
cd /d "%~dp0"
set OUT=%TEMP%\EwidencjaCzasu_testy.exe
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /target:exe /main:EwidencjaCzasu.Tests /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /out:"%OUT%" ..\zrodla\Dane.cs ..\zrodla\Czytnik.cs ..\zrodla\Aplikacja.cs ..\zrodla\Panel.cs ..\zrodla\Wyglad.cs ..\zrodla\Grafik.cs Testy.cs
if errorlevel 1 (echo BLAD KOMPILACJI & pause & exit /b 1)
chcp 65001 >nul
"%OUT%"
pause
