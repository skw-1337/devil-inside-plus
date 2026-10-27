@echo off
rem Compile Devil Inside Plus avec le csc fourni par Windows (.NET Framework 4)
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
set FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
"%CSC%" /nologo /target:winexe /platform:x86 /optimize+ /codepage:65001 /out:DevilInsidePlus.exe /win32icon:src\devil-inside-plus.ico /win32manifest:src\app.manifest /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:"%FW%\System.IO.Compression.dll" /r:"%FW%\System.IO.Compression.FileSystem.dll" src\*.cs
exit /b %ERRORLEVEL%
