Prebuilt files embedded into the one-click installer
====================================================

sekiro_coop.dll
  The 1.0.8-beta public build ("Sekiro Together"), i.e. the DLL the installer
  embeds. It contains the Chinese in-game settings (native page + Mod settings
  panel) and the unlimited_spirit_emblems feature. The Rust source in this tree
  is still the older 1.0.5-beta tree, so building from source produces a 1.0.5
  DLL; drop your own sekiro_coop.dll here to override the embedded one. The
  upstream license is in LICENSE-sekiro-coop.txt.

libstdc++-6.dll / libgcc_s_seh-1.dll / libwinpthread-1.dll
  MinGW runtime DLLs required by the GNU-built co-op DLL. The installer writes
  them next to sekiro_coop.dll. A local MSVC-built sekiro_coop.dll next to the
  installer takes priority and does not need these, but they are harmless.

me3-windows-amd64.zip
  me3 v0.13.0 Windows release (MIT/Apache-2.0), extracted to
  %LOCALAPPDATA%\Programs\garyttierney\me3 when me3 is not installed.

LICENSE-sekiro-coop.txt
  Upstream co-op mod license.
