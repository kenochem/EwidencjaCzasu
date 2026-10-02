@echo off
rem Kompiluje program Ewidencja Czasu z plikow zrodlowych (kompilator jest wbudowany w Windows).
rem Przed kompilacja zamknij dzialajacy program.
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /target:winexe /optimize+ /r:System.IO.Compression.dll /out:..\EwidencjaCzasu.exe Dane.cs Czytnik.cs Aplikacja.cs Panel.cs Wyglad.cs Grafik.cs
if errorlevel 1 (echo BLAD KOMPILACJI) else (echo Gotowe: EwidencjaCzasu.exe)
pause
