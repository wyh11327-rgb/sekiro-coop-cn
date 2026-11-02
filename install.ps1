<#
.SYNOPSIS
  One-click Windows installer for Sekiro Seamless Co-op with unlimited Spirit Emblems.

.DESCRIPTION
  - Finds Sekiro (Steam registry + libraryfolders.vdf) unless -GameDir is given.
  - Uses a sekiro_coop.dll next to this script, or builds one with build-dist.cmd/cargo.
  - Installs me3 to %LOCALAPPDATA%\Programs\garyttierney\me3 if it is missing.
  - Installs the mod outside the game folder (%LOCALAPPDATA%\SekiroCoop by default).
  - Writes sekiro_coop_settings.ini with the password and unlimited_spirit_emblems = 1.
  - Creates a "Sekiro Co-op" desktop shortcut that starts the game through me3.

.EXAMPLE
  .\一键安装.exe
  .\一键安装.exe -Password "our-secret" -PlayerName "Wolf"
  .\一键安装.exe -CheckOnly -GameDir "D:\SteamLibrary\steamapps\common\Sekiro"
#>
[CmdletBinding()]
param(
    [string]$GameDir = '',
    [string]$SteamDir = '',
    [string]$InstallDir = '',
    [string]$RepoRoot = '',
    [string]$DefaultIni = '',
    [string]$Password = '',
    [string]$PlayerName = '',
    [switch]$SkipMe3,
    [switch]$NoShortcut,
    [switch]$CheckOnly,
    [switch]$RemoveConflicts,
    [switch]$AllowOldDll,
    [switch]$Launch,
    [switch]$Help
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
if ($Help) {
    Get-Help -Name $PSCommandPath -Full
    exit 0
}
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
} catch {
    # Older .NET: TLS 1.2 is usually already available; the download reports its own error.
}

$script:RepoRoot = if ($RepoRoot) { [IO.Path]::GetFullPath($RepoRoot) } else { $PSScriptRoot }
$script:DefaultIni = if ($DefaultIni) { [IO.Path]::GetFullPath($DefaultIni) } else { Join-Path $script:RepoRoot 'installer\default-settings.ini' }
$script:SupportedExeSize = 68005144  # Steam Sekiro 1.06 (1.6.0.0), see game.rs.
$script:Me3Api = 'https://api.github.com/repos/garyttierney/me3/releases/latest'
$script:Me3Asset = 'me3-windows-amd64.zip'
$script:ModDll = 'sekiro_coop.dll'

function Write-Step([string]$Text) {
    Write-Host "==> $Text" -ForegroundColor Cyan
}

function Write-Ok([string]$Text) {
    Write-Host "    $Text" -ForegroundColor Green
}

function Write-Warn([string]$Text) {
    Write-Host "    WARNING: $Text" -ForegroundColor Yellow
}

function Throw-Help([string]$Text) {
    throw "$Text`nSee the install section in README.md, or run: 一键安装.exe -Help"
}

function Find-SekiroDir([string]$Explicit) {
    $candidates = New-Object System.Collections.Generic.List[string]

    if ($Explicit) {
        $candidates.Add($Explicit)
    } else {
        $steam = $SteamDir
        if (-not $steam) {
            foreach ($key in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam')) {
                $item = Get-ItemProperty -Path $key -ErrorAction SilentlyContinue
                if ($item) {
                    foreach ($name in @('SteamPath', 'InstallPath')) {
                        $value = $item.$name
                        if ($value) {
                            $steam = $value
                            break
                        }
                    }
                }
                if ($steam) { break }
            }
        }

        if ($steam) {
            $steam = ($steam -replace '/', '\').TrimEnd('\')
            # A Steam library entry can point at a drive that is no longer mounted
            # (the screenshot's E:): Join-Path throws then, so each path is guarded.
            try { $candidates.Add((Join-Path $steam 'steamapps\common\Sekiro')) } catch { }
            try {
                $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
                if (Test-Path -LiteralPath $vdf) {
                    $raw = Get-Content -Raw -LiteralPath $vdf
                    foreach ($m in [regex]::Matches($raw, '"path"\s+"([^"]+)"')) {
                        $library = $m.Groups[1].Value -replace '\\\\', '\'
                        try { $candidates.Add((Join-Path $library 'steamapps\common\Sekiro')) } catch { }
                    }
                }
            } catch { }
        }
    }

    foreach ($candidate in $candidates) {
        if (-not $candidate) { continue }
        try {
            $exe = Join-Path $candidate 'sekiro.exe'
            if (Test-Path -LiteralPath $exe) {
                return (Resolve-Path -LiteralPath $candidate).Path
            }
        } catch { }
    }
    return $null
}

function Find-ModDll {
    $candidates = @(
        (Join-Path $script:RepoRoot $script:ModDll),
        (Join-Path $script:RepoRoot ('target-dist\release\' + $script:ModDll)),
        (Join-Path $script:RepoRoot ('target\release\' + $script:ModDll)),
        (Join-Path $script:RepoRoot ('dist\sekiro-coop\' + $script:ModDll))
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    if ($CheckOnly) {
        return $null
    }

    $buildScript = Join-Path $script:RepoRoot 'build-dist.cmd'
    if (Test-Path -LiteralPath $buildScript) {
        Write-Step 'No built DLL found; building with build-dist.cmd (needs Rust + VS 2022 Build Tools)'
        Push-Location $script:RepoRoot
        try {
            & cmd.exe /c build-dist.cmd
            if ($LASTEXITCODE -ne 0) {
                Write-Warn "build-dist.cmd failed (exit $LASTEXITCODE)"
            }
        } catch {
            Write-Warn "build-dist.cmd failed: $($_.Exception.Message)"
        } finally {
            Pop-Location
        }
        $candidate = Join-Path $script:RepoRoot ('target-dist\release\' + $script:ModDll)
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    $cargo = Get-Command cargo.exe -ErrorAction SilentlyContinue
    if ($cargo) {
        Write-Step 'Trying a direct cargo build'
        Push-Location $script:RepoRoot
        try {
            & $cargo.Source build --release -p sekiro_coop
            if ($LASTEXITCODE -ne 0) {
                Write-Warn "cargo build failed (exit $LASTEXITCODE)"
            }
        } catch {
            Write-Warn "cargo build failed: $($_.Exception.Message)"
        } finally {
            Pop-Location
        }
        $candidate = Join-Path $script:RepoRoot ('target\release\' + $script:ModDll)
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    return $null
}

function Find-Me3([switch]$NoDownload) {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\garyttierney\me3\bin\me3.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\me3\bin\me3.exe'),
        (Join-Path $env:ProgramFiles 'me3\bin\me3.exe')
    )
    $command = Get-Command me3.exe -ErrorAction SilentlyContinue
    if ($command) {
        $candidates += $command.Source
    }
    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    if ($NoDownload) {
        return $null
    }

    Write-Step 'me3 is missing; downloading the latest Windows build from GitHub'
    $api = Invoke-RestMethod -Uri $script:Me3Api -Headers @{ 'User-Agent' = 'sekiro-coop-one-click-installer' }
    $asset = $api.assets | Where-Object { $_.name -eq $script:Me3Asset } | Select-Object -First 1
    if (-not $asset) {
        $asset = $api.assets | Where-Object { $_.name -like '*windows*' -and $_.name -like '*.zip' } | Select-Object -First 1
    }
    if (-not $asset) {
        Throw-Help "The latest me3 release has no $($script:Me3Asset). Download me3 manually: https://github.com/garyttierney/me3/releases"
    }

    $target = Join-Path $env:LOCALAPPDATA 'Programs\garyttierney\me3'
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    $zip = Join-Path $env:TEMP ('me3-' + [guid]::NewGuid().ToString('N') + '.zip')
    try {
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip -UseBasicParsing
        Expand-Archive -Path $zip -DestinationPath $target -Force
    } catch {
        Throw-Help "Could not download or unpack me3: $($_.Exception.Message)`nDownload it manually: https://github.com/garyttierney/me3/releases"
    } finally {
        Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue
    }

    $found = Get-ChildItem -LiteralPath $target -Filter 'me3.exe' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $found) {
        Throw-Help "me3 was unpacked to $target but me3.exe was not found."
    }
    return $found.FullName
}

# Replaces the first active "key = value" line. Comments are left alone; a missing key is
# appended (the mod finds keys in any section).
function Set-IniValue([string]$Text, [string]$Key, [string]$Value) {
    $regex = [regex]::new('(?im)^[ \t]*' + [regex]::Escape($Key) + '[ \t]*=[ \t]*.*$')
    $match = $regex.Match($Text)
    if ($match.Success) {
        return $Text.Substring(0, $match.Index) + $Key + ' = ' + $Value + $Text.Substring($match.Index + $match.Length)
    }
    $eol = if ($Text.Contains("`r`n")) { "`r`n" } else { "`n" }
    if ($Text.Length -gt 0 -and -not $Text.EndsWith("`n")) {
        $Text += $eol
    }
    return $Text + $Key + ' = ' + $Value + $eol
}

function Write-Profile([string]$Path) {
    $profile = @'
profileVersion = "v1"
# Co-op uses its own save (copied from the normal one on first launch).
savefile = "S0000.coop.sl2"

[[supports]]
game = "sekiro"

[[natives]]
path = "sekiro_coop.dll"
optional = false
enabled = true
'@
    Set-Content -LiteralPath $Path -Value $profile -Encoding ASCII
}

function Write-Settings([string]$Path, [string]$PasswordValue, [string]$NameValue) {
    $template = $script:DefaultIni
    if (Test-Path -LiteralPath $Path) {
        $text = Get-Content -Raw -LiteralPath $Path
        if ($PasswordValue) {
            $text = Set-IniValue $text 'password' $PasswordValue
        }
        if ($NameValue) {
            $text = Set-IniValue $text 'player_name' $NameValue
        }
    } elseif (Test-Path -LiteralPath $template) {
        $text = Get-Content -Raw -LiteralPath $template
        $text = $text.Replace('@PASSWORD@', $PasswordValue).Replace('@NAME@', $NameValue)
        $text = $text.Replace('@TRANSPORT@', 'auto').Replace('@HOST@', '')
    } else {
        $text = @"
; Sekiro seamless co-op settings (written by the one-click installer).
password = $PasswordValue
player_name = $NameValue

[GAMEPLAY]
unlimited_spirit_emblems = 1
"@
    }
    $text = Set-IniValue $text 'unlimited_spirit_emblems' '1'
    Set-Content -LiteralPath $Path -Value $text -Encoding UTF8
}

function New-Shortcut([string]$ShortcutPath, [string]$Me3Path, [string]$ProfilePath, [string]$GameExe, [string]$WorkDir) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $Me3Path
    $shortcut.Arguments = 'launch --game sekiro --profile "' + $ProfilePath + '"'
    $shortcut.WorkingDirectory = $WorkDir
    $shortcut.Description = 'Sekiro Seamless Co-op'
    if (Test-Path -LiteralPath $GameExe) {
        $shortcut.IconLocation = $GameExe + ',0'
    }
    $shortcut.Save()
}

# --- 1. Find the game -------------------------------------------------------------------
Write-Step 'Looking for Sekiro'
$gameDir = Find-SekiroDir $GameDir
if (-not $gameDir) {
    if ($CheckOnly) {
        Throw-Help 'Sekiro was not found. Pass -GameDir "D:\...\steamapps\common\Sekiro".'
    }
    $GameDir = Read-Host 'Sekiro folder (the one that contains sekiro.exe)'
    $gameDir = Find-SekiroDir $GameDir
    if (-not $gameDir) {
        Throw-Help 'That folder does not contain sekiro.exe.'
    }
}
$gameExe = Join-Path $gameDir 'sekiro.exe'
Write-Ok $gameDir

$exeSize = (Get-Item -LiteralPath $gameExe).Length
if ($exeSize -ne $script:SupportedExeSize) {
    Write-Warn "sekiro.exe is $exeSize bytes; the mod supports Steam 1.06 ($($script:SupportedExeSize) bytes)."
    Write-Warn 'Verify the game in Steam first (Properties > Installed Files > Verify integrity).'
}

# --- 2. Find/build the mod DLL ----------------------------------------------------------
Write-Step 'Looking for sekiro_coop.dll'
$dll = Find-ModDll
if (-not $dll) {
    if ($CheckOnly) {
        Write-Warn 'No sekiro_coop.dll found; the installer would build it (Rust + VS 2022 Build Tools).'
    } else {
        Throw-Help @'
sekiro_coop.dll was not found and could not be built.

Put a prebuilt sekiro_coop.dll next to 一键安装.exe, or install:
  - Rust stable: https://rustup.rs
  - Visual Studio 2022 Build Tools with "Desktop development with C++"
Then run 一键安装.exe again. build-dist.cmd also expects
"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools".
'@
    }
} else {
    Write-Ok $dll
    $ascii = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($dll))
    if ($ascii -notmatch 'unlimited_spirit_emblems') {
        Write-Warn 'This DLL does not contain the unlimited_spirit_emblems feature (older build).'
        if (-not $AllowOldDll -and -not $CheckOnly) {
            Throw-Help @'
Refusing to install a co-op DLL without the unlimited_spirit_emblems feature.
Rebuild it from this source tree (build-dist.cmd), or pass -AllowOldDll if you
only want the plain co-op mod.
'@
        }
    }
}

# --- 3. Find/install me3 ----------------------------------------------------------------
Write-Step 'Looking for me3'
$me3 = $null
if ($SkipMe3) {
    $me3 = Find-Me3 -NoDownload
    if ($me3) {
        Write-Ok $me3
        Write-Warn 'Download skipped (-SkipMe3): using the me3 already on this PC.'
    } else {
        Write-Warn 'Skipped (-SkipMe3) and me3 was not found; no shortcut/launcher will work.'
    }
} else {
    $me3 = Find-Me3 -NoDownload:$CheckOnly
    if ($me3) {
        Write-Ok $me3
    } elseif ($CheckOnly) {
        Write-Warn 'me3 is missing; the installer would download it from GitHub.'
    } else {
        Throw-Help 'me3 was not installed and the automatic download failed.'
    }
}

if ($CheckOnly) {
    $installDir = if ($InstallDir) { $InstallDir } else { Join-Path $env:LOCALAPPDATA 'SekiroCoop' }
    $installDir = [IO.Path]::GetFullPath($installDir)
    Write-Host ''
    Write-Host 'Check only: nothing was written.' -ForegroundColor Green
    Write-Host "  game:      $gameDir"
    Write-Host "  dll:       $(if ($dll) { $dll } else { '(would build)' })"
    Write-Host "  me3:       $(if ($me3) { $me3 } else { '(would install)' })"
    Write-Host "  install:   $installDir"
    Write-Host '  feature:   unlimited_spirit_emblems = 1'
    exit 0
}

# --- 4. Destination ---------------------------------------------------------------------
if (-not $InstallDir) {
    $InstallDir = Join-Path $env:LOCALAPPDATA 'SekiroCoop'
}
$InstallDir = [IO.Path]::GetFullPath($InstallDir)
if ($InstallDir.TrimEnd('\').StartsWith($gameDir.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) {
    Throw-Help 'InstallDir must not be inside the game folder (the mod must run from its own folder).'
}
if (Get-Process -Name 'sekiro' -ErrorAction SilentlyContinue) {
    Throw-Help 'Sekiro is running. Close it before installing.'
}
if (-not $Password -and -not $PSBoundParameters.ContainsKey('Password')) {
    $Password = Read-Host 'Co-op password (same for every player; Enter = leave empty)'
}
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

# --- 5. Other online mods that must go --------------------------------------------------
$conflicts = @('dinput8.dll', 'steam_api64.org', 'cream_api.ini', 'SekiroOnlineFont.ttf') |
    ForEach-Object { Join-Path $gameDir $_ } |
    Where-Object { Test-Path -LiteralPath $_ }
if ($conflicts) {
    if ($RemoveConflicts) {
        foreach ($file in $conflicts) {
            Remove-Item -LiteralPath $file -Force
            Write-Ok "Removed $file"
        }
    } else {
        Write-Warn 'Other online-mod files were found and were NOT removed:'
        foreach ($file in $conflicts) { Write-Warn "  $file" }
        Write-Warn 'Remove them, then in Steam use Properties > Installed Files > Verify integrity,'
        Write-Warn 'or rerun with -RemoveConflicts.'
    }
}

# --- 6. Copy the mod --------------------------------------------------------------------
Write-Step 'Installing the co-op mod'
Copy-Item -LiteralPath $dll -Destination (Join-Path $InstallDir $script:ModDll) -Force
$profilePath = Join-Path $InstallDir 'sekiro-coop.me3'
Write-Profile $profilePath
$iniPath = Join-Path $InstallDir 'sekiro_coop_settings.ini'
Write-Settings $iniPath $Password $PlayerName
Write-Ok $InstallDir
Write-Ok 'sekiro_coop_settings.ini: unlimited_spirit_emblems = 1'

# Launcher fallback for people who do not use the shortcut.
$launcherPath = Join-Path $InstallDir 'Play Sekiro Co-op.cmd'
if ($me3) {
    $launcher = @"
@echo off
setlocal
set "ME3=$me3"
if not exist "%ME3%" (
  echo me3 not found at %ME3%
  echo Install me3 from https://github.com/garyttierney/me3/releases
  pause
  exit /b 1
)
"%ME3%" launch --game sekiro --profile "%~dp0sekiro-coop.me3"
if errorlevel 1 pause
"@
    Set-Content -LiteralPath $launcherPath -Value $launcher -Encoding Default
}

# --- 7. Shortcut ------------------------------------------------------------------------
if (-not $NoShortcut -and $me3) {
    Write-Step 'Creating the desktop shortcut'
    try {
        $desktop = [Environment]::GetFolderPath('Desktop')
        $shortcutPath = Join-Path $desktop 'Sekiro Co-op.lnk'
        New-Shortcut $shortcutPath $me3 $profilePath $gameExe $InstallDir
        Write-Ok $shortcutPath
    } catch {
        Write-Warn "Could not create the desktop shortcut: $($_.Exception.Message)"
        Write-Warn "Use $launcherPath instead."
    }
}

if (-not (Get-Process -Name 'steam' -ErrorAction SilentlyContinue)) {
    Write-Warn 'Steam is not running. Start Steam before using the shortcut.'
}

# --- 8. Summary -------------------------------------------------------------------------
Write-Host ''
Write-Host 'Installed.' -ForegroundColor Green
Write-Host "  Mod:      $InstallDir"
Write-Host "  Settings: $iniPath"
Write-Host "  Password: $(if ($Password) { 'set' } else { 'NOT SET (co-op stays off until you set it)' })"
Write-Host '  Spirit Emblems: unlimited (set unlimited_spirit_emblems = 0 to restore normal use)'
if (-not $NoShortcut -and $me3) {
    Write-Host '  Start:    desktop shortcut "Sekiro Co-op"'
}
Write-Host '  Reminder: everyone needs the same password and the same mod version.'

if ($Launch -and $me3) {
    Write-Step 'Starting Sekiro through me3'
    Start-Process -FilePath $me3 -ArgumentList ('launch --game sekiro --profile "' + $profilePath + '"') -WorkingDirectory $InstallDir
}
