"""Checks the seed finder's generator against real savegames.

    python check_savegame.py <savegame.a8s | dump folder | folder of them> ... [--exe PATH] [--jobs N] [--verbose]

For every savegame it extracts the map (savegame_map_dump.py, needs RDAExplorer and FileDBReader; see README.md), runs the app's
dump switches for the same settings and compares, per region (Latium, Albion):

    islands      type, rotation and position of every island (Cinis included)
    third party  the traders and the raider
    decorations  every decoration island with rotation and position
    fertility    the goods of every island (as a set per island, order does not matter)
    slots        the goods plus the number of active mine and river/marsh slots per island

A savegame passes when every count matches. The exit status is 0 only if all savegames pass.

The settings are read from the file or folder name (see common.parse_case), e.g. Archipelago_Large_1204.a8s,
Corners_Small_15_nodlc_sparse.a8s, Atoll_Large_1_retro.a8s, or given on the command line for a single savegame
(--template --size --seed --dlc --fertility --slots). A savegame without a size in its name gets the size whose island count
fits.
"""
import argparse
import collections
import concurrent.futures as cf
import re
import shutil
import subprocess
import sys
from pathlib import Path

import common

REGIONS = ("Latium", "Albion")
CATEGORIES = ("islands", "third party", "decorations", "fertility", "slots")
SHORT = {"islands": "isl", "third party": "3rd", "decorations": "deco", "fertility": "fert", "slots": "slot"}


def read_truth(dump: Path) -> dict:
    """The savegame's map: per region the islands (name, rotation, x, y), third-party islands, decorations and, per island with
    fertilities, its goods and its number of active slots."""
    truth = {r: {"island": [], "special": [], "deco": [], "fertility": [], "slots": []} for r in REGIONS}
    for line in (dump / "map_dump.txt").read_text(encoding="utf-8").splitlines():
        p = line.split("|")
        if len(p) < 6 or p[0] not in REGIONS:
            continue
        region, name = p[0], p[1]
        kind = "deco" if "island_deco_" in name else "special" if "3rdparty" in name else "island"
        truth[region][kind].append((name, int(p[4]), int(p[2]), int(p[3])))
        if kind == "island" and p[5]:
            flags = p[11].split(",")[1::2] if len(p) > 11 else []
            goods = frozenset(p[5].split(","))
            truth[region]["fertility"].append((name, goods))
            truth[region]["slots"].append((name, goods, sum(1 for f in flags if f == "01")))
    return truth


def read_layout(lines: list[str]) -> dict:
    got = {r: {"island": [], "special": [], "deco": []} for r in REGIONS}
    for line in lines:
        p = line.split("|")
        if len(p) == 7 and p[1] in ("island", "special", "deco"):
            got[p[0]][p[1]].append((p[2], int(p[4]), int(p[5]), int(p[6])))
    return got


def read_goods(lines: list[str]) -> tuple[list, list]:
    """The app's dump rows name|set|goods|active slots -> (goods per island, goods and active slots per island)."""
    fertility, slots = [], []
    for line in lines:
        p = line.split("|")
        if len(p) == 4 and p[2]:
            goods = frozenset(p[2].split(","))
            fertility.append((p[0], goods))
            slots.append((p[0], goods, int(p[3])))
    return fertility, slots


def compare(truth: list, produced: list) -> tuple[int, int, int, list]:
    """(matching, truth count, produced count, examples of what differs)."""
    a, b = collections.Counter(truth), collections.Counter(produced)
    both = sum((a & b).values())
    return both, sum(a.values()), sum(b.values()), [f"in the savegame only: {x}" for x in list((a - b))[:3]] + [f"in the app only:      {x}" for x in list((b - a))[:3]]


def run_case(case: common.Case, truth: dict, exe: Path, work: Path) -> dict:
    """Runs the app for one savegame's settings; returns {(region, category): (match, truth, produced, examples)}."""
    tag = re.sub(r"\W+", "_", case.label())
    env = {"NODLC": "1" if case.dlc == "off" else "0", "SLOTS": case.slots}
    layout_env = {"NODLC": "1" if case.dlc == "off" else "0", "RETRO": "1" if case.dlc == "retro" else "0"}
    template, size, seed = case.template, case.size, str(case.seed)
    layout = read_layout(common.run_exe(exe, ["--dump-layout", template, size, seed], work / f"{tag}_layout.txt", env=layout_env))
    if case.dlc == "retro":
        latium = common.run_exe(exe, ["--dump-profile-retro", template, size, seed], work / f"{tag}_l.txt", (case.fertility,), env)
    else:
        latium = common.run_exe(exe, ["--dump-profile-sites", template, size, seed], work / f"{tag}_l.txt", (case.fertility,), env)
    albion = common.run_exe(exe, ["--dump-albion-sites", template, size, seed], work / f"{tag}_a.txt", (case.fertility,), env)
    goods = {"Latium": read_goods(latium), "Albion": read_goods(albion)}
    result = {}
    for region in REGIONS:
        for kind, category in (("island", "islands"), ("special", "third party"), ("deco", "decorations")):
            result[(region, category)] = compare(truth[region][kind], layout[region][kind])
        result[(region, "fertility")] = compare(truth[region]["fertility"], goods[region][0])
        result[(region, "slots")] = compare(truth[region]["slots"], goods[region][1])
    return result


def infer_size(case: common.Case, truth: dict, exe: Path, work: Path) -> str:
    """The size whose generated island count equals the savegame's (Latium plus Albion) - for names without a size."""
    wanted = len(truth["Latium"]["island"]) + len(truth["Albion"]["island"])
    for size in ("Large", "Medium", "Small"):
        lines = common.run_exe(exe, ["--dump-layout", case.template, size, str(case.seed)], work / "size.txt",
                               env={"NODLC": "1" if case.dlc == "off" else "0", "RETRO": "1" if case.dlc == "retro" else "0"})
        if sum(1 for l in lines if l.split("|")[1:2] == ["island"]) == wanted:
            return size
    raise SystemExit(f"Cannot tell the map size of {case.label()}; put it into the file name.")


def collect(paths: list[Path], rda: Path | None, fdb: Path | None) -> list[tuple[str, Path]]:
    """(name, dump folder) for every input; savegames are extracted into tools/dumps first."""
    found: list[tuple[str, Path]] = []

    def add_save(save: Path) -> None:
        dump = common.DUMPS / save.stem
        if not (dump / "map_dump.txt").exists() or (dump / "map_dump.txt").stat().st_mtime < save.stat().st_mtime:
            command = [sys.executable, str(common.HERE / "savegame_map_dump.py"), str(save), "--out-dir", str(dump)]
            for flag, value in (("--rda", rda), ("--fdb", fdb)):
                if value:
                    command += [flag, str(value)]
            print(f"extracting {save.name} ...", flush=True)
            done = subprocess.run(command, capture_output=True, text=True)
            if done.returncode != 0:
                raise SystemExit(f"Extracting {save} failed:\n{done.stdout[-400:]}\n{done.stderr[-800:]}")
        found.append((save.stem, dump))

    for path in paths:
        if path.is_file() and path.suffix.lower() == ".a8s":
            add_save(path)
        elif (path / "map_dump.txt").exists():
            found.append((path.name, path))
        elif path.is_dir():
            for save in sorted(path.rglob("*.a8s")):
                add_save(save)
            for dump in sorted(path.rglob("map_dump.txt")):
                found.append((dump.parent.name, dump.parent))
        else:
            raise SystemExit(f"Not a savegame, dump or folder: {path}")
    return found


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("inputs", nargs="+", type=Path, help="savegames (.a8s), dump folders (with map_dump.txt) or folders of them")
    parser.add_argument("--exe", help="Anno117SeedFinder.exe (default: a build in this repository)")
    parser.add_argument("--jobs", type=int, default=4, help="savegames checked in parallel")
    parser.add_argument("--verbose", action="store_true", help="show examples of every difference")
    parser.add_argument("--rda", type=Path, help="RdaConsole.exe, for extracting savegames")
    parser.add_argument("--fdb", type=Path, help="FileDBReader.exe, for extracting savegames")
    for option in ("template", "size", "seed", "dlc", "fertility", "slots"):
        parser.add_argument("--" + option, help="settings of a single savegame (instead of reading them from its name)")
    args = parser.parse_args()

    exe = common.find_exe(args.exe)
    work = common.temp_dir()
    items = collect(args.inputs, args.rda, args.fdb)
    if not items:
        raise SystemExit("No savegames or dumps found.")
    jobs = []
    for name, dump in items:
        case = common.parse_case(name)
        if len(items) == 1 and args.template and args.seed:
            case = common.Case(common.TEMPLATES[args.template.lower()], args.size.capitalize() if args.size else None, int(args.seed),
                               args.dlc or "on", args.fertility or "abundant", args.slots or "abundant")
        if case is None:
            print(f"skipped {name}: no template and seed in the name (see README.md)")
            continue
        jobs.append((name, dump, case))

    def check(job):
        name, dump, case = job
        truth = read_truth(dump)
        if case.size is None:
            case.size = infer_size(case, truth, exe, work)
        return name, case, run_case(case, truth, exe, work)

    failures = 0
    totals = collections.Counter()
    with cf.ThreadPoolExecutor(max(1, args.jobs)) as pool:
        for name, case, result in pool.map(check, jobs):
            ok = all(m == t == g for (m, t, g, _) in result.values())
            failures += 0 if ok else 1
            parts = []
            for region in REGIONS:
                cells = " ".join(f"{SHORT[c]} {result[(region, c)][0]}/{result[(region, c)][1]}" for c in CATEGORIES)
                parts.append(f"{region[:3]}: {cells}")
            print(f"{'PASS' if ok else 'FAIL'}  {case.label():42s} {' | '.join(parts)}")
            for key, (m, t, g, examples) in result.items():
                totals[key[1], "match"] += m
                totals[key[1], "total"] += t
                if args.verbose and (m != t or g != t):
                    print(f"      {key[0]} {key[1]}: {m}/{t} (app produced {g})")
                    for example in examples:
                        print(f"        {example}")
    shutil.rmtree(work, ignore_errors=True)
    print()
    for category in CATEGORIES:
        print(f"  {category:12s} {totals[category, 'match']}/{totals[category, 'total']}")
    print(f"{len(jobs) - failures} of {len(jobs)} savegames pass")
    return 0 if failures == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
