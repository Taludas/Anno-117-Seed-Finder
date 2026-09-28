# Caveats — what tripped us up

Notes from reverse-engineering the Anno 117 map generator from savegames. Almost every one of these cost hours or a wrong turn, and several of them produced rules that looked right for weeks. If you continue this work, read this first.

---

## About the generator itself

### 1. The order of the dice rolls is everything

Each phase consumes a fixed number of rolls. Get the *count* wrong anywhere and everything after it shifts, so the map changes completely. Get a *value* wrong without changing the count and only that one value is wrong.

This has a brutal consequence: a single decorative island placed in the wrong cell makes **the fertility of the whole map wrong**, because the decoration phase sits before the fertility phase. Two Archipelago seeds looked like a "fertility bug" for a long time; the actual fault was one decoration cell.

It also has a pleasant consequence: when you suspect a value is wrong but the count is right, you can change it freely and only that value moves. The slot-count fix (`min(max(fixed, wanted), fixed+random)`) changed many numbers and shifted nothing.

### 2. Fitted constants hide the real structure

Several rules survived a long time as fitted numbers and were all wrong in the same way — they were the sum or the side effect of something real:

| Fitted guess | What it really was |
|---|---|
| "grid 177 + tail 69" draws before decorations | the nudge phase (2 passes × 20 islands × 80 draws) plus the normal grid |
| per-template swap tables for fertility roles | one shuffled role list whose content differs per fertility setting |
| "+8 for deco_01 and deco_02 at rotation 1, DLC only" | a quarter turn is measured with the **opposite** rotation's offset, for every asset |
| "variant draw is Scaled(3) clamped" | a uniform draw over the row's own span; the 1/3 : 2/3 split came from clamping |
| a special Island Chains map shift with a `free==96` case | the same centring rule as every other template |
| Albion start points: a 24-unit box around the island, measured with the opposite rotation's offset | each start point is just one occupied cell in the same grid the decoration scan reads (Latium always did this) |
| Albion map width for Archipelago Large never below 2016 | no floor: the width is always the content span rounded up. Seed 1 (retro save) has width 2000 and exposed it |
| "a candidate whose flip leaves the map falls back to the other axis" | no fallback exists: the game retries at the next cell. The rule was fitted on two old savegames and hid the real start-point rule for months |

**If a constant has no explanation, it is probably a symptom.** Prefer a rule you can state in one sentence about what the engine does.

### 3. Derive from the game's files before you fit

The slot counts were fitted from savegames for weeks: seven numbers per island, 55 islands. The island file (`.a7minfo`) contain the fixed and random slot counts directly, and they reproduced **all 55 fitted definitions exactly** — and immediately gave the two settings we had no data for. Hours of fitting replaced by one extraction script.

Data worth reading directly: island files (slot counts), map template files (slots, start points, specials), assets.xml (`MapGeneratorSlotCount` tables), and the `MapTemplate` header inside every savegame.

### 4. The savegame header is ground truth — use it

Every savegame contains its own `MapTemplate` with `Size`, `IslandShift` and `PlayableArea`, for both regions. That is the exact answer to "how big is this map and where is it", with no fitting.

Both map-shift bugs — Latium's rounding and Albion's, which was 8–16 units off and dragged the whole playable rectangle with it — were found by comparing against this header. The tool is `tools/savegame_header.py`. Check against it before inventing a geometry rule.

### 5. The same engine quirk appears in several places

Rotation 1 is measured with rotation 3's offset in the decoration clearance check. The Albion *player start point* check looked like it did the same thing with a 24-unit border — but that was only a lucky approximation. Fitting the border and the box type against every accepted and every refused cell showed the game simply marks each start point as one occupied grid cell and lets the normal scan window (24 units left and above, 32 right and below) find it. Latium had done it that way all along.

When you find an oddity, look for the other places the same code path is used — and when a second copy of a rule behaves differently, check whether one of the two is a disguised version of the other.

---

## About the game's data

### 6. Files on disk can be older than the game

A patch changed the Latium Island Chains and Rift templates, and the copies in the research folder were from before it. The same thing happened again later with an extraction that mixed old and new files: the base templates were current while the `_dlc01expanded` copies were stale.

**The savegames always win.** Where a template file and the savegames disagree, trust the savegames — they are what the installed game actually produced.

### 6b. Start points ignore the map shift in the decoration pass

On DLC01 maps the player start points block decoration islands at their **template** position, not at their position in the shifted map (maps without DLC01 do use the shifted position) he difference only exists where islands stick out past the map edge (Corners Large seed 27028: a shift of (32, 48)). One wrong blocked cell sent 12 of 14 decorations elsewhere while every island and fertility still matched, because the number of rolls stayed the same.

### 7. Duplicate island names are normal

When an island pool runs out it is refilled, so the same island can appear twice on one map. Any comparison keyed by island name silently collapses them.

This produced a phantom bug: 63 Albion savegames appeared to have "exactly one misplaced island". 62 of them were the comparison script losing a duplicate. **Compare as multisets.**

### 8. The savegame stores different fertility identifiers than the app

For the regular and sparse settings the savegame records the *variant* identifier of a fertility set, while the app works with the *role* identifier. Comparing them directly makes every regular and sparse save look broken.

Compare the **list of goods**, not the set identifier — that is what the long-standing regression script does, and it is why it never showed the problem.

### 9. Savegame file names are not a data source

Two whole analysis rounds were wrong because of file-name parsing:

- `\breg` does not match `large_reg` — in a regular expression `_` counts as a word character, so there is no boundary.
- `spa` is not `spars`, and `l` / `m` / `s` are sizes just as much as `large` / `medium` / `small`.

Both made correct output look like a total failure. Parse names into tokens, and when a whole category fails at once, suspect your own parser before the generator.

---

## About the method

### 10. Check both directions, and check the base rate

There are two ways to disagree with the game: accepting a spot it skipped, and rejecting a spot it used. For a long time only false accepts were measured — the false-reject check was dead code, sitting after the branch that ended the loop, so it never ran. Half the problem was invisible.

And when a signal does appear, measure the control group. "An island sits one cell outside the scan rectangle" looked decisive at 81 % of rejected candidates — until the accepted ones turned out to be at 17 %, which ruled out every simple margin rule.

### 11. A rule that fits a few savegames is not a rule

Many candidate rules fixed one save and broke twenty. Score every change against the whole archive and against both DLC states before keeping it. Several plausible ideas — larger scan margins, dilating the island footprints, using bounding boxes instead of outlines, shifting the scan window — were all rejected this way.

### 12. Chains beat single matches when scanning the dice stream

Matching one island's goods against the stream gives thousands of false positives; the entropy is too low. Matching a *chain* of consecutive results, carrying the bag state along, pins the position uniquely. That is how the retroactive DLC's fertility phase was located in a stream of hundreds of thousands of draws.

### 13. Forcing the truth is the sharpest tool

The most productive technique was a scratch build that forces each decoration onto the cell the savegame shows, and reports every cell the app would have taken earlier. That converts "the map is wrong" into a list of individual decisions with coordinates — the last Albion error was a single line of output.

Keep such probes in a **copy** of the project, never in the app.

### 14. Coverage is not correctness

"All savegames pass" meant little while the archive happened to cover 55 of 90 setting combinations. Count the combinations you actually have before claiming anything. The 90-savegame validation set exists because that count was measured and found wanting.

---

### 15. A "known bad seed" is a rule you have not found yet

Archipelago / Medium / seed 100 stayed wrong for a long time and looked like a one-cell mystery. The way in was to stop looking at that one cell and to measure, for **every** cell in the archive, what the game accepted and refused:

1. Force the game's decisions and count dice draws — decoration 5 consumed exactly two rolls, so the game had drawn the coin flip and *then* refused the cell.
2. Switch off one gate at a time in a scratch build and list which cells the game refused that we would accept. Two independent rules fell out: the axis fallback (which does not exist) and the start-point cells.
3. Fit the rule against both sets — cells the game accepted and cells it refused. Zero errors over 3,150 + 68 cells.

Result: every Albion decoration in every savegame is exact, and seed 100 works.

## About the preview

### 16. Saved positions already include the IslandShift

An island position in a savegame is the template position **plus** the map's `IslandShift`, i.e. it is in the same frame as the saved decorations. The generator works with the unshifted position and adds the shift only where it needs the final frame (`--dump-moved-placements` prints the unshifted value). An older comparison script removed "the most common offset" before comparing positions, which hid the difference for a long time. Anything that puts islands and decorations on one map (the preview) must add the shift; `--dump-layout` does, and `tools/check_savegame.py` checks it against your savegames.

### 17. The island images are drawn north-up

The top-down island images are drawn with their top edge towards +y of the map, that is vertically flipped against the generator's y-down frame. That was found by comparing the shape of each image with the island's mask (centre-of-mass distance 0.042 flipped against 0.075 or more for every other orientation). A rotation of r quarter turns is then r turns counter-clockwise on the north-up picture. An unrotated image shown at -45 degrees is exactly the isometric view of rotation 0.

### 18. Where a game file and the savegames disagree, the savegames win

Two examples that are baked into the code: the base-game Corners Small template lists start points (600,1440)... that break every DLC-off Small save, while the corner points of the DLC01 template fit all of them; and the player start points stored inside a savegame are unusable for Albion because the ship spawns at a third-party harbour when Albion is the second province.

## About the tile counts

### 19. The island tile values have two sources

Total and swamp tiles per island come from the island atlas; harbour tiles come from the game's island files (an `AreaID` of 0x2001/0x4001 on a water tile, the same rule as the layout tool's extractor). The two are not on the same footing: counting the extractor's buildable and marsh tiles gives roughly 2 to 7 percent more than the atlas for most islands, so the advanced fertility filters (harbour tiles or swamp tiles of the islands that carry a fertility) are consistent among themselves and with the atlas for swamp, but harbour tiles are not directly comparable with the area columns. The island files were re-read on the day the filters were added; three islands differ from an older extraction, so the game files change between patches.

## About seed scoring

### 20. A weight on something that is also a hard filter is harmless but empty

If a metric is both a hard filter and weighted, every surviving hit already satisfies the filter, so its normalized value is 1 for all of them: the weight raises every seed's absolute score by the same amount without changing their order. This is expected, not a bug - weighting is meant for metrics you leave *unfiltered* but still prefer (that is the point of the Cinis rows, which score independently of the corresponding hard-filter checkbox).

### 21. The outlier checkbox only rescales the current search's own hits

"Extend the scoring range for outliers" does not touch the underlying 100k-seed statistics tables; it only widens a metric's effective min/max to cover whatever the *currently shown* hits happen to contain, then renormalizes those same hits. Loading a different set of seeds (a new search, a loaded seed list) recomputes it again from scratch.

### 22. A DataGridColumn's HeaderStyle/CellStyle silently breaks the grid's Shift+click multi-sort

Setting `HeaderStyle` or `CellStyle` on one `DataGridColumn` — even just to add a tooltip, with no `BasedOn` — drops that header's default template for the *whole grid*, not just that column, and with it the built-in Shift+click multi-sort. A tooltip on a header or a cell belongs on the `Header`/`CellTemplate` content itself (a plain `TextBlock` with its own `ToolTip`), exactly like every other tooltipped column in this grid already does — never on `HeaderStyle`/`CellStyle`. `SmokeColumnStyles()` in the self-test asserts no column ever has either set again. The one exception is the grid-wide `DataGrid.CellStyle` for the seed-comparison tint (`ResultTableTools.cs`): WPF hands it down to every column's `CellStyle`, so the guard accepts exactly that style, and `SmokeTableTools()` checks that a header click still sorts with it in place.

### 23. A sort key needs the exact value the player sees, not a finer one

The `Score` column's sort key must be *exactly* the rounded value shown in the cell. An earlier version added a tiny extra offset to break ties automatically (favouring whichever metric had the highest weight) — which meant two seeds that *looked* tied at "10.0" almost never had the *same* underlying sort value, so Shift+click on a second column had no real tie left to resolve and appeared to do nothing. Any displayed, rounded value used as a `SortMemberPath` needs its sort key rounded exactly the same way, or the grid's native multi-sort silently loses its purpose.

### 24. The gauge tooltip's range is always the raw 100k population, never the outlier-extended one

Every filter column's tooltip (`RangeGauge`) plots a seed's value against `ScoreMetrics.PopulationRange` directly, computed once when the row is built. It never applies the "extend the scoring range for outliers" adjustment `SeedScoring.Apply` makes for the *score* — that adjustment is scoped to one search's scoring pass and would need recomputing whenever the outlier checkbox or the result set changes, for a number the tooltip's job (context against the general population) doesn't call for. A hit that is itself the outlier will show a bar pinned at the very edge (or a hair past it, clamped) — expected, not a bug.

### 25. The output file's on-disk name is not what the "Output file" box says

`RunSearch` stamps the search's start time into the actual file name (`TimestampedFileName`) before it ever reaches `SeedSearcher` — the box keeps showing the plain name the player typed or browsed to, as a template, so it can be reused search after search without a manual rename in between. Anything that needs the *real* path a search just wrote (a script, a support answer, a "did it save?" check) has to read it from the completed search itself (`SearchSummary.Output` / the app's "Open output" button), never by re-deriving it from the text box. This also retired the old "you're about to overwrite the seed list you loaded" confirmation dialog: since the written file never again has the exact name of an existing file unless two searches start in the same second, there is nothing left for it to warn about.

### 26. A new game started after loading a savegame has 10 instead of 14 Latium decoration islands

DLC01 enlarges the Latium map and adds four decoration islands to the base ten (`EnlargedAdditionalDecoIslands = 4` in every
Latium map template). A new game that is started after a savegame has been loaded in the same session (load, Quit to Title, New
Game) misses those four. The islands, their rotations, the traders and the first ten decorations stay exactly the same, but the
four missing placements also skip their rotation draws: the slot phase starts four draws earlier, and every island's slots and
fertilities come out different. Reproduced on purpose (Corners Large 2621, fresh start: 14 decorations; after loading a save: 10)
and found in a player's savegame (Corners Large 856497867) and in older validation saves (Corners Small 3-7), all of which the
app reproduces completely with the "after loading a save" option (`MapProfile.AfterLoad`; `_afterload` in savegame names for
`tools/check_savegame.py`). A savegame without any Albion island has simply not created Albion yet; the checker skips such a
region.

## Practical traps

- **Running the app with a wrong switch opens the GUI** and the process hangs until the timeout. Always use exact switch names and a timeout, and kill stray processes afterwards.
- **Patching C# from a Python heredoc** turns `\n` into a real newline and breaks string literals. Use raw strings, and watch that files differ in line endings (CRLF vs LF).
- **A full disk breaks the build** with errors that look like source errors (`CS2001: source file not found`).
- **Research output belongs outside the repository**; thousands of small probe files otherwise pile up next to the scripts.
- **`--render-ui … en` saves the language.** It writes `language.txt` next to the exe, and `--settings-smoke-test` then fails because it expects German at startup. Delete the file after an English render.
