# Tools: checking the generator against your own savegames

These scripts are optional. The app does not need them. They exist so that anyone who owns the game can test the offline generator against a real map: create a map in the game with a known seed and settings, save it, and let the tools compare what the game made with what the app predicts.

No savegames and no game files are included in this repository. You bring your own.

## What you need

- Python 3.10 or newer (standard library only, nothing to install).
- A built `Anno117SeedFinder.exe` (see the main README, "Building it yourself"). The tools look for it in
  `src/Anno117SeedFinder/bin/Release|Debug/...`; otherwise pass `--exe` or set `ANNO_SEEDFINDER_EXE`.
- For reading savegames: **RdaConsole** and **FileDBReader**, two community tools for Anno file formats (search for them in the `anno-mods` organisation on GitHub). Put the two `.exe` files (and their DLLs) into `tools/bin/`, or anywhere on your `PATH`, or pass `--rda` / `--fdb`.

## Check savegames: `check_savegame.py`

```
python check_savegame.py "D:\saves\Archipelago_Large_1204.a8s"
python check_savegame.py "D:\saves"                # every .a8s in a folder
python check_savegame.py tools\dumps               # savegames that were extracted before
```

For every savegame it reads the map out of the file, runs the app for the same settings and compares, separately for Latium and Albion:

| Column | What is compared |
|---|---|
| `isl` | type, rotation and position of every island (Cinis included) |
| `3rd` | the traders and the raider |
| `deco` | every decoration island with rotation and position |
| `fert` | the fertilities of every island (as a set, order does not matter) |
| `slot` | the fertilities plus the number of active mine and river/marsh slots of every island |

A savegame passes when all counts match; the exit status is 0 only if all savegames pass. `--verbose` lists what differs ("in the savegame only" = the game has it and the app does not produce it, "in the app only" = the other way round).

### Telling the tool which settings a savegame has

Name the file (or the dump folder) like this; separators can be anything that is not a letter or digit, case does not matter:

```
<Template>_<Size>_<Seed>[_nodlc | _retro][_regular | _sparse][_slots_<abundant|regular|sparse>]
```

| Part | Values |
|---|---|
| Template | `Archipelago`, `Atoll`, `Rift`, `Corners`, `IslandChains` |
| Size | `Large`, `Medium`, `Small` (may be left out: the tool then finds the size whose island count fits) |
| Seed | the map seed |
| DLC | nothing = Prophecies of Ash on; `nodlc` = the map was created without it; `retro` = activated afterwards |
| Fertility | `regular` or `sparse` (nothing = abundant) |
| Slots | `slots_regular` or `slots_sparse` (the resource-deposit option; nothing = abundant) |

Examples: `Archipelago_Large_1204.a8s`, `Corners_Small_15_nodlc_sparse.a8s`, `Atoll_Large_1_retro.a8s`.
For a single savegame you can give the settings on the command line instead:
`--template Corners --size Small --seed 15 --dlc off --fertility sparse --slots regular`.

Things the game options do **not** change (checked): the Woods option and the start mode (flagship / starting island).

## The other scripts

| Script | Purpose |
|---|---|
| `savegame_map_dump.py` | extracts the map of one savegame into `tools/dumps/<name>/` (the step `check_savegame.py` runs for you) |
| `savegame_header.py` | prints the map size, island shift, playable area and enlargement offset stored in a savegame, the exact answer to "how big is this map and where is it" (see `CAVEATS.md`) |
| `read_map_templates.py` | prints the player start points of the game's `.a7tinfo` template files, for people who have extracted those |
| `common.py` | shared helpers |

`tools/dumps/` and `tools/bin/` are ignored by git.

## Credits

`savegame_map_dump.py` is adapted from code of the Anno 117 Layout Tool; see `THIRD-PARTY-NOTICES.md`.