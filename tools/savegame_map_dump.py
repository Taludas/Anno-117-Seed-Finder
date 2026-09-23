"""
savegame_map_dump.py - reads the map of an Anno 117 savegame (.a8s) and writes it out as text, as ground truth for
check_savegame.py.

    python savegame_map_dump.py <savegame.a8s> [--out-dir DIR] [--rda RdaConsole.exe] [--fdb FileDBReader.exe] [--keep-work]

Needs two external tools (see README.md): RdaConsole (from RDAExplorer) and FileDBReader. They are found in tools/bin,
in C:/tools, next to the Anno 117 Layout Tool's cache, on PATH, or via --rda / --fdb.

The script reads only the MapTemplate section of each session (Latium and Albion), the part that describes how the map was
generated, and ignores buildings. Pipeline:
  1. RdaConsole extracts the .a8s into data.a7s (one file)
  2. zlib-decompress data.a7s into a FileDB V3 blob
  3. FileDBReader converts it to XML
  4. stream the XML for the two sessions (SessionGUID tells Latium from Albion) and their BinaryData blobs
  5. decode each blob (a second FileDB) and convert it to XML with FileDBReader
  6. stream MapTemplate/TemplateElement/Element for
       Position, MapFilePath (island model name), Rotation90 (0-3), FertilityGuids (six little-endian uint32),
       RandomizeFertilities, FertilitySetGUID (Starter/Secondary/Tertiary rule) and MineSlotActivation (raw hex of every slot,
       see the note below)

Output, under --out-dir (default tools/dumps/<savegame name>/):
  map_dump.txt          one line per island, all fields:
                        region|name|x|z|rotation|fertilities|fertility set values|set markers|area markers|randomised|slot count|slots
  start_points.txt      the template elements without an island file: third parties and player start points
  latium_matchrows.txt  name|rotation|x|z per Latium island (input of the app's --match-slots switch)
  albion_matchrows.txt  the same for Albion (--match-albion-slots)

MineSlotActivation is written as raw hex: per slot a GUID and an active flag (01/00). check_savegame.py counts the active flags.

Credits: adapted from savegame_parser.py of the Anno 117 Layout Tool (MIT, see THIRD-PARTY-NOTICES.md). The savegame field
reference is oliversaggau/anno-designer, branch "Savegames", AnnoDesigner.Import/docs/Anno117_Savegames.md.
"""

import argparse
import os
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import zlib
from pathlib import Path
from typing import Optional


# ── Constants ────────────────────────────────────────────────────────────────

SESSION_REGIONS: dict[int, str] = {
    3245: "Latium",
    6627: "Albion",
}

# Where the two external tools are looked for besides PATH; the layout tool's cache is included so a machine that already
# has that tool set up needs no extra flags.
_LAYOUT_TOOL_SETTINGS_DIR = Path(os.environ.get("APPDATA", "")) / "Anno 117 Layout Tool"
_TOOL_SEARCH_DIRS = [
    Path(__file__).resolve().parent / "bin",
    _LAYOUT_TOOL_SETTINGS_DIR / "tools" / "RDAExplorer",
    _LAYOUT_TOOL_SETTINGS_DIR / "tools" / "FileDBReader",
    _LAYOUT_TOOL_SETTINGS_DIR / "tools",
    Path("C:/tools"),
    Path("C:/tools/RDAExplorer"),
    Path("C:/tools/FileDBReader"),
    Path(os.environ.get("PROGRAMFILES", "C:/Program Files")) / "RDAExplorer",
    Path(os.environ.get("PROGRAMFILES(X86)", "C:/Program Files (x86)")) / "RDAExplorer",
]


class ParseError(Exception):
    """Raised when the savegame cannot be parsed."""


# ── Binary helpers (same decoding as savegame_parser.py) ─────────────────────

def _le_uint32(hex8: str) -> int:
    return struct.unpack('<I', bytes.fromhex(hex8))[0]


def _le_uint32_pair(hex16: str) -> tuple[int, int]:
    return struct.unpack('<II', bytes.fromhex(hex16))


def _hex_to_utf16(hex_str: str) -> str:
    try:
        return bytes.fromhex(hex_str).decode('utf-16-le').rstrip('\x00')
    except Exception:
        return ''


# ── External tool discovery ───────────────────────────────────────────────────

def _find_tool(exe_names: list[str]) -> Optional[Path]:
    for name in exe_names:
        for d in _TOOL_SEARCH_DIRS:
            p = d / name
            if p.is_file():
                return p
        found = shutil.which(name)
        if found:
            return Path(found)
    return None


# ── External tool wrappers (unchanged from savegame_parser.py) ───────────────

def _rda_extract(rda_exe: Path, a8s_path: Path, out_dir: Path) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    result = subprocess.run(
        [str(rda_exe), 'extract', '-f', str(a8s_path), '-o', str(out_dir), '-y', '-n'],
        capture_output=True, text=True, timeout=90,
    )
    if result.returncode != 0:
        raise ParseError(f"RdaConsole failed (exit {result.returncode}):\n{result.stderr[:500]}")


def _filedb_to_xml(fdb_exe: Path, input_path: Path, timeout: int = 300) -> Path:
    result = subprocess.run(
        [str(fdb_exe), 'decompress', '-f', input_path.name, '-y'],
        capture_output=True, text=True, timeout=timeout,
        cwd=str(input_path.parent),
    )
    xml_path = input_path.with_suffix('.xml')
    if not xml_path.exists() or xml_path.stat().st_size == 0:
        raise ParseError(
            f"FileDBReader produced no XML for '{input_path.name}'.\n"
            f"Exit {result.returncode}\n"
            f"stdout: {(result.stdout or '').strip()[:300]}\n"
            f"stderr: {(result.stderr or '').strip()[:300]}"
        )
    return xml_path


def _zlib_decompress(src: Path, dst: Path) -> None:
    data = src.read_bytes()
    try:
        dst.write_bytes(zlib.decompress(data))
    except zlib.error as exc:
        raise ParseError(f"zlib decompression of '{src.name}' failed: {exc}") from exc


def _find_file(search_dir: Path, patterns: tuple[str, ...]) -> Optional[Path]:
    for pat in patterns:
        matches = [p for p in search_dir.rglob(pat) if p.is_file()]
        if matches:
            return max(matches, key=lambda p: p.stat().st_size)
    return None


def _list_files(d: Path) -> str:
    files = [p for p in d.rglob('*') if p.is_file()]
    names = [p.name for p in files[:12]]
    return ', '.join(names) + (f' (+{len(files)-12} more)' if len(files) > 12 else '') or '(none)'


# ── Outer XML: extract session BinaryData blobs, tagged by region ────────────

_RE_SESSION_GUID = re.compile(r'<SessionGUID>([0-9A-Fa-f]+)</SessionGUID>')
_RE_BINARY = re.compile(r'<BinaryData>([0-9A-Fa-f]+)</BinaryData>')


def _extract_session_binaries(outer_xml_path: Path, work_dir: Path) -> list[tuple[str, Optional[int], Path]]:
    """
    Stream the outer XML and collect (region_name, session_guid, bin_path) for
    every game-world session under MetaGameManager/GameSessions.

    SessionDesc/SessionGUID always precedes SessionData/BinaryData within the
    same session block, so a flat "last GUID seen, then next BinaryData"
    pairing is reliable (same simplifying assumption savegame_parser.py already
    relies on for BinaryData alone).
    """
    in_game_sessions = False
    pending_guid: Optional[int] = None
    results: list[tuple[str, Optional[int], Path]] = []

    with open(outer_xml_path, 'r', encoding='utf-8', errors='replace') as f:
        for line in f:
            s = line.strip()
            if '<GameSessions>' in s:
                in_game_sessions = True
                continue
            if '</GameSessions>' in s:
                break
            if not in_game_sessions:
                continue

            m = _RE_SESSION_GUID.search(s)
            if m:
                try:
                    pending_guid = _le_uint32(m.group(1)[:8])
                except Exception:
                    pending_guid = None
                continue

            m = _RE_BINARY.search(s)
            if m:
                idx = len(results)
                bin_path = work_dir / f'session_{idx}.bin'
                bin_path.write_bytes(bytes.fromhex(m.group(1)))
                region = SESSION_REGIONS.get(pending_guid, f'Unknown({pending_guid})' if pending_guid is not None else 'Unknown')
                results.append((region, pending_guid, bin_path))
                pending_guid = None

    return results


# ── Session XML: stream MapTemplate/TemplateElement/Element only ─────────────
#
# Everything else in the session XML (AreaManagers, AreaInfo, buildings, roads,
# polygons) is irrelevant to map-generation comparison and is skipped entirely -
# the loop returns as soon as </MapTemplate> closes, without scanning the much
# larger building-data portion of the file.

_RE_POS_HDR = re.compile(r'^<Position>([0-9A-Fa-f]{16})</Position>$')
_RE_PATH_HDR = re.compile(r'^<MapFilePath>([0-9A-Fa-f]+)</MapFilePath>$')
_RE_ROT_HDR = re.compile(r'^<Rotation90>([0-9A-Fa-f]+)</Rotation90>$')
_RE_RANDOMIZE_HDR = re.compile(r'^<RandomizeFertilities>([0-9A-Fa-f]+)</RandomizeFertilities>$')
_RE_NONE_HEX = re.compile(r'^<None>([0-9A-Fa-f]*)</None>$')

# NOTE: the real per-Element field names differ from the (community, work-in-
# progress) documentation this script was first written against. Verified by
# inspecting actual FileDBReader output from a real Anno 117 v2.0 savegame:
#   <FertilityGuids></FertilityGuids>        - present but always empty
#   <FertilitiesPerAreaIndex>                - list-wrapped; holds the real
#     <None>0100</None>                        fertility values. Every island
#     <None>A1080000DE0F0000...</None>         seen so far has exactly one
#   </FertilitiesPerAreaIndex>                 (marker, value-blob) pair; the
#                                               marker's meaning is unconfirmed
#                                               (possibly an area/slot index or
#                                               list-count boilerplate - it was
#                                               "0100" on every island sampled
#                                               so far). The value-blob is N
#                                               concatenated little-endian
#                                               uint32 fertility GUIDs.
#   <FertilitySetGUIDs>                       - same (marker, value) shape as
#     <None>0100</None>                         above. The decoded value did
#     <None>6BA30000</None>                     NOT match any of
#   </FertilitySetGUIDs>                        Anno117SeedFinder's internal
#                                               Starter/Secondary/Tertiary/
#                                               Continental set-id constants
#                                               (31312/3656/14198/144793/...) -
#                                               those are this project's own
#                                               synthetic labels for RNG rule
#                                               pools, not real game GUIDs, so
#                                               no direct match is expected.
#                                               Kept raw for now; correlating
#                                               it to Starter/Secondary/
#                                               Tertiary role is a follow-up.
_RE_FERTAREA_OPEN = '<FertilitiesPerAreaIndex>'
_RE_FERTAREA_CLOSE = '</FertilitiesPerAreaIndex>'
_RE_FERTSET_OPEN = '<FertilitySetGUIDs>'
_RE_FERTSET_CLOSE = '</FertilitySetGUIDs>'


def _decode_none_list(entries: list[str]) -> tuple[list[int], list[str]]:
    """
    Split a raw <None> hex list into decoded uint32 values (entries whose
    length is a non-zero multiple of 8 hex chars) and leftover "marker" hex
    strings (anything else - e.g. the recurring short "0100" seen before every
    value-blob so far, whose exact meaning is not yet confirmed).
    """
    values: list[int] = []
    markers: list[str] = []
    for entry in entries:
        if entry and len(entry) % 8 == 0:
            for i in range(0, len(entry), 8):
                values.append(_le_uint32(entry[i:i + 8]))
        else:
            markers.append(entry)
    return values, markers


LAST_STARTS: list = []


def _parse_map_template(session_xml_path: Path) -> list[dict]:
    """Return one dict per island element found in <MapTemplate>."""
    islands: list[dict] = []

    in_map_template = False
    in_templ_element = False
    in_element = False
    in_mineslot = False
    in_fertarea = False
    in_fertset = False

    t_elemtype = ''
    t_pos = t_path = t_rot = t_randomize = None
    t_mineslot: list[str] = []
    t_fertarea: list[str] = []
    t_fertset: list[str] = []

    with open(session_xml_path, 'r', encoding='utf-8', errors='replace') as f:
        for raw_line in f:
            s = raw_line.strip()

            if not in_map_template:
                if s == '<MapTemplate>':
                    in_map_template = True
                continue
            if s == '</MapTemplate>':
                break

            if not in_templ_element:
                if s == '<TemplateElement>':
                    in_templ_element = True
                continue
            if s == '</TemplateElement>':
                in_templ_element = False
                continue

            if not in_element:
                if s.startswith('<ElementType>'):
                    t_elemtype = s[len('<ElementType>'):-len('</ElementType>')]
                if s == '<Element>':
                    in_element = True
                    t_pos = t_path = t_rot = t_randomize = None
                    t_mineslot = []
                    t_fertarea = []
                    t_fertset = []
                continue
            if s == '</Element>':
                in_element = False
                if t_pos and not t_path:
                    try:
                        sx, sz = _le_uint32_pair(t_pos)
                        LAST_STARTS.append((t_elemtype, sx, sz))
                    except Exception:
                        pass
                if t_pos and t_path:
                    try:
                        px, pz = _le_uint32_pair(t_pos)
                        name = Path(_hex_to_utf16(t_path)).stem.lower()
                        rot = int(t_rot, 16) if t_rot else 0
                        fertilities, fertarea_markers = _decode_none_list(t_fertarea)
                        fertset_values, fertset_markers = _decode_none_list(t_fertset)
                        randomize = bool(int(t_randomize, 16)) if t_randomize else None
                        islands.append({
                            'name': name,
                            'x': px,
                            'z': pz,
                            'rot': rot,
                            'fertilities': fertilities,
                            'fertarea_markers': fertarea_markers,
                            'fertility_set_values': fertset_values,
                            'fertset_markers': fertset_markers,
                            'randomize_fertilities': randomize,
                            'mineslot_raw': list(t_mineslot),
                        })
                    except Exception as exc:
                        print(f"  ! failed to decode island element ({t_path!r}): {exc}", file=sys.stderr)
                continue

            if in_fertarea:
                if s == _RE_FERTAREA_CLOSE:
                    in_fertarea = False
                    continue
                m = _RE_NONE_HEX.match(s)
                if m:
                    t_fertarea.append(m.group(1))
                continue
            if s == _RE_FERTAREA_OPEN:
                in_fertarea = True
                continue

            if in_fertset:
                if s == _RE_FERTSET_CLOSE:
                    in_fertset = False
                    continue
                m = _RE_NONE_HEX.match(s)
                if m:
                    t_fertset.append(m.group(1))
                continue
            if s == _RE_FERTSET_OPEN:
                in_fertset = True
                continue

            if in_mineslot:
                if s == '</MineSlotActivation>':
                    in_mineslot = False
                    continue
                m = _RE_NONE_HEX.match(s)
                if m:
                    t_mineslot.append(m.group(1))
                continue
            if s == '<MineSlotActivation>':
                in_mineslot = True
                continue

            m = _RE_POS_HDR.match(s)
            if m:
                t_pos = m.group(1)
                continue
            m = _RE_PATH_HDR.match(s)
            if m:
                t_path = m.group(1)
                continue
            m = _RE_ROT_HDR.match(s)
            if m:
                t_rot = m.group(1)
                continue
            m = _RE_RANDOMIZE_HDR.match(s)
            if m:
                t_randomize = m.group(1)
                continue

    return islands


# ── Main pipeline ──────────────────────────────────────────────────────────────

def dump_map(a8s_path: Path, rda_exe: Path, fdb_exe: Path, out_dir: Path, keep_work: bool) -> None:
    work_dir = Path(tempfile.mkdtemp(prefix='anno117_mapdump_'))
    try:
        print("Extracting savegame archive...")
        outer_dir = work_dir / 'outer'
        _rda_extract(rda_exe, a8s_path, outer_dir)

        a7s_file = _find_file(outer_dir, ('data.a7s', '*.a7s'))
        if a7s_file is None:
            raise ParseError(f"No .a7s file found in savegame archive.\nExtracted: {_list_files(outer_dir)}")

        print(f"Decompressing {a7s_file.name}...")
        raw_fdb = work_dir / 'data_raw.bin'
        _zlib_decompress(a7s_file, raw_fdb)

        print("Decoding outer FileDB (may take a minute)...")
        outer_xml = _filedb_to_xml(fdb_exe, raw_fdb, timeout=300)

        print("Locating session BinaryData in outer XML...")
        sessions = _extract_session_binaries(outer_xml, work_dir)
        if not sessions:
            raise ParseError("No BinaryData found under GameSessions in outer XML.")

        out_dir.mkdir(parents=True, exist_ok=True)
        dump_lines = [
            "# region|name|x|z|rotation90|fertilities|fertility_set_values|fertset_markers|"
            "fertarea_markers|randomize_fertilities|mineslot_count|mineslot_raw"
        ]
        matchrows: dict[str, list[str]] = {}
        start_lines: list[str] = ['# region|element_type|x|z  (template elements without an island file: third parties, player starts)']

        for i, (region, guid, session_bin) in enumerate(sessions):
            print(f"Decoding session {i + 1}/{len(sessions)} ({region}) FileDB...")
            session_xml = _filedb_to_xml(fdb_exe, session_bin, timeout=600)
            print(f"Parsing MapTemplate for session {i + 1}/{len(sessions)} ({region})...")
            LAST_STARTS.clear()
            islands = _parse_map_template(session_xml)
            for et, sx, sz in LAST_STARTS:
                start_lines.append(f"{region}|{et}|{sx}|{sz}")
            print(f"  -> {len(islands)} island(s) in {region} (SessionGUID={guid})")

            rows = matchrows.setdefault(region, [])
            for isl in islands:
                fert_str = ','.join(str(v) for v in isl['fertilities'])
                fertset_str = ','.join(str(v) for v in isl['fertility_set_values'])
                fertset_markers_str = ','.join(isl['fertset_markers'])
                fertarea_markers_str = ','.join(isl['fertarea_markers'])
                mineslot_str = ','.join(isl['mineslot_raw'])
                dump_lines.append(
                    f"{region}|{isl['name']}|{isl['x']}|{isl['z']}|{isl['rot']}|"
                    f"{fert_str}|{fertset_str}|{fertset_markers_str}|{fertarea_markers_str}|"
                    f"{isl['randomize_fertilities']}|{len(isl['mineslot_raw']) // 2}|{mineslot_str}"
                )
                rows.append(f"{isl['name']}|{isl['rot']}|{isl['x']}|{isl['z']}")

        (out_dir / 'start_points.txt').write_text(chr(10).join(start_lines), encoding='utf-8')
        dump_path = out_dir / 'map_dump.txt'
        dump_path.write_text('\n'.join(dump_lines), encoding='utf-8')
        print(f"\nWrote {dump_path}")

        for region, rows in matchrows.items():
            match_path = out_dir / f'{region.lower()}_matchrows.txt'
            match_path.write_text('\n'.join(rows), encoding='utf-8')
            print(f"Wrote {match_path}  ({len(rows)} rows, feed into --match-slots / --match-albion-slots)")

    finally:
        if keep_work:
            print(f"Work directory kept at: {work_dir}")
        else:
            shutil.rmtree(work_dir, ignore_errors=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('savegame', type=Path, help='Path to the .a8s savegame file')
    parser.add_argument('--rda', type=Path, default=None, help='Path to RdaConsole.exe (auto-detected if omitted)')
    parser.add_argument('--fdb', type=Path, default=None, help='Path to FileDBReader.exe (auto-detected if omitted)')
    parser.add_argument('--out-dir', type=Path, default=None,
                         help='Output directory (default: tools/dumps/<savegame name>/)')
    parser.add_argument('--keep-work', action='store_true', help='Keep extracted intermediate files for inspection')
    args = parser.parse_args()

    if not args.savegame.is_file():
        print(f"Savegame not found: {args.savegame}", file=sys.stderr)
        return 1

    rda_exe = args.rda or _find_tool(['RdaConsole.exe', 'RDAConsole.exe'])
    fdb_exe = args.fdb or _find_tool(['FileDBReader.exe'])
    if not rda_exe or not fdb_exe:
        missing = [n for n, p in (('RdaConsole', rda_exe), ('FileDBReader', fdb_exe)) if not p]
        print(f"Could not locate: {', '.join(missing)}.", file=sys.stderr)
        print("Pass --rda / --fdb, or put the tools into tools/bin (see README.md).", file=sys.stderr)
        return 1

    out_dir = args.out_dir or (Path(__file__).resolve().parent / 'dumps' / args.savegame.stem)

    try:
        dump_map(args.savegame, rda_exe, fdb_exe, out_dir, args.keep_work)
    except ParseError as exc:
        print(f"Error: {exc}", file=sys.stderr)
        return 1

    return 0


if __name__ == '__main__':
    sys.exit(main())
