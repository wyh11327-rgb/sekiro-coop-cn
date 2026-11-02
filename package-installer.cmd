@echo off
rem Packs the self-contained GUI installer for friends:
rem   dist\one-click\sekiro-coop-one-click.zip
rem Only the C# compiler that ships with Windows is needed; no Rust / Visual Studio.
setlocal
call "%~dp0build-installer-exe.cmd" || exit /b 1
set "OUT=%~dp0dist\one-click"
set "PKG=%OUT%\sekiro-coop-one-click"
if exist "%PKG%" rmdir /s /q "%PKG%"
mkdir "%PKG%" || exit /b 1

rem The installer exe has a Chinese name; copy it from Unicode code points so this
rem batch file stays pure ASCII.
set "SRCDIR=%~dp0"
set "PKGDEST=%PKG%"
powershell -NoProfile -Command "$n = -join ([char]0x4E00,[char]0x952E,[char]0x5B89,[char]0x88C5) + '.exe'; Copy-Item -LiteralPath ($env:SRCDIR + $n) -Destination $env:PKGDEST -Force" || exit /b 1
copy /y "%~dp0README-INSTALL.txt" "%PKG%\README-INSTALL.txt" >nul || exit /b 1
copy /y "%~dp0ship-public\HOW-TO-PLAY.txt" "%PKG%\HOW-TO-PLAY.txt" >nul || exit /b 1
copy /y "%~dp0ship-public\LICENSE.txt" "%PKG%\LICENSE.txt" >nul || exit /b 1
copy /y "%~dp0prebuilt\LICENSE-sekiro-coop.txt" "%PKG%\LICENSE-sekiro-coop.txt" >nul || exit /b 1

rem Windows tar, not Git's GNU tar, keeps forward-slash paths.
pushd "%OUT%" || exit /b 1
if exist "sekiro-coop-one-click.zip" del "sekiro-coop-one-click.zip"
"%SystemRoot%\System32\tar.exe" -a -c -f "sekiro-coop-one-click.zip" "sekiro-coop-one-click" || (popd & exit /b 1)
popd
echo valmis: %OUT%\sekiro-coop-one-click.zip
