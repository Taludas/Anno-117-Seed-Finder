# Anno 117 Seed Finder — open problems in map generation

As of: 2026-09-13

All figures are verified against real `.a8s` savegames (extraction via
`.research/tools/savegame_map_dump.py`, comparing fertilities per island as a
multiset). "Matches" means: every fertility set of every island agrees exactly.

---

## 1. The root cause: the decoration phase

Almost all remaining bugs share **one** common cause. The generator's order is:

```
Island placement → third parties (pirate/traders) → decoration phase → site activation → fertilities
```

What is provably **correct**:

- **Island placement**: for all 15 DLC-off combinations (seed 23), the rotation
  of every single regular island matches the savegame exactly. Since exactly one
  `r.Next()` is drawn per slot, this proves the RNG cursor stays exactly in sync
  across the entire placement loop.
- **Third parties**: rotation, order and anchor swap of the pirate and both
  traders match exactly in **every** case checked — 5× DLC-on and 7× DLC-off.

That leaves the decoration phase as the only point of divergence. And because
fertilities are assigned *after* it, a single draw too many or too few shifts
the entire fertility assignment for the map.

### Why the number of draws varies

When placing a decoration, a cell is first checked for clearance at rotation
`axis`, but the rotation actually used is `axis + 2*draw` — i.e. it may end up
rotated 180°. That rotation shifts the anchor point of the footprint box, so a
candidate that already passed the clearance check can fall outside the playable
bounds. It gets rejected and the next candidate is tried — **but the random
value has already been consumed**. Every such rejection costs exactly one extra
draw.

This is a **map-edge effect** and explains the observed values exactly.

---

## 2. Problem A — Atoll: the decoration routine has not been rebuilt

**Status: worked around, not solved.**

Atoll and Rift do not simulate decoration placement at all; instead they
consume a fixed number of random values (`AdvanceTemplatePhase`:
`ShuffleCount(grid²)` + `ShuffleCount(6)` + `tail`). The `tail` is therefore a
fitted constant, not a computed value.

Measured against **13 Atoll/Large savegames** (DLC on):

| tail | rejections | seeds | decorations at the outermost edge (≥2328) |
|------|------------|-------|--------------------------------------------|
| 69 | 0 | 1, 4, 5, 6, 7, 999, 666666666 | **0 in all seven** |
| 70 | 1 | 3, 9, 10 | 1, 3, 0 |
| 71 | 2 | 2, 8, 333333 | 1, 4, 1 |

The base value is **69 = 5 × 14 − 1** for 14 decorations. For DLC-off (10
decorations), the analogous value would be 49. No seed fell outside 69–71
(search range 60–82).

The behaviour is **deterministic**: seed 2 produced the same value across two
independently generated savegames.

**What has already been ruled out** (please do not re-investigate this):

- The existing real `PlaceDecorations` simulation (which works correctly for
  Corners and Archipelago) does **not** reproduce Atoll — tested across **111
  grid sizes × 13 seeds** against the decoration-ordering fingerprint; the best
  match was 3 of 13. Atoll therefore uses a structurally different routine.
- The third-party bits (pirate rotation == 3 plus the two pass flags) that
  explain the analogous shift on Archipelago do not apply here: seeds 999 and
  333333 have **identical** values for all these conditions, yet land in
  different tail groups.
- Proximity between finished islands does not correlate cleanly (161.8 / 136.9
  for tail=69 vs. 155.3 / 157.2 for tail=71 — fully interleaved).
- Rejected candidates leave **no trace** in the finished map. The rejection can
  fundamentally not be reconstructed from a savegame.

**To do:** rebuild Atoll's real decoration routine (candidate grid, ordering,
collision and bounds checking). The table then becomes unnecessary outright.

**Current workaround in the tool:** the default is now 69 (previously 71,
overfitted to seed 2). For unverified seeds, the search now tries **all three**
values and counts a hit if any of them matches — so no seed is ever lost. Such
hits get a ⚠ in the results list and must be double-checked in-game.

---

## 3. Problem B — DLC-off: same cause, more widespread

**Status: open.**

DLC-off is **not** structurally broken. It matches for quite a few seeds:

| Profile | Seed | Result |
|---------|------|--------|
| Atoll / Large | 500 | ✅ 18/18 |
| Corners / Small | 2500 | ✅ (self-test reference) |
| Archipelago / Large | 6153 | ✅ (self-test reference) |
| Archipelago / Small | 23 | ✅ 18/18 |
| Corners / Medium | 23 | ✅ 16/16 |

Seed 23 is simply a "rejection seed" on most templates. Current state for seed
23 (DLC off):

| | Small | Medium | Large |
|---|---|---|---|
| Archipelago | ✅ 18/18 | ❌ 1/18 | ❌ 1/18 |
| Atoll | ❌ 0/18 | ❌ 2/18 | ❌ 1/18 |
| Rift | ❌ 2/18 | ❌ 0/18 | ❌ 0/18 |
| Corners | ❌ 0/16 | ✅ 16/16 | ❌ 0/16 |
| Island Chains | ❌ 1/16 | ❌ 0/16 | ❌ 1/16 |

Further failures with other seeds: Archipelago/Large 50, Corners/Large 50000,
Rift/Large 5000, IslandChains/Large 500000.

**Important limitation of the search so far:** a sweep of the pure decoration
draw count over 0..90 finds **no** solution for any of the 13 failing
combinations. This does not rule out a pure "tail" fix, though — a wrong
**grid size** shifts consumption by around 260 draws, which is outside the
tested range. The grid size for DLC-off is therefore very likely wrong as
well.

Known so far: DLC-on Atoll uses grid 177 at map width 2688 (= width/16 + 9).
For DLC-off (width 2048), the same rule would give 137 — that is the currently
configured value, and it does not work.

**To do:** cleanly determine the grid size and base `tail` for DLC-off; after
that, the same ±1/±2 rejection spread as in problem A will presumably remain.

---

## 4. Problem C — further seed tables with the same pattern

All of these are symptoms of the same cause and disappear once the decoration
routine is genuinely simulated per template:

| Table | File/location | Content |
|---|---|---|
| `VerifiedAtollDecorationTail` | Program.cs | 14 entries, Atoll |
| `VerifiedCornersSmallDecorationCount` | Program.cs | seeds 3–7, Corners/Small |
| `VerifiedArchipelagoMediumExtraAdvance` | Program.cs | seeds 2, 100000, 6000000 |
| `VerifiedArchipelagoWiggleBorder` | Program.cs | seeds 6000000, 731629381 |

Each of these tables means: for seeds **not** listed, the tool guesses the
most common value. If the seed happens to differ, the prediction is silently
wrong. During a search, this is the more dangerous case — a good seed is then
dropped without comment and never shows up.

For Atoll, this has since been safeguarded (every candidate is checked, hits
are flagged). For the remaining three tables, **not yet**.

The former Island Chains table `VerifiedChainDecorationRetries` (4 entries) has
been removed: the method from the current author's version explains these
rejections completely (see section 8). This shows that such tables really do
arise from missing edge rules — not from genuine randomness.

---

## 5. Problem D — starter rotation is a heuristic

`RomanStarterRotation` determines the rotation of starter islands via an angle
comparison between the asset's "coast direction" and a fixed diagonal
direction per quadrant. This is an approximation and is sometimes off — there
are already three hardcoded exceptions (Corners/Small slot 19 and 20,
Rift/Medium slot 23).

For Island Chains this is solved: `IslandChainStarterRotation` from the
author's version replaces the quadrant diagonal with fixed,
template-specific target directions per starting bay (slots 18–21), and hits
**95 of 96** starter rotations across 24 savegames (previously 44/96). The
same approach — target direction derived from template geometry instead of the
quadrant — is the obvious way to also replace the other templates and the
three exceptions.

Example, Atoll/Small DLC-off seed 23: of four starter islands, two are wrong
(`large_02` → predicted 1, actual 0; `large_09` → predicted 0, actual 1). The
deviation here is **not** close — the angular distances are 0.40 vs. 1.18
radians, so the heuristic clearly picks the wrong rotation.

**Important for prioritisation:** this bug has **no consequence** for the
fertility prediction. The random value is drawn per slot regardless of which
rotation the heuristic derives from it — the RNG cursor stays in sync.
Affected are only the island orientation shown in the preview and (for
templates with a real decoration simulation) the occupancy mask.

---

## 6. Problem E — Archipelago/Medium: closed, but with a lesson

Originally, it looked as if eight seeds (3, 9, 10, 14, 16, 24, 26, 29) on
Archipelago/Medium would skip the wiggle entirely and produce only 10 instead
of 14 decorations. A dedicated table
(`VerifiedArchipelagoMediumBulkPhase`) was built for this.

**That was an artifact of the data collection.** All eight savegames came from
a single capture session within four minutes. In these files, the Albion
region was missing entirely (0 entries instead of 29) — the map had therefore
not yet finished generating/serializing when it was saved.

Newly generated savegames for the same seeds consistently show 14
decorations. Seed 3 was generated three times: once the old state, twice
normal.

**The table has been removed.** Across every Archipelago/Medium extraction
ever taken, this holds without exception:

| Albion in the savegame | Decorations in Latium |
|---|---|
| 0 entries | 10 (faulty) |
| 29 entries | 14 (correct) |

**Lesson for future reference data:** a savegame is only usable if **both**
regions are present. A quick quicksave immediately after map generation
produces unusable data. The check is simple:
`grep -c "^Albion|" map_dump.txt` must return 29.

The author's version, by their own account, also excludes eight faulty saves
and names **hot-reload** (a new map loaded into a running session) as the
cause. That is probably the more precise explanation than "saved too early".

---

## 7. Problem F — Albion is barely checked against savegames

**Status: open.**

All investigations so far concerned Latium. A comparison of Albion
fertilities against every complete Abundant savegame (DLC on) gives:

| Template | Albion exact |
|---|---|
| Archipelago | 32/35 |
| Atoll | 11/20 |
| Corners | 21/22 |
| Rift | 14/15 |
| Island Chains | 24/25 |

Weakest is Atoll/Large: of the 13 seeds (1–10, 999, 333333, 666666666), only 6
match in Albion.

The Albion decoration collision uses a different border in each version — the
author's version 2 cells, this version 3. For Abundant, that is the only
difference in the Albion code. Results are identical across every savegame,
**except** for two Archipelago/Medium seeds, and there in opposite directions:
seed 29 only matches with border 2, seed 190517995 only with border 3. Neither
value is therefore the whole truth; there is presumably a missing edge rule of
the same kind found for Island Chains in Latium.

**To do:** systematically validate Albion, starting with Atoll/Large, and
determine the collision rule so that both Archipelago seeds match.

---

## 8. What is confirmed to work

- **Island placement and slot assignment**: for all templates, sizes and both
  DLC states.
- **Third parties**: fully correct, DLC on and off.
- **DLC-on** overall: self-test 36/36; Atoll/Large 100% across 13 seeds; seed
  999 at 100% on Corners/Medium, Corners/Large, Rift/Large, Atoll/Small,
  Atoll/Large.
- **Fertility settings** (Abundant / Regular / Sparse): covered by the
  self-test.
- **Island Chains** (DLC on): 24/25 Latium and 24/25 Albion **with no seed
  exceptions**, 95/96 starter rotations. Adopted from the author's version: a
  single EnlargementOffset for both axes, half-open collision edges during
  decoration placement, an extra left-edge cell for `deco_01`, and fixed
  directions for the starting bays. The one failure, Small / seed 5000000,
  fails completely (0/23) in both versions and is presumably a faulty
  reference savegame.

---

## 9. Recommended order

1. **Rebuild Atoll's real decoration routine.** Biggest lever: fully solves
   problem A, and very likely hands over the key for Rift directly (same
   shortcut) as well as for the DLC-off part of problem B.
2. **Determine the grid size for DLC-off.** Can be tackled independently; a
   two-dimensional sweep over grid **and** tail would be the next concrete
   step (the previous sweep only varied the tail and was therefore too
   narrow).
3. **Validate Albion** (problem F), starting with Atoll/Large.
4. **Safeguard the three remaining seed tables** while 1. and 2. are still
   open — analogous to the Atoll approach: check every candidate, flag hits.
   This removes the silent dropping of good seeds.
5. **Starter rotation** — switch the remaining templates to the Island Chains
   pattern last — cosmetic, no effect on the search.

---

## 10. Comparison with the author's version (as of 2026-09-13)

Both versions were run against the same complete Abundant savegames. Adopted
from the author's version were the Island Chains method (section 8) and the
raw marble filter.

The author's version, on the other hand, is still missing these fixes:

| Area | Author's version | This version | Fix |
|---|---|---|---|
| Archipelago / Medium | 2/24 | 24/24 | extra draw after decoration only for specific seeds instead of always; wiggle border 1 |
| Atoll / Large | 3/13 | 13/13 | tail base 69 instead of 71 plus rejection table |
| Corners / Small (seeds 3–7) | 0/5 | 5/5 | decoration-count table |
| Rift / Medium (600000, 6000000) | 0/2 | 2/2 | width condition also for Medium, rotation slot 23 |
| Atoll / Large, DLC off (seed 500) | ✗ | ✓ | tail 50 |
| Regular / Sparse | not supported | supported | set resolution per setting |

All other template/size combinations give identical results in both versions.

---

## 11. Tools

Every investigation is reproducible:

```
--self-test-report <out>                         reference tests (currently 36/36)
--dump-profile <tmpl> <size> <seed> <out>        fertilities per island (DLC on)
--dump-profile-nodlc <tmpl> <size> <seed> <out>  same, DLC off
--dump-placements <tmpl> <size> <seed> <out>     raw placement incl. rotation and slot
--dump-profile-phase <tmpl> <size> <seed> <grid> <tail> <out>
--dump-vanilla-profile-phase  … same, DLC off
--dump-decorations-forcewidth <tmpl> <size> <seed> <width> <out>
--dump-third-party-raw[-nodlc] …                 third-party raw values
--analyze-mines <size> <dlc> <seeds> <out>       statistics for minerals/copper/silver
--describe-seeds <tmpl> <size> <in.txt> <out.csv>
```

Savegame extraction: `python .research/tools/savegame_map_dump.py <file.a8s> --out-dir <folder>`
Comparison: `python .research/tools/compare_fert.py <map_dump.txt> <dump-profile-output>`

Reference savegames live under `.research/dumps/` (among others `batch12_atoll/`
with the 13 Atoll/Large seeds, `batch10_dlcoff23/` with all 15 DLC-off
combinations).
