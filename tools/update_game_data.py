#!/usr/bin/env python3
"""update_game_data.py - brings the app's game data up to date after a game update (one click: Update-Game-Data.bat).

What it does, in order:
  1. finds the game's archives (maindata\\*.rda) and extracts the files the app depends on, LAYERED: the base archives first, then
     zz_patchfiles_150 ... in numeric order, so a later patch overrides an earlier file exactly as the game does.
       islands    *.a7m, *.a7minfo   (tile grids, outlines, slot objects)
       templates  *.a7t, *.a7tinfo   (map templates: positions, start points, island types)
       assets     assets.xml         (fertility sets and pools, fertilities, map generator, random islands)
     An archive that has not changed since the last run is not extracted again.
  2. compares every file with the previous run (tools/game_data/state.json) and lists what the update changed.
  3. writes the island data into the app (src/Anno117SeedFinder): island-masks.txt (Aegyptus outlines), island_tiles/*.png,
     EgyptGenerator.TileTable and Footprints, the harbour tile counts of IslandAreas - exactly what changed, nothing else.
  4. reports changed templates (what moved or was added) and changed fertility sets/pools, which need a human look
     (they are NOT rewritten automatically: the generator's rules live in code).
  5. rebuilds the app (dotnet build -c Release), runs its self-test, re-exports the 2.1 source (export_21.py) if that project exists.
  6. optionally checks savegames: --check <save.a8s|folder> [...] runs check_savegame.py on them (name the file like
     Corners_Large_301.a8s, see README.md) and warns when a savegame was made with a mod that changes fertility sets.

    python tools/update_game_data.py                      (everything above)
    python tools/update_game_data.py --check "D:\\saves"    (and verify savegames)
    python tools/update_game_data.py --dry-run            (extract and report, change nothing in the app)
    python tools/update_game_data.py --force              (extract every archive again)

Needs: RdaConsole.exe and FileDBReader.exe (+ its FileFormats folder), Python 3.10+ with Pillow (pip install pillow), the game.
Where things are is looked up (C:\\tools, tools/bin, PATH, the usual game folders); override with --game, --rda, --fdb, --work.
The first run takes the previous state from the old extraction folders in .research (if present), later runs from state.json.
"""
import argparse
import collections
import hashlib
import io
import json
import os
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent
SRC = REPO / "src" / "Anno117SeedFinder"
EXPORT_21_TARGET = Path(r"D:/117 mods/anno117-seedfinder/src/Anno117SeedFinder")
sys.path.insert(0, str(HERE))

GAME_DIRS = [
    r"C:\Spiele\UPlay\Anno 117 - Pax Romana", r"C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games\Anno 117 - Pax Romana",
    r"C:\Program Files\Ubisoft\Ubisoft Game Launcher\games\Anno 117 - Pax Romana", r"C:\Program Files (x86)\Steam\steamapps\common\Anno 117 - Pax Romana",
    r"C:\Program Files\Epic Games\Anno117", r"D:\Spiele\UPlay\Anno 117 - Pax Romana", r"D:\SteamLibrary\steamapps\common\Anno 117 - Pax Romana",
]
# Archives that never hold islands, templates or assets.
SKIP_ARCHIVES = re.compile(r"^(sound|shaders|video|ui|graphics_.*|.*graphics.*|infotips|script|file_browse_patterns|en_us\d*|de_de\d*|[a-z]{2}_[a-z]{2}\d*)\.rda$", re.I)
ISLAND_EXT = (".a7m", ".a7minfo")
TEMPLATE_EXT = (".a7t", ".a7tinfo")
TRACKED_ASSETS = ("Fertility", "FertilitySet", "FertilityPool", "MapTemplate", "MapTemplateType", "MapGenerator", "RandomIsland", "Island", "Region")


def lp(path):
    """Long-path form for Windows (the DLC03 pool paths exceed 260 characters)."""
    path = os.path.abspath(str(path))
    return "\\\\?\\" + path if os.name == "nt" and not path.startswith("\\\\?\\") else path


def sha1(path):
    h = hashlib.sha1()
    with open(lp(path), "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def say(text=""):
    print(text, flush=True)


# ---------------------------------------------------------------------------------------------------------------- tools and game

def find_tool(names, extra=None):
    dirs = [Path(extra)] if extra else []
    dirs += [HERE / "bin", Path("C:/tools"), Path("C:/tools/RDAExplorer"), Path("C:/tools/FileDBReader"),
             Path(os.environ.get("APPDATA", "")) / "Anno 117 Layout Tool" / "tools"]
    for name in names:
        for d in dirs:
            if d.is_file() and d.name.lower() == name.lower():
                return d
            if (d / name).is_file():
                return d / name
        found = shutil.which(name)
        if found:
            return Path(found)
    return None


def find_game(given):
    candidates = [given] if given else [os.environ.get("ANNO117_DIR")] + GAME_DIRS
    for c in candidates:
        if c and (Path(c) / "maindata").is_dir():
            return Path(c)
        if c and Path(c).is_dir() and list(Path(c).glob("*.rda")):
            return Path(c).parent
    return None


def archive_order(maindata):
    """Base archives alphabetically, then the patches by number: the order the game applies them."""
    names = [p.name for p in maindata.glob("*.rda") if not SKIP_ARCHIVES.match(p.name)]
    patches = sorted((n for n in names if n.lower().startswith("zz_patchfiles_")), key=lambda n: int(re.search(r"(\d+)", n).group(1)))
    return sorted(n for n in names if n not in patches) + patches


def run_rda(rda_exe, archive, out, filters):
    out.mkdir(parents=True, exist_ok=True)
    for f in filters:
        # RdaConsole crashes when its output goes to a pipe; a file is fine.
        with open(out.parent / "rda.log", "w") as log:
            subprocess.run([str(rda_exe), "extract", "-f", str(archive), "-o", str(out), "--filter", f, "-y", "-n"], stdout=log, stderr=log)


# ---------------------------------------------------------------------------------------------------------------- extract + merge

def extract_layers(game, rda_exe, work, state, force):
    maindata = game / "maindata"
    order = archive_order(maindata)
    layers = work / "layers"
    known = state.get("archives", {})
    now = {}
    for name in order:
        path = maindata / name
        stem = path.stem
        stat = path.stat()
        signature = {"size": stat.st_size, "mtime": int(stat.st_mtime)}
        now[name] = signature
        out = layers / stem
        if not force and known.get(name) == signature and out.is_dir():
            say(f"  {name}: unchanged, kept")
            continue
        if out.exists():
            shutil.rmtree(lp(out), ignore_errors=True)
        started = time.time()
        filters = ["a7m", "a7t"] + (["assets.xml"] if "config" in stem or stem.startswith("zz_patchfiles") else [])
        run_rda(rda_exe, path, out, filters)
        count = sum(len(fs) for _, _, fs in os.walk(lp(out)))
        say(f"  {name}: extracted {count} files ({time.time() - started:.0f} s)")
    return order, now, layers


def merge(order, layers):
    """rel path -> file, a later archive overrides an earlier one."""
    merged = {}
    for name in order:
        root = layers / Path(name).stem
        if not root.is_dir():
            continue
        for dp, _, files in os.walk(lp(root)):
            for f in files:
                full = os.path.join(dp, f)
                rel = os.path.relpath(full, lp(root)).replace(os.sep, "/")
                if rel.startswith("data/") or rel.startswith("shared/"):
                    merged[rel] = (full[4:] if full.startswith("\\\\?\\") else full, name)
    return merged


def kind(rel):
    if rel.endswith(ISLAND_EXT):
        return "island"
    if rel.endswith(TEMPLATE_EXT):
        return "template"
    if rel.endswith("assets.xml"):
        return "assets"
    return None


def baseline_manifest():
    """The previous state before this script existed: the layered extraction of 2026-10-15 (islands) and the decoded templates."""
    manifest = {}
    base = REPO / ".research" / "island-extract-2026-10-15" / "merged"
    if base.is_dir():
        for dp, _, files in os.walk(lp(base)):
            for f in files:
                if f.endswith(ISLAND_EXT):
                    manifest[os.path.relpath(os.path.join(dp, f), lp(base)).replace(os.sep, "/")] = sha1(os.path.join(dp, f))
    return manifest


# ---------------------------------------------------------------------------------------------------------------- islands

def load_island(a7m, rda_exe, fdb_exe, formats, cache, expected_sha):
    """(w, h, area, water, river, irr) of an island, decoded via RdaConsole + FileDBReader (cached by content)."""
    import layout_extract_islands as ex
    d = cache / f"{Path(a7m).stem}_{expected_sha[:10]}"
    if not (d / "gamedata.xml").exists():
        d.mkdir(parents=True, exist_ok=True)
        with open(d / "log.txt", "w") as log:
            subprocess.run([str(rda_exe), "extract", "-f", str(a7m), "-o", str(d), "--filter", "gamedata", "-y", "-n"], stdout=log, stderr=log)
            subprocess.run([str(fdb_exe), "decompress", "-f", "gamedata.data", "-i", str(formats / "Island_Gamedata.xml"), "-y"], cwd=d, stdout=log, stderr=log)
    x = ex._load_xml(d / "gamedata.xml")
    w, h = [int(v) for v in x.find(".//WorldSize").text.split()]
    area = ex._parse_area_ids_raw(x.find(".//AreaIDs"), w, h)
    water = ex._parse_bits(x.find(".//Water"), w, h, invert=True)
    river = ex._parse_bits(x.find(".//RiverGrid"), w, h)
    st = x.find(".//m_StaticTileGrid")
    irr = ex._parse_byte_blocks(st, w, h) if st is not None else None
    return w, h, area, water, river, irr


# Tile colours and classes as the seed preview draws them (shared with the island images already in the app).
COL = {"rock": (120, 104, 88, 255), "dry": (214, 186, 132, 255), "fertile": (104, 150, 64, 255), "riverbank": (48, 112, 98, 255),
       "marsh": (128, 118, 62, 255), "river": (52, 110, 170, 255), "harbour": (120, 196, 204, 255), "shallow": (70, 140, 160, 120)}


def classify(i, area, water, river, irr, egypt):
    a = area[i]
    if a == 0:
        return None
    wet = bool(water and water[i])
    if (a & 0xFF) == 1 and a > 1:
        if wet:
            return "harbour"
        if river and river[i]:
            return "river"
        v = irr[i] if irr else 0
        if egypt:
            return "riverbank" if v == 1 else "fertile" if v == 10 else "dry"
        return "marsh" if v else "dry"
    if wet:
        return "shallow" if egypt else None
    return "rock"


def render_island(name, box, w, h, area, water, river, irr):
    import base64
    from PIL import Image
    egypt = name.startswith("egypt_")
    if box is None:
        xs = [i % w for i in range(w * h) if area[i] != 0]
        ys = [i // w for i in range(w * h) if area[i] != 0]
        box = (min(xs), min(ys), max(xs) + 1, max(ys) + 1)
    x0, y0, x1, y1 = box
    img = Image.new("RGBA", (x1 - x0, y1 - y0), (0, 0, 0, 0))
    px = img.load()
    for y in range(max(y0, 0), min(y1, h)):
        for x in range(max(x0, 0), min(x1, w)):
            c = classify(y * w + x, area, water, river, irr, egypt)
            if c:
                px[x - x0, (y1 - 1) - y] = COL[c]
    img = img.quantize(colors=16, method=Image.Quantize.FASTOCTREE)
    counts = collections.Counter(classify(i, area, water, river, irr, egypt) for i in range(w * h))
    counts.pop(None, None)
    gw, gh = (w + 7) // 8, (h + 7) // 8
    stride = (gw + 31) // 32 * 4
    bits = bytearray(stride * gh)
    for i in range(w * h):
        if area[i] != 0:
            cx, cy = (i % w) // 8, (i // w) // 8
            bits[cy * stride + cx // 8] |= 1 << (cx % 8)
    line = f"{name}|{w}|{h}|{x0}|{y0}|{x1}|{y1}|{gw}|{gh}|{base64.b64encode(bytes(bits)).decode()}"
    return line, img, dict(counts)


def read_text(path):
    raw = Path(path).read_bytes()
    return raw.decode("utf-8-sig"), raw[:3] == b"\xef\xbb\xbf"


def write_text(path, text, bom):
    Path(path).write_bytes((b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8"))


def apply_islands(merged, rda_exe, fdb_exe, formats, work, only_changed, log):
    """Island masks, tile images, Aegyptus tile table and footprints, harbour counts - from the merged a7m files."""
    from PIL import Image
    files = {Path(rel).stem: (full, rel) for rel, (full, _) in merged.items() if rel.endswith(".a7m")}
    cache = work / "decoded"
    masks_path = SRC / "island-masks.txt"
    raw = io.open(masks_path, encoding="utf-8", newline="").read()
    nl = "\r\n" if "\r\n" in raw else "\n"
    lines = [l for l in raw.split(nl) if l]
    new_lines, changed_png, changed_lines = [], [], []
    egypt_tiles, egypt_foot = {}, {}
    todo = 0
    for l in lines:
        if l.startswith("#"):
            new_lines.append(l)
            continue
        name = l.split("|")[0]
        entry = files.get(name)
        if entry is None or (only_changed is not None and name not in only_changed and not name.startswith("egypt_")):
            new_lines.append(l)
            continue
        full, rel = entry
        w, h, area, water, river, irr = load_island(full, rda_exe, fdb_exe, formats, cache, sha1(full))
        egypt = name.startswith("egypt_")
        box = None if egypt else tuple(int(v) for v in l.split("|")[3:7])
        line, img, counts = render_island(name, box, w, h, area, water, river, irr)
        if egypt:
            if line != l:
                changed_lines.append(name)
            new_lines.append(line)
            short = name.replace("egypt_dlc_03_valley_", "")
            build = [i for i in range(w * h) if classify(i, area, water, river, irr, True) in ("dry", "fertile", "riverbank", "harbour")]
            if build:
                xs = [i % w for i in build]
                ys = [i // w for i in build]
                egypt_foot[short] = (min(xs), min(ys), max(xs) + 1, max(ys) + 1)
            egypt_tiles[short] = (counts.get("dry", 0) + counts.get("fertile", 0) + counts.get("riverbank", 0), counts.get("fertile", 0), counts.get("riverbank", 0), counts.get("harbour", 0))
        else:
            new_lines.append(l)
        png = SRC / "island_tiles" / (name + ".png")
        if png.exists() and Image.open(png).convert("RGBA").tobytes() == img.convert("RGBA").tobytes():
            continue
        if png.exists() or egypt:
            img.save(png, optimize=True)
            changed_png.append(name)
    io.open(masks_path, "w", encoding="utf-8", newline="").write(nl.join(new_lines) + nl)

    eg_path = SRC / "EgyptGenerator.cs"
    tiles_changed = foot_changed = []
    if eg_path.exists():
        eg, bom = read_text(eg_path)

        def update(text, start, values, fmt):
            i = text.index(start)
            j = text.index("};", i)
            block = text[i:j]
            changed = []

            def sub(m):
                key = m.group(1)
                if key not in values:
                    return m.group(0)
                new = fmt(key, values[key])
                if new != m.group(0):
                    changed.append(key)
                return new
            block = re.sub(r'\["(\w+)"\]=(?:new)?\([\d,]+\)', sub, block)
            return text[:i] + block + text[j:], changed
        eg, tiles_changed = update(eg, "Dictionary<string,EgyptTiles> TileTable", egypt_tiles, lambda k, v: f'["{k}"]=new({v[0]},{v[1]},{v[2]},{v[3]})')
        eg, foot_changed = update(eg, "Footprints=new", egypt_foot, lambda k, v: f'["{k}"]=({v[0]},{v[1]},{v[2]},{v[3]})')
        write_text(eg_path, eg, bom)

    prog_path = SRC / "Program.cs"
    prog, pbom = read_text(prog_path)
    harbour = {}
    i = prog.index("internal static class IslandAreas")
    j = prog.index("};", i)
    block = prog[i:j]
    for m in re.finditer(r'\["(\w+)"\]=new\((\d+),(\d+),(\d+)\)', block):
        name = m.group(1)
        entry = files.get(name)
        if entry is None:
            continue
        full, _ = entry
        w, h, area, water, river, irr = load_island(full, rda_exe, fdb_exe, formats, cache, sha1(full))
        hb = sum(1 for k in range(w * h) if classify(k, area, water, river, irr, False) == "harbour")
        if hb != int(m.group(4)):
            harbour[name] = (m.group(0), f'["{name}"]=new({m.group(2)},{m.group(3)},{hb})')
    # Not rewritten: three Latium/Albion values (roman_island_large_01, roman_dlc01_island_continental_01, celtic_island_large_06) have
    # differed by 1-34 tiles from the game files since before this script existed and were kept; only reported.
    log(f"Aegyptus outline lines changed: {len(changed_lines)}")
    log(f"island tile images written: {len(changed_png)}" + (f" ({', '.join(changed_png[:8])}{' ...' if len(changed_png) > 8 else ''})" if changed_png else ""))
    log(f"Aegyptus TileTable entries changed: {tiles_changed or 'none'}")
    log(f"Aegyptus Footprints entries changed: {foot_changed or 'none'}")
    log(f"harbour tile counts differing from the game files (Latium/Albion, informational, not rewritten): { {k: v[1] for k, v in harbour.items()} or 'none'}")
    return bool(changed_png or changed_lines or tiles_changed or foot_changed)


def check_island_info(merged, rda_exe, fdb_exe, work, log):
    """island-masks.txt (Latium/Albion) must equal the island's .a7minfo outline; reports coast direction and slot object changes."""
    import base64
    import struct
    import xml.etree.ElementTree as ET
    app = {l.split("|")[0]: l for l in io.open(SRC / "island-masks.txt", encoding="utf-8").read().splitlines() if l and not l.startswith("#")}
    infos = {Path(rel).stem: (full, rel) for rel, (full, _) in merged.items() if rel.endswith(".a7minfo")}
    cache = work / "info"
    mismatches = []

    def ints(hexs, fmt="<i"):
        raw = bytes.fromhex(hexs)
        n = struct.calcsize(fmt)
        return [struct.unpack(fmt, raw[i:i + n])[0] for i in range(0, len(raw), n)]
    for name, line in app.items():
        if name.startswith("egypt_") or name not in infos:
            continue
        full, _ = infos[name]
        d = cache / f"{name}_{sha1(full)[:10]}"
        if not (d / (name + ".xml")).exists():
            d.mkdir(parents=True, exist_ok=True)
            shutil.copy2(lp(full), d / Path(full).name)
            with open(d / "log.txt", "w") as lg:
                subprocess.run([str(fdb_exe), "decompress", "-f", Path(full).name, "-y"], cwd=d, stdout=lg, stderr=lg)
        try:
            root = ET.parse(d / (name + ".xml")).getroot()
            w, h = ints(root.find(".//MapSize").text)
            rect = ints(root.find(".//ActiveMapRect").text)
            g = root.find(".//ActiveMapGrid")
            gw, gh = ints(g.find("x").text)[0], ints(g.find("y").text)[0]
            bits = base64.b64encode(bytes.fromhex(g.find("bits").text)).decode()
            new = f"{name}|{w}|{h}|{rect[0]}|{rect[1]}|{rect[2]}|{rect[3]}|{gw}|{gh}|{bits}"
        except Exception as e:  # noqa: BLE001
            mismatches.append(f"{name}: cannot read ({e})")
            continue
        if new != line:
            mismatches.append(f"{name}: island-masks.txt line differs from the game's .a7minfo")
    log(f"Latium/Albion outlines vs .a7minfo: {len(mismatches)} differences" + ("".join("\n    " + m for m in mismatches[:20])))
    return mismatches


# ---------------------------------------------------------------------------------------------------------------- templates, assets

def decode_template_info(merged, fdb_exe, work):
    """stem -> sorted list of element keys of every .a7tinfo (type, position, island, rotation, fertility set), cached by content."""
    out = {}
    cache = work / "templates"
    for rel, (full, _) in merged.items():
        if not rel.endswith(".a7tinfo"):
            continue
        stem = Path(rel).stem
        d = cache / f"{stem}_{sha1(full)[:10]}"
        if not (d / (stem + ".xml")).exists():
            d.mkdir(parents=True, exist_ok=True)
            shutil.copy2(lp(full), d / Path(full).name)
            with open(d / "log.txt", "w") as lg:
                subprocess.run([str(fdb_exe), "decompress", "-f", Path(full).name, "-y"], cwd=d, stdout=lg, stderr=lg)
        out[stem] = template_elements(d / (stem + ".xml"))
    return out


def template_elements(xml_path):
    if not Path(xml_path).exists():
        return []
    text = Path(xml_path).read_text(encoding="utf-8", errors="replace")
    keys = []
    for e in re.findall(r"<Element>(.*?)</Element>", text, re.S):
        def g(tag):
            m = re.search(rf"<{tag}>(.*?)</{tag}>", e, re.S)
            return m.group(1).strip() if m else ""
        island = g("MapFilePath")
        try:
            island = bytes.fromhex(island).decode("utf-16le").split("/")[-1]
        except ValueError:
            pass
        keys.append("|".join([g("ElementType"), g("Position"), island, g("Rotation90"), g("FertilitySetGUID"), g("FertilityGuids").replace("\n", "").replace(" ", "")]))
    return sorted(keys)


def asset_digests(path):
    """GUID -> sha1 of every tracked asset block (fertilities, sets, pools, map generator ...), plus the template name."""
    text = Path(lp(path)).read_text(encoding="utf-8", errors="replace")
    out = {}
    for m in re.finditer(r"<Asset>\s*<Template>(\w+)</Template>(.*?)</Asset>", text, re.S):
        template, body = m.group(1), m.group(2)
        if template not in TRACKED_ASSETS:
            continue
        g = re.search(r"<GUID>(\d+)</GUID>", body)
        if g:
            out[f"{template}:{g.group(1)}"] = hashlib.sha1(re.sub(r"\s+", "", body).encode()).hexdigest()
    return out


def regenerate_egypt_ranges(exe, seeds, log):
    """The Aegyptus filter ranges (100k seeds per size and setting) follow the tile counts: regenerated with --analyze-egypt."""
    out = Path(os.environ.get("TEMP", ".")) / "egypt_ranges.txt"
    if out.exists():
        out.unlink()
    started = time.time()
    subprocess.run(["dotnet", str(exe), "--analyze-egypt", str(seeds), str(out)], capture_output=True)
    if not out.exists():
        log("Aegyptus ranges: --analyze-egypt produced nothing, ranges NOT updated")
        return False
    rows = [l for l in out.read_text(encoding="utf-8").splitlines() if l.strip()]
    path = SRC / "EgyptFilters.cs"
    text, bom = read_text(path)
    nl = "\r\n" if "\r\n" in text else "\n"
    lines = text.replace("\r\n", "\n").split("\n")
    first = next(i for i, l in enumerate(lines) if l.startswith("  [(MapSizeKind."))
    last = max(i for i, l in enumerate(lines) if l.startswith("  [(MapSizeKind."))
    lines[first:last + 1] = rows
    stamp = time.strftime("%Y-%m-%d")
    lines = [re.sub(r"\(island files of [^)]*\)", f"(island files of the {stamp} game build)", l) if "Generated with --analyze-egypt" in l else l for l in lines]
    new = nl.join(lines)
    changed = new != text
    write_text(path, new, bom)
    log(f"Aegyptus filter ranges regenerated over {seeds} seeds ({time.time() - started:.0f} s): {'changed' if changed else 'identical'}")
    return changed


# ---------------------------------------------------------------------------------------------------------------- main

def main():
    ap = argparse.ArgumentParser(description="Updates the app's game data after a game update.", formatter_class=argparse.RawDescriptionHelpFormatter, epilog=__doc__)
    ap.add_argument("--game", help="game folder (the one with maindata)")
    ap.add_argument("--rda", type=Path, help="RdaConsole.exe")
    ap.add_argument("--fdb", type=Path, help="FileDBReader.exe")
    ap.add_argument("--work", type=Path, default=HERE / "game_data", help="working folder (default tools/game_data, ignored by git)")
    ap.add_argument("--force", action="store_true", help="extract every archive again")
    ap.add_argument("--dry-run", action="store_true", help="extract and report only, do not change the app")
    ap.add_argument("--ranges", action="store_true", help="regenerate the Aegyptus filter ranges even if no island data changed")
    ap.add_argument("--no-build", action="store_true", help="do not rebuild and self-test the app")
    ap.add_argument("--no-export", action="store_true", help="do not re-export the 2.1 source")
    ap.add_argument("--check", nargs="*", default=[], help="savegames (or folders) to check with check_savegame.py")
    args = ap.parse_args()

    report = []

    def log(text=""):
        say(text)
        report.append(text)

    game = find_game(args.game)
    rda_exe = args.rda or find_tool(["RdaConsole.exe"])
    fdb_exe = args.fdb or find_tool(["FileDBReader.exe"])
    if not game:
        raise SystemExit("Game folder not found. Pass --game \"<folder with maindata>\" or set ANNO117_DIR.")
    if not rda_exe or not fdb_exe:
        raise SystemExit("RdaConsole.exe / FileDBReader.exe not found (put them in tools/bin or C:/tools, or pass --rda / --fdb; see tools/README.md).")
    formats = fdb_exe.parent / "FileFormats"
    if not (formats / "Island_Gamedata.xml").exists():
        raise SystemExit(f"FileFormats/Island_Gamedata.xml not found next to FileDBReader ({formats}).")
    try:
        import PIL  # noqa: F401
    except ImportError:
        raise SystemExit("Pillow is missing: pip install pillow")

    work = args.work
    work.mkdir(parents=True, exist_ok=True)
    state_path = work / "state.json"
    state = json.loads(state_path.read_text()) if state_path.exists() else {}
    log(f"Game: {game}")
    log(f"Tools: {rda_exe} / {fdb_exe}")
    log("")
    log("== Extracting (layered) ==")
    order, archives_now, layers = extract_layers(game, rda_exe, work, state, args.force)
    patches = [n for n in order if n.lower().startswith("zz_patchfiles_")]
    log(f"Archives: {len(order)}; patches in order: {', '.join(Path(p).stem.replace('zz_patchfiles_', '') for p in patches)}")
    new_archives = [n for n in archives_now if n not in state.get("archives", {})]
    changed_archives = [n for n in archives_now if n in state.get("archives", {}) and state["archives"][n] != archives_now[n]]
    if state:
        log(f"New archives since the last run: {new_archives or 'none'}; changed: {changed_archives or 'none'}")

    merged = merge(order, layers)
    manifest = {rel: sha1(full) for rel, (full, _) in merged.items() if kind(rel) in ("island", "template")}
    first_run = "manifest" not in state
    previous = state.get("manifest") or baseline_manifest()
    by_name = collections.defaultdict(set)
    for rel, h in previous.items():
        by_name[Path(rel).name].add(h)
    changed, added = [], []
    for rel, h in manifest.items():
        if rel in previous:
            if previous[rel] != h:
                changed.append(rel)
        elif Path(rel).name in by_name:
            if h not in by_name[Path(rel).name]:
                changed.append(rel)
        elif rel.endswith(ISLAND_EXT) or not first_run:
            added.append(rel)
    removed = [rel for rel in previous if rel not in manifest and not first_run]
    log("")
    log("== What changed since the last run ==" + (" (first run: islands compared with the 2026-10-15 extraction; templates and assets only recorded)" if first_run else ""))
    for label, items in (("changed", changed), ("new", added), ("removed", removed)):
        by_kind = collections.Counter(kind(r) for r in items)
        log(f"{label}: {len(items)} files {dict(by_kind) if items else ''}")
        for r in items[:60]:
            log(f"    {Path(r).name}")
        if len(items) > 60:
            log(f"    ... and {len(items) - 60} more")

    # what the newest patch archive carries: a quick read on what an update touched
    if patches:
        newest = patches[-1]
        carried = [rel for rel, (_, archive) in merged.items() if archive == newest and kind(rel)]
        by_kind = collections.Counter(kind(r) for r in carried)
        templates_in_patch = sorted({Path(r).stem for r in carried if kind(r) == "template"})
        log(f"newest patch {newest}: {dict(by_kind)}; templates in it: {templates_in_patch or 'none'}")

    # assets (fertility sets, pools, ...)
    assets_rel = [rel for rel in merged if rel.endswith("assets.xml")]
    digests = {}
    for rel in assets_rel:
        if rel.endswith("config/export/assets.xml"):
            digests = asset_digests(merged[rel][0])
    old_assets = state.get("assets")
    log("")
    log("== Fertility sets, pools and generator assets (assets.xml) ==")
    if not digests:
        log("assets.xml not found in the archives.")
    elif old_assets is None:
        log(f"first run: recorded {len(digests)} tracked assets, nothing to compare with yet.")
    else:
        diff = [k for k in sorted(set(digests) | set(old_assets)) if digests.get(k) != old_assets.get(k)]
        log("no tracked asset changed." if not diff else f"{len(diff)} tracked assets changed: {', '.join(diff[:40])}  <- CHECK the generator's fertility rules")

    # templates
    log("")
    log("== Map templates ==")
    tmpl = decode_template_info(merged, fdb_exe, work)
    old_tmpl = state.get("templates")
    if old_tmpl is None:
        old_tmpl = {}
        log(f"first run: {len(tmpl)} templates recorded, nothing to compare with yet.")
    template_changes = []
    for stem, elements in tmpl.items():
        if stem in old_tmpl and old_tmpl[stem] != elements:
            only_old = set(old_tmpl[stem]) - set(elements)
            only_new = set(elements) - set(old_tmpl[stem])
            template_changes.append(stem)
            log(f"  {stem}: {len(only_old)} elements gone, {len(only_new)} new   <- CHECK")
            for e in sorted(only_old)[:3]:
                log(f"      - {e[:150]}")
            for e in sorted(only_new)[:3]:
                log(f"      + {e[:150]}")
    if not template_changes:
        log("no template changed (positions, island files, rotations, fertility sets of the elements).")
    unchanged_info_only = [r for r in changed if r.endswith(".a7t")]
    if unchanged_info_only:
        log(f"note: {len(unchanged_info_only)} .a7t files changed (the large template body: terrain and decoration); the app reads only the elements above.")

    # islands
    applied = False
    mismatches = []
    if args.dry_run:
        log("")
        log("dry run: the app was not changed.")
    else:
        log("")
        log("== Writing island data into the app ==")
        only = {Path(r).stem for r in changed + added if r.endswith(".a7m")} if not first_run else None
        applied = apply_islands(merged, rda_exe, fdb_exe, formats, work, None if first_run else only, log)
        mismatches = check_island_info(merged, rda_exe, fdb_exe, work, log)

    state.update({"archives": archives_now, "manifest": manifest, "assets": digests or old_assets, "templates": tmpl, "updated": time.strftime("%Y-%m-%d %H:%M")})
    state_path.write_text(json.dumps(state))

    # build + self-test + 2.1 export
    if not args.dry_run and not args.no_build and shutil.which("dotnet"):
        log("")
        log("== Build and self-test ==")
        build = subprocess.run(["dotnet", "build", "-c", "Release", "-v", "q", "--nologo"], cwd=SRC, capture_output=True, text=True)
        log("build: " + ("ok" if build.returncode == 0 else "FAILED\n" + build.stdout[-1500:]))
        exe = SRC / "bin" / "Release" / "net10.0-windows" / "Anno117SeedFinder.dll"
        if build.returncode == 0 and exe.exists() and (applied or args.ranges) and (SRC / "EgyptFilters.cs").exists():
            if regenerate_egypt_ranges(exe, 100000, log):
                build = subprocess.run(["dotnet", "build", "-c", "Release", "-v", "q", "--nologo"], cwd=SRC, capture_output=True, text=True)
                log("rebuild after the new ranges: " + ("ok" if build.returncode == 0 else "FAILED\n" + build.stdout[-1500:]))
        if build.returncode == 0 and exe.exists():
            out = Path(os.environ.get("TEMP", ".")) / "self_test_report.txt"
            subprocess.run(["dotnet", str(exe), "--self-test-report", str(out)], capture_output=True)
            log("self-test: " + (out.read_text(errors="replace").strip().splitlines()[-1] if out.exists() else "no report"))
    if not args.dry_run and not args.no_export and EXPORT_21_TARGET.parent.is_dir():
        r = subprocess.run([sys.executable, str(HERE / "export_21.py")], capture_output=True, text=True)
        log("2.1 export: " + (r.stdout.strip().splitlines()[-1] if r.stdout.strip() else r.stderr.strip()[-300:]))

    # savegames
    if args.check:
        log("")
        log("== Savegame check ==")
        r = subprocess.run([sys.executable, str(HERE / "check_savegame.py"), *args.check], capture_output=True, text=True)
        log(r.stdout.strip()[-3000:])

    (work / "report.md").write_text("\n".join(report), encoding="utf-8")
    say("")
    say(f"Report: {work / 'report.md'}")
    say("Next: bump the version (see AppVersion.cs), check README/START-HERE, run the smoke tests, copy to dist.")


if __name__ == "__main__":
    main()
