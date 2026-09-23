"""Shared helpers of the tools: where things are, how a savegame's settings are read from its file name, how the app is run."""
import os
import re
import subprocess
import tempfile
from dataclasses import dataclass
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent
DUMPS = HERE / "dumps"          # extracted savegames (ignored by git)

TEMPLATES = {"archipelago": "Archipelago", "archipel": "Archipelago", "atoll": "Atoll", "rift": "Rift",
             "corners": "Corners", "islandchains": "IslandChains", "chains": "IslandChains", "ic": "IslandChains"}
SIZES = {"large": "Large", "medium": "Medium", "small": "Small"}
LEVELS = ("abundant", "regular", "sparse")


@dataclass
class Case:
    """The settings a savegame was created with."""
    template: str
    size: str | None
    seed: int
    dlc: str = "on"            # "on", "off" (created without Prophecies of Ash) or "retro" (activated afterwards)
    fertility: str = "abundant"
    slots: str = "abundant"

    def label(self) -> str:
        parts = [self.template, self.size or "?", str(self.seed)]
        if self.dlc != "on":
            parts.append(self.dlc)
        if self.fertility != "abundant":
            parts.append(self.fertility)
        if self.slots != "abundant":
            parts.append("slots-" + self.slots)
        return " ".join(parts)


def parse_case(name: str) -> Case | None:
    """Reads the settings from a file or folder name.

    <Template>_<Size>_<Seed>[_nodlc | _retro][_regular | _sparse][_slots_<abundant|regular|sparse>]
    e.g. Archipelago_Large_1204, Corners_Small_15_nodlc_sparse, Atoll_Large_1_retro. Separators may be anything that is not a
    letter or digit, case does not matter. The size may be left out (it is then found by comparing island counts).
    Returns None when the name has no template or no seed."""
    tokens = [t for t in re.split(r"[^a-z0-9]+", name.lower()) if t]
    template = size = seed = None
    dlc, fertility, slots = "on", "abundant", "abundant"
    index = 0
    while index < len(tokens):
        token = tokens[index]
        if token in TEMPLATES and template is None:
            template = TEMPLATES[token]
        elif token in SIZES and size is None:
            size = SIZES[token]
        elif token in ("nodlc", "dlcoff", "off"):
            dlc = "off"
        elif token in ("retro", "retroactive"):
            dlc = "retro"
        elif token == "slots" and index + 1 < len(tokens) and tokens[index + 1] in LEVELS:
            slots = tokens[index + 1]
            index += 1
        elif token in LEVELS:
            fertility = token
        elif token.isdigit() and seed is None and int(token) > 0:
            seed = int(token)
        index += 1
    if template is None or seed is None:
        return None
    return Case(template, size, seed, dlc, fertility, slots)


def find_exe(explicit: str | None = None) -> Path:
    """The seed finder executable: the explicit path, $ANNO_SEEDFINDER_EXE, or a build in this repository."""
    candidates = [explicit, os.environ.get("ANNO_SEEDFINDER_EXE")]
    for configuration in ("Release", "Debug"):
        candidates.append(str(REPO / "src/Anno117SeedFinder/bin" / configuration / "net10.0-windows/Anno117SeedFinder.exe"))
        candidates.append(str(REPO / "src/Anno117SeedFinder/bin" / configuration / "net10.0-windows/win-x64/Anno117SeedFinder.exe"))
    for candidate in candidates:
        if candidate and Path(candidate).is_file():
            return Path(candidate)
    raise SystemExit("Anno117SeedFinder.exe not found. Build it first (see the README), or pass --exe / set ANNO_SEEDFINDER_EXE.")


def run_exe(exe: Path, arguments: list[str], output: Path, after: tuple[str, ...] = (), env: dict[str, str] | None = None,
            timeout: int = 180) -> list[str]:
    """Runs one dump switch of the app (`arguments`, then the output file, then `after`) and returns the lines it wrote."""
    if output.exists():
        output.unlink()
    environment = dict(os.environ)
    environment.update(env or {})
    subprocess.run([str(exe), *arguments, str(output), *after], env=environment, capture_output=True, timeout=timeout)
    return output.read_text(encoding="utf-8").splitlines() if output.exists() else []


def temp_dir() -> Path:
    return Path(tempfile.mkdtemp(prefix="anno117-tools-"))
