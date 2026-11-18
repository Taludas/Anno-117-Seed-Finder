#!/usr/bin/env python3
"""
Extract island tile data from Anno 117 .a7m files into data/island_data.json.

Pipeline per island:
  1. RdaConsole extracts gamedata.data from the .a7m archive.
  2. FileDBReader decompresses + interprets gamedata.data to XML using
     the Island_Gamedata.xml format file.
  3. This script parses the XML and classifies each tile using (when
     available) AreaIDs, the Water bitfield, the RiverGrid bitfield,
     and the IrrigationManager byte grid.

Tile byte values in the output:
  0 = TILE_SEA       - open sea / out of bounds
  1 = TILE_LAND      - non-buildable terrain (cliffs, mountains)
  2 = TILE_BUILDABLE - regular buildable land
  3 = TILE_HARBOUR   - buildable coastal water (harbour zone)
  4 = TILE_MARSH     - marsh / irrigation area (buildable)

Reference implementation:
  https://github.com/Just-Chaldea/anno-designer/blob/main/IslandOutlinesExtractor/Program.cs

Usage:
  python extract_islands.py [--island-dir PATH] [--output PATH]
"""

import argparse
import base64
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

# ── Tool locations ────────────────────────────────────────────────────────────
RDA_CONSOLE   = r"C:\tools\RdaConsole.exe"
FILEDB_READER = r"C:\tools\FileDBReader.exe"
INTERPRETER   = r"C:\tools\FileFormats\Island_Gamedata.xml"

# ── Tile categories ───────────────────────────────────────────────────────────
TILE_SEA       = 0   # open sea / off-map
TILE_LAND      = 1   # non-buildable terrain (cliffs, mountains)
TILE_BUILDABLE = 2   # regular buildable land
TILE_HARBOUR   = 3   # buildable coastal water (harbour zone)
TILE_MARSH     = 4   # marsh / irrigation area (buildable)

# AreaID values found in practice.
# 0x0000 = sea, 0x0001 = non-buildable, 0x2001 = buildable,
# 0x4001 = continental-island buildable zone (same rules as 0x2001)
_BUILDABLE_IDS = {0x2001, 0x4001}


# ── XML helpers ───────────────────────────────────────────────────────────────

def _le_uint16(hex_str: str) -> int:
    """Interpret a 2-byte little-endian hex string as an unsigned int.
    Used for fields the V1 interpreter leaves as raw hex (e.g. AreaIDs block x/y)."""
    raw = bytes.fromhex(hex_str.strip().zfill(4))
    return raw[0] | (raw[1] << 8)


def _parse_block_mode(text: str | None) -> int | None:
    """Map block <mode> text to its integer value.
    AreaIDs blocks use numeric text ('1', '2', …).
    m_StaticTileGrid blocks use boolean text ('True'/'False') because the
    V1 interpreter defines the mode field as a boolean."""
    if text is None:
        return None
    t = text.strip()
    if t.lower() == 'true':
        return 1
    if t.lower() == 'false':
        return 0
    try:
        return int(t)
    except ValueError:
        try:
            return int(t, 16)
        except ValueError:
            return None


def _bytes_from_text(text: str) -> bytes | None:
    """Parse raw byte data from FileDBReader XML text.

    Two formats appear in practice:
      • Space-separated decimal bytes  "1 0 255 31 ..."
        → produced when the interpreter knows the field type
      • Contiguous hex string          "0100FF1F..."
        → produced when the interpreter leaves the field as raw binary

    Distinguishing rule: if the text contains a space → decimal; otherwise hex.
    This is unambiguous because a valid hex string never needs spaces, and a
    decimal byte list always has spaces between values (except single-byte
    fields which are just "0"-"255" decimals, identical to their hex value).
    """
    t = text.strip()
    if not t:
        return None
    if ' ' in t:
        try:
            return bytes(int(p) for p in t.split())
        except ValueError:
            return None
    # No spaces → hex string
    try:
        return bytes.fromhex(t)
    except ValueError:
        # Last resort: single decimal integer (e.g. <default>0</default>)
        try:
            v = int(t)
            return bytes([v]) if 0 <= v <= 255 else None
        except ValueError:
            return None


_INVALID_XML_CHARS = re.compile(rb'&#x[0-8B-CE-Fb-ce-f];|&#x1[0-9A-Fa-f];')


def _load_xml(xml_path: Path):
    """Load FileDBReader XML, stripping illegal XML 1.0 control-char entities."""
    raw = _INVALID_XML_CHARS.sub(b'', xml_path.read_bytes())
    return ET.fromstring(raw.decode('utf-8', errors='replace'))


# ── Sparse block parsers ──────────────────────────────────────────────────────

def _parse_area_ids_raw(node, grid_w: int, grid_h: int) -> list:
    """
    Decode the AreaIDs sparse block structure.
    Returns a flat list of raw UInt16 AreaID values (row-major).
    Default value (unspecified tiles) is 0x0000 (sea).

    In the FileDBReader V1 output the x/y and values fields are raw hex
    byte strings (the interpreter doesn't know the types), so we use
    _le_uint16 / bytes.fromhex to decode them.
    """
    ids     = [0] * (grid_w * grid_h)
    block_w = block_h = 16

    for block in node.findall('block'):
        mode_text    = block.findtext('mode')
        x_text       = block.findtext('x')
        y_text       = block.findtext('y')
        values_text  = block.findtext('values')
        default_text = block.findtext('default')

        mode = _parse_block_mode(mode_text)

        if mode is not None:
            if mode == 1:               # dimension / start block
                if x_text: block_w = _le_uint16(x_text)
                if y_text: block_h = _le_uint16(y_text)
            elif mode == 2:             # default-fill block
                # Each mode-2 block carries its own <default> fill value
                fill_val = 0
                if default_text:
                    raw_d = _bytes_from_text(default_text)
                    if raw_d and len(raw_d) >= 2:
                        fill_val = raw_d[0] | (raw_d[1] << 8)
                if fill_val != 0:
                    bx = _le_uint16(x_text) if x_text else 0
                    by = _le_uint16(y_text) if y_text else 0
                    for dy in range(block_h):
                        for dx in range(block_w):
                            tx, ty = bx + dx, by + dy
                            if 0 <= tx < grid_w and 0 <= ty < grid_h:
                                ids[ty * grid_w + tx] = fill_val
            # mode 0 = end block: no action needed
            continue

        # Values block (no mode element)
        if values_text is None:
            continue

        bx  = _le_uint16(x_text) if x_text else 0
        by  = _le_uint16(y_text) if y_text else 0
        raw = _bytes_from_text(values_text)
        if not raw:
            continue
        count = block_w * block_h
        for i in range(count):
            off = i * 2
            if off + 2 > len(raw):
                break
            area_id = raw[off] | (raw[off + 1] << 8)
            tx = bx + (i % block_w)
            ty = by + (i // block_w)
            if 0 <= tx < grid_w and 0 <= ty < grid_h:
                ids[ty * grid_w + tx] = area_id

    return ids


def _parse_byte_blocks(node, grid_w: int, grid_h: int) -> bytearray | None:
    """
    Decode a sparse block structure with 1-byte values (m_StaticTileGrid).
    Like AreaIDs, x/y fields are raw little-endian hex — NOT decimal.
    Returns None if the node is missing.
    """
    if node is None:
        return None
    vals     = bytearray(grid_w * grid_h)
    block_w  = block_h  = 16
    fill_val = 0

    for block in node.findall('block'):
        mode_text    = block.findtext('mode')
        x_text       = block.findtext('x')
        y_text       = block.findtext('y')
        values_text  = block.findtext('values')
        default_text = block.findtext('default')

        mode = _parse_block_mode(mode_text)

        if mode is not None:
            if mode == 1:               # dimension / start block
                if x_text: block_w = _le_uint16(x_text)
                if y_text: block_h = _le_uint16(y_text)
                if default_text:
                    raw_d = _bytes_from_text(default_text)
                    fill_val = raw_d[0] if raw_d else 0
            elif mode == 2:             # default-fill block
                if fill_val != 0:
                    bx = _le_uint16(x_text) if x_text else 0
                    by = _le_uint16(y_text) if y_text else 0
                    for dy in range(block_h):
                        for dx in range(block_w):
                            tx, ty = bx + dx, by + dy
                            if 0 <= tx < grid_w and 0 <= ty < grid_h:
                                vals[ty * grid_w + tx] = fill_val
            continue

        if values_text is None:
            continue

        bx  = _le_uint16(x_text) if x_text else 0
        by  = _le_uint16(y_text) if y_text else 0
        raw = _bytes_from_text(values_text)
        if not raw:
            continue
        count = block_w * block_h
        for i in range(count):
            if i >= len(raw):
                break
            tx = bx + (i % block_w)
            ty = by + (i // block_w)
            if 0 <= tx < grid_w and 0 <= ty < grid_h:
                vals[ty * grid_w + tx] = raw[i]

    return vals


def _parse_bits(node, grid_w: int, grid_h: int,
                invert: bool = False) -> bytearray | None:
    """
    Decode a compact bitfield grid (WorldManager.Water / RiverGrid).

    XML format (from interpreted output):
      <Water>
        <x>512</x>
        <y>512</y>
        <bits>0 0 255 31 ...</bits>   ← space-separated decimal byte values
      </Water>

    C# BitArray layout: bit 0 of tile index j = (byte[j//8] >> (j%8)) & 1.
    With invert=True (Water grid): raw 0 → water=True, raw 1 → dry=True.
    Returns None if the node or data is missing / unparseable.
    """
    if node is None:
        return None

    def _int_child(tag):
        v = node.findtext(tag) or node.get(tag)
        try:
            return int(v.strip()) if v else None
        except ValueError:
            return None

    w = _int_child('x') or grid_w
    h = _int_child('y') or grid_h
    n = w * h

    bits_text = (node.findtext('bits') or node.get('bits') or '').strip()
    if not bits_text:
        return None

    raw = _bytes_from_text(bits_text)
    if not raw:
        return None

    result = bytearray(n)
    for byte_idx, byte_val in enumerate(raw):
        for bit_pos in range(8):
            arr_idx = byte_idx * 8 + bit_pos
            if arr_idx >= n:
                break
            bit = (byte_val >> bit_pos) & 1
            if invert:
                bit ^= 1
            result[arr_idx] = bit

    return result


# ── Tile classification ───────────────────────────────────────────────────────

def _classify_tiles(area_ids: list, water, river, irrig,
                    grid_w: int, grid_h: int) -> bytearray:
    """
    Classify each tile following the C# reference logic:

      AreaID 0x0000             → sea
      AreaID 0x0001 + water     → sea   (deep sea misclassified as non-buildable)
      AreaID 0x0001 + dry       → land  (cliff / mountain)
      AreaID 0x2001/0x4001 + water   → harbour
      AreaID 0x2001/0x4001 + river   → land  (non-buildable river interior)
      AreaID 0x2001/0x4001 + dry     → buildable
      anything else             → land  (unknown, treat conservatively)

    Marsh is a secondary overlay: irrigated buildable tiles that are NOT
    on the outer edge of the buildable region.
    """
    tiles = bytearray(grid_w * grid_h)

    for i in range(grid_w * grid_h):
        aid = area_ids[i]
        is_wet   = bool(water[i])  if water  else False
        is_river = bool(river[i])  if river  else False

        if aid == 0x0000:
            tiles[i] = TILE_SEA
        elif aid == 0x0001:
            tiles[i] = TILE_SEA if is_wet else TILE_LAND
        elif aid in _BUILDABLE_IDS:
            if is_wet:
                tiles[i] = TILE_HARBOUR
            elif is_river:
                tiles[i] = TILE_LAND
            else:
                tiles[i] = TILE_BUILDABLE
        else:
            tiles[i] = TILE_LAND   # unknown AreaID → non-buildable

    # ── Marsh overlay ─────────────────────────────────────────────────────────
    # Marsh = any irrigated buildable tile. Coastal buildable tiles that sit
    # on open water are already classified as HARBOUR by the Water grid, so
    # there is no need to strip a sea-coast border here.
    if irrig is not None:
        for i in range(grid_w * grid_h):
            if tiles[i] == TILE_BUILDABLE and irrig[i]:
                tiles[i] = TILE_MARSH

    return tiles


def _compute_quadrants(tiles: bytearray, grid_w: int, grid_h: int) -> bytearray:
    """
    Compute per-tile quadrant cut masks for diagonal 45° corners.

    Mirrors the C# IslandOutlinesExtractor logic exactly:
      - Every painted (non-sea/harbour) tile bevels against "empty" neighbours.
      - Sea and harbour tiles are treated as "empty canvas" for ALL tile types.
      - Marsh tiles additionally treat adjacent buildable tiles as "empty", so
        the interior marsh→buildable boundary also gets a bevel — but ONLY on the
        marsh side.  Buildable tiles never bevel against marsh, eliminating the
        double-cut that would otherwise create mismatched-colour triangles.
      - A bevel is only assigned when both open neighbours share the same type
        (mixed corners — e.g. north=sea, east=buildable — are left as 90° squares).

    Bit encoding (same as C# AnnoObject.TileQuadrants):
      1=W  2=S  4=E  8=N   (kept inner quarters, i.e. the non-cut half of the tile)
    """
    quads = bytearray(grid_w * grid_h)

    def get_type(tx, ty):
        if tx < 0 or ty < 0 or tx >= grid_w or ty >= grid_h:
            return TILE_SEA
        return tiles[ty * grid_w + tx]

    _coast = (TILE_SEA, TILE_HARBOUR)

    for ty in range(grid_h):
        for tx in range(grid_w):
            t = get_type(tx, ty)
            if t in _coast:
                continue   # sea/harbour are the "empty canvas" — they never bevel

            tn = get_type(tx,     ty - 1)
            ts = get_type(tx,     ty + 1)
            tw = get_type(tx - 1, ty    )
            te = get_type(tx + 1, ty    )

            # "open" = coast for all tiles; buildable is also open for marsh and land tiles.
            # This mirrors C#: marsh and land tiles are "painted" and bevel against
            # unpainted (buildable) neighbours.  Buildable tiles are unpainted so they
            # never bevel back → no double-cut at any interior boundary.
            interior = t in (TILE_MARSH, TILE_LAND)
            n = tn in _coast or (interior and tn == TILE_BUILDABLE)
            s = ts in _coast or (interior and ts == TILE_BUILDABLE)
            w = tw in _coast or (interior and tw == TILE_BUILDABLE)
            e = te in _coast or (interior and te == TILE_BUILDABLE)

            if (n ^ s) and (e ^ w):
                # Only cut when both open sides have the same neighbour type —
                # mixed corners (sea on one side, buildable on the other) are left square.
                if   n and e and tn == te: quads[ty * grid_w + tx] = 0b0011  # NE cut
                elif n and w and tn == tw: quads[ty * grid_w + tx] = 0b0110  # NW cut
                elif s and e and ts == te: quads[ty * grid_w + tx] = 0b1001  # SE cut
                elif s and w and ts == tw: quads[ty * grid_w + tx] = 0b1100  # SW cut

    return quads


# ── Single-island extraction ──────────────────────────────────────────────────

def extract_island(a7m_path: Path, work_dir: Path) -> dict | None:
    island_dir = work_dir / a7m_path.stem
    island_dir.mkdir(parents=True, exist_ok=True)

    # Step 1 – extract gamedata.data from the .a7m archive
    result = subprocess.run(
        [RDA_CONSOLE, 'extract',
         '-f', str(a7m_path),
         '-o', str(island_dir),
         '--filter', 'gamedata',
         '-y', '-n'],
        capture_output=True, text=True,
    )
    gamedata = island_dir / 'gamedata.data'
    if not gamedata.exists():
        print(f"    ERROR: gamedata.data not extracted — {result.stderr[:200]}")
        return None

    # Step 2 – decompress + interpret (run from island_dir so FileDBReader finds the file)
    result = subprocess.run(
        [FILEDB_READER, 'decompress',
         '-f', 'gamedata.data',
         '-i', INTERPRETER,
         '-y'],
        capture_output=True, text=True,
        cwd=str(island_dir),
    )
    xml_path = island_dir / 'gamedata.xml'
    if not xml_path.exists() or xml_path.stat().st_size == 0:
        print(f"    ERROR: gamedata.xml not produced — {result.stderr[:200]}")
        return None

    # Step 3 – parse XML
    try:
        root = _load_xml(xml_path)
    except ET.ParseError as exc:
        print(f"    ERROR: XML parse error — {exc}")
        return None

    # Grid dimensions
    ws_node = root.find('.//WorldSize')
    if ws_node is None or not (ws_node.text or '').strip():
        print("    ERROR: WorldSize not found")
        return None
    parts = ws_node.text.split()
    grid_w, grid_h = int(parts[0]), int(parts[1])

    # Playable area bounding box [x0, y0, x1, y1]
    pa_node = root.find('.//PlayableArea')
    if pa_node is not None and (pa_node.text or '').strip():
        playable = [int(v) for v in pa_node.text.split()]
    else:
        playable = [0, 0, grid_w, grid_h]

    # AreaIDs (always available)
    area_ids_node = root.find('.//AreaIDs')
    if area_ids_node is None:
        print("    ERROR: AreaIDs not found")
        return None
    area_ids = _parse_area_ids_raw(area_ids_node, grid_w, grid_h)

    # Optional grids (may not be present / parseable with the V1 interpreter)
    water_node = root.find('.//WorldManager/Water')
    if water_node is None:
        water_node = root.find('.//Water')
    river_node = root.find('.//WorldManager/RiverGrid')
    if river_node is None:
        river_node = root.find('.//RiverGrid')
    irrig_node = root.find('.//IrrigationManager/m_StaticTileGrid')
    if irrig_node is None:
        irrig_node = root.find('.//m_StaticTileGrid')

    # invert=True: in the raw bits 0=water, 1=dry; after invert: 1=water
    water = _parse_bits(water_node, grid_w, grid_h, invert=True)
    river = _parse_bits(river_node, grid_w, grid_h, invert=False)
    irrig = _parse_byte_blocks(irrig_node, grid_w, grid_h)

    found = []
    if water: found.append('water')
    if river: found.append('river')
    if irrig: found.append('irrigation')
    extra = f"  [{', '.join(found)}]" if found else '  [AreaIDs only]'

    # Step 4 – classify tiles
    tiles = _classify_tiles(area_ids, water, river, irrig, grid_w, grid_h)

    # Stats
    counts = {v: tiles.count(v) for v in (TILE_SEA, TILE_LAND, TILE_BUILDABLE,
                                           TILE_HARBOUR, TILE_MARSH)}
    print(
        f"    {grid_w}x{grid_h}"
        f"  build={counts[TILE_BUILDABLE]}"
        f"  land={counts[TILE_LAND]}"
        f"  harbour={counts[TILE_HARBOUR]}"
        f"  marsh={counts[TILE_MARSH]}"
        f"{extra}"
    )

    unknown_ids = {aid for aid in area_ids
                   if aid not in (0x0000, 0x0001) and aid not in _BUILDABLE_IDS}
    if unknown_ids:
        print(f"    WARN: unknown AreaIDs: {[f'0x{a:04X}' for a in sorted(unknown_ids)]}")

    # Compute 45° diagonal cut masks and pack into tile bytes.
    # Lower nibble = tile type (0-4), upper nibble = quadrant mask (0=full).
    quads = _compute_quadrants(tiles, grid_w, grid_h)
    n_cuts = sum(1 for q in quads if q)
    print(f"    cuts={n_cuts}")
    packed = bytes((quads[i] << 4) | tiles[i] for i in range(grid_w * grid_h))

    return {
        'width':         grid_w,
        'height':        grid_h,
        'playable_area': playable,
        'tiles': base64.b64encode(packed).decode('ascii'),
    }


# ── Main ─────────────────────────────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(description='Extract Anno 117 island outlines.')
    parser.add_argument(
        '--island-dir', default=r'd:\117 mods\anno117-layouttool\island_files',
        help='Directory containing .a7m island files')
    parser.add_argument(
        '--output', default=r'd:\117 mods\anno117-layouttool\data\island_data.json',
        help='Output JSON file path')
    parser.add_argument(
        '--keep-tmp', action='store_true',
        help='Keep the temporary extraction directory for debugging')
    args = parser.parse_args()

    island_dir  = Path(args.island_dir)
    output_path = Path(args.output)

    a7m_files = sorted(island_dir.glob('*.a7m'))
    if not a7m_files:
        print(f"No .a7m files found in {island_dir}")
        sys.exit(1)

    print(f"Found {len(a7m_files)} island file(s) in {island_dir}")
    work_dir = Path(tempfile.mkdtemp(prefix='island_extract_'))
    print(f"Working directory: {work_dir}\n")

    islands: dict = {}
    ok = 0

    try:
        for a7m in a7m_files:
            print(f"  {a7m.name}")
            data = extract_island(a7m, work_dir)
            if data:
                islands[a7m.stem] = data
                ok += 1
            else:
                print(f"    SKIPPED")

        output_path.parent.mkdir(parents=True, exist_ok=True)
        payload = {'version': 3, 'islands': islands}
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(payload, f, separators=(',', ':'))

        size_kb = output_path.stat().st_size // 1024
        print(f"\nDone: {ok}/{len(a7m_files)} islands -> {output_path} ({size_kb} KB)")

    finally:
        if args.keep_tmp:
            print(f"Kept tmp dir: {work_dir}")
        else:
            shutil.rmtree(work_dir, ignore_errors=True)


if __name__ == '__main__':
    main()
