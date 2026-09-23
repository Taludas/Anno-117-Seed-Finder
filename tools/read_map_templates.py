"""Prints size-independent facts of the game's map template files: the player start points (element type 2) of every
.a7tinfo in a folder.

    python read_map_templates.py <folder with the game's *.a7tinfo files>

Easy/medium/hard are Large/Medium/Small; default = Archipelago, donut = Atoll, chain = Island Chains; roman_* is Latium and
celtic_* is Albion. The files come out of the game's archives (RDAExplorer) and are not part of this repository. Needs
FileDBReader like savegame_map_dump.py.
"""
import glob
import re
import shutil
import struct
import sys
from pathlib import Path

import savegame_map_dump as dump
import common

if len(sys.argv) != 2:
    raise SystemExit(__doc__)
work = common.temp_dir()
reader = dump._find_tool(["FileDBReader.exe"])
if reader is None:
    raise SystemExit("FileDBReader.exe not found (see README.md).")
for template in sorted(glob.glob(str(Path(sys.argv[1]) / "*.a7tinfo"))):
    copy = work / Path(template).name
    shutil.copy(template, copy)
    text = Path(dump._filedb_to_xml(reader, copy)).read_text(encoding="utf-8")
    starts = []
    for element in re.findall(r"<TemplateElement>(.*?)</TemplateElement>", text, re.S):
        if re.search(r"<ElementType>02000000", element):
            starts.append(struct.unpack("<II", bytes.fromhex(re.search(r"<Position>(\w{16})", element).group(1))))
    print(Path(template).stem, starts)
shutil.rmtree(work, ignore_errors=True)
