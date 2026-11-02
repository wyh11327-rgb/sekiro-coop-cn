# Linux / Steam Deck

Windows is the tested platform. Linux and the Steam Deck should work through
Proton because me3 supports them, but this has not been tested on real hardware
yet. Tell me either way in an issue.

No `WINEDLLOVERRIDES` launch option is needed. That one is only for Sekiro Online.

## Setup

1. Start Sekiro once from Steam normally and quit at the main menu. That creates
   the Proton prefix.
2. Install me3 in a terminal (on the Deck: Desktop Mode, Konsole):

   ```sh
   curl --proto '=https' --tlsv1.2 -sSfL https://github.com/garyttierney/me3/releases/latest/download/installer.sh | sh
   ```

   "me3 is not available on PATH" is fine, `play.sh` also looks in `~/.local/bin`.
3. Unzip the release in your home folder, **not** the game folder, for example
   `~/Games/sekiro-together`.
4. With Steam already running, start the mod from that folder:

   ```sh
   sh play.sh
   ```

   Starting the game from the Steam library button starts it **without** the mod.
5. The rest is the same as on Windows: the ini, the shared password and the three
   co-op items (Kindred Bell, Kindred Charm, Severed Thread).

## Where the files are

`sekiro_coop_settings.ini` sits next to the DLL. The log, the save backups and the
co-op save are inside Sekiro's Proton prefix (`compatdata/814380`), which lives at:

```
~/.local/share/Steam/steamapps/compatdata/814380/pfx/
```

| What | Path inside the prefix |
|---|---|
| log, save backups | `drive_c/users/steamuser/AppData/Local/sekiro-coop/` |
| saves | `drive_c/users/steamuser/AppData/Roaming/Sekiro/` |

## If the game does not start

- Start Steam first, then run `sh play.sh`.
- Check me3 is there: `command -v me3` or `ls ~/.local/bin/me3`.
- The log inside the prefix (`AppData/Local/sekiro-coop/log.txt`) says whether the
  DLL was loaded at all.
