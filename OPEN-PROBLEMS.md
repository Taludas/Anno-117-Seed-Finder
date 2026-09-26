# Open problems

Everything that is not finished. The long working log this file used to contain has been replaced by `CAVEATS.md`, which keeps the findings that are worth remembering; the generator itself is now validated in full (see `README.md`).

Last checked: 2026-09-22.

---

## 1. "DLC activated later" is only exact for measured map sizes

Retroactive DLC works, and all 15 savegames of it match completely — islands, positions, rotations, fertilities and slot counts. But between placing the new islands and rolling their building slots the game consumes a large number of dice rolls that we cannot compute. The count depends on how far the map was enlarged, and it is stored as a measured table for the enlargement sizes that appear in the savegames: −24, 8, 16, 32, 40, 64, 80, 144, 152 and 176.

For an enlargement size outside that list the app falls back to `168 + size / 16`, which matches the measured values for the small sizes but is wrong for 144 and 152. When it is wrong, the **fertilities on the newly added islands** are wrong; the old part of the map stays correct.

**What would close it.** Savegames of retroactively enabled DLC for enlargement sizes not yet in the table, especially in the range 88–136. The enlargement size can be computed for any seed without a savegame, so a list of seeds that hit the missing sizes can be produced on request. Retro mode has also not been tested with the regular and sparse fertility settings.
