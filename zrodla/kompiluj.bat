@echo off
rem Kompiluje program Ewidencja Czasu z plikow zrodlowych (kompilator jest wbudowany w Windows).
rem Wynik: EwidencjaCzasu.exe w folderze glownym repozytorium.
rem Uzycie: kompiluj.bat          - kompilacja z pauza na koncu
rem         kompiluj.bat /cicho   - bez pauzy (wywolanie z innych skryptow)
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Nie znaleziono kompilatora .NET Framework 4 ^(csc.exe^).
  if /i not "%~1"=="/cicho" pause
  exit /b 1
)
taskkill /im EwidencjaCzasu.exe /f >nul 2>&1
"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /r:System.IO.Compression.dll /out:..\EwidencjaCzasu.exe Dane.cs Czytnik.cs Aplikacja.cs Panel.cs Wyglad.cs Grafik.cs
set WYNIK=%ERRORLEVEL%
if %WYNIK% neq 0 (echo BLAD KOMPILACJI) else (echo Gotowe: EwidencjaCzasu.exe)
if /i not "%~1"=="/cicho" pause
exit /b %WYNIK%
