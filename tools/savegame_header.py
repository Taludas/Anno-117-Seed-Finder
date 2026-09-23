"""Prints the map size, island shift, playable area and enlargement offset that a savegame stores for Latium and Albion.

    python savegame_header.py <savegame.a8s> ...

These values are the exact answer to "how big is this map and where is it": the app's generator must reproduce them
(Size, IslandShift, PlayableArea, EnlargementOffset). Needs RdaConsole and FileDBReader like savegame_map_dump.py.
"""
import re
import shutil
import struct
import subprocess
import sys
from pathlib import Path

import common


def header(savegame: Path) -> dict:
    out = common.DUMPS / "_header_tmp"
    done = subprocess.run([sys.executable, str(common.HERE / "savegame_map_dump.py"), str(savegame), "--out-dir", str(out), "--keep-work"],
                          capture_output=True, text=True, timeout=900)
    found = re.search(r"Work directory kept at: (.*)", done.stdout + done.stderr)
    if not found:
        raise SystemExit(f"Extracting {savegame} failed:\n{done.stdout[-400:]}\n{done.stderr[-800:]}")
    work = Path(found.group(1).strip())
    result = {}
    try:
        for session in sorted(work.glob("session_*.xml")):
            text = session.read_text(encoding="utf-8", errors="ignore")
            start = text.index("<MapTemplate>")
            block = text[start:start + 3000]
            file_name = bytes.fromhex(re.search(r"<Filename>(\w+)</Filename>", text[start:start + 6000]).group(1)).decode("utf-16le").split("/")[-1]

            def integers(tag: str, count: int):
                match = re.search(rf"<{tag}>(\w+)</{tag}>", block)
                return struct.unpack(f"<{count}i", bytes.fromhex(match.group(1))) if match else None

            result["Latium" if file_name.startswith("roman") else "Albion"] = dict(
                template=file_name, size=integers("Size", 2), island_shift=integers("IslandShift", 2),
                playable_area=integers("PlayableArea", 4), enlargement_offset=integers("EnlargementOffset", 2))
    finally:
        shutil.rmtree(work, ignore_errors=True)
        shutil.rmtree(out, ignore_errors=True)
    return result


if __name__ == "__main__":
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    for argument in sys.argv[1:]:
        print(argument)
        for region, values in header(Path(argument)).items():
            print(f"  {region}: " + ", ".join(f"{k}={v}" for k, v in values.items()))
