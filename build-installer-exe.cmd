@echo off
rem Builds the single-file GUI installer install.exe and its launcher.
rem Uses the C# compiler that ships with Windows (.NET Framework 4.x), so no
rem Visual Studio is needed. The GUI embeds the upstream co-op DLL, me3 and the
rem default settings file; the user only needs install.exe.
setlocal
set "ROOT=%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo csc.exe not found: install the .NET Framework 4.x.
  exit /b 1
)

if not exist "%ROOT%prebuilt\sekiro_coop.dll" (
  echo missing prebuilt\sekiro_coop.dll
  exit /b 1
)
if not exist "%ROOT%prebuilt\me3-windows-amd64.zip" (
  echo missing prebuilt\me3-windows-amd64.zip
  exit /b 1
)

echo Building SekiroCoopLauncher.exe...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 ^
  /out:"%ROOT%installer\SekiroCoopLauncher.exe" ^
  "%ROOT%installer\Launcher.cs" || exit /b 1

echo Building install.exe...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 ^
  /out:"%ROOT%install.exe" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll ^
  /resource:"%ROOT%installer\SekiroCoopLauncher.exe",SekiroCoopLauncher.exe ^
  /resource:"%ROOT%prebuilt\sekiro_coop.dll",sekiro_coop.dll ^
  /resource:"%ROOT%prebuilt\libstdc++-6.dll",libstdc++-6.dll ^
  /resource:"%ROOT%prebuilt\libgcc_s_seh-1.dll",libgcc_s_seh-1.dll ^
  /resource:"%ROOT%prebuilt\libwinpthread-1.dll",libwinpthread-1.dll ^
  /resource:"%ROOT%prebuilt\me3-windows-amd64.zip",me3-windows-amd64.zip ^
  /resource:"%ROOT%installer\default-settings.ini",default-settings.ini ^
  "%ROOT%installer\InstallerGui.cs" || exit /b 1

echo valmis: %ROOT%install.exe
