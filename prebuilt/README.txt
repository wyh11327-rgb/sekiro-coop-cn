Prebuilt files embedded into install.exe
=======================================

sekiro_coop.dll
  Built from this source tree with the Rust GNU toolchain. It contains the
  Chinese in-game settings (native page + Mod settings panel) and the
  unlimited_spirit_emblems feature. The upstream license is in
  LICENSE-sekiro-coop.txt.

libstdc++-6.dll / libgcc_s_seh-1.dll / libwinpthread-1.dll
  MinGW runtime DLLs required by the GNU-built co-op DLL. install.exe writes
  them next to sekiro_coop.dll. A local MSVC-built sekiro_coop.dll next to
  install.exe takes priority and does not need these, but they are harmless.

me3-windows-amd64.zip
  me3 v0.13.0 Windows release (MIT/Apache-2.0), extracted to
  %LOCALAPPDATA%\Programs\garyttierney\me3 when me3 is not installed.

LICENSE-sekiro-coop.txt
  Upstream co-op mod license.
