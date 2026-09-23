"""Fill NPC body and head frames from the original AO index files.

The original Personajes.ind is empty in this download. The world editor reads
cuerpos.dat, moldes.ini, cabezas.ini and graficos.ini directly, so use those.
"""

from __future__ import annotations

import argparse
import json
import re
import shutil
import zipfile
from collections import Counter
from pathlib import Path

from PIL import Image

from map_migration import graphics_index, resolve_grh


ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "Archivos Originales/Recursos-master/Recursos-master"
MAPS = ROOT / "Assets/Resources/AOMigrator/WorldV07/Maps"
TEXTURES = ROOT / "Assets/Resources/AOMigrator/WorldV07/Textures"
BACKUP = ROOT / "MigrationReports/backup_before_npc_visuals.zip"
REPORT = ROOT / "MigrationReports/npc_visual_report.json"
MAP_FILE = re.compile(r"map_[0-9]+\.json\Z")
BODY_HEADINGS = (3, 1, 4, 2)  # South, north, west, east in moldes.ini.


def sections(path: Path) -> dict[str, dict[str, str]]:
    result: dict[str, dict[str, str]] = {}
    section = None
    for raw in path.read_text("cp1252").splitlines():
        name = re.fullmatch(r"\s*\[([^]]+)\]\s*", raw)
        if name:
            section = result.setdefault(name.group(1).lower(), {})
        elif section is not None and "=" in raw and not raw.lstrip().startswith("'"):
            key, value = raw.split("=", 1)
            section[key.strip().lower()] = value.split("'", 1)[0].strip()
    return result


def integer(row: dict[str, str], key: str) -> int:
    try:
        return int(row.get(key.lower(), "0"))
    except ValueError:
        return 0


def frame(file_num: int, sx: int, sy: int, width: int, height: int) -> dict:
    return {"fileNum": file_num, "sx": sx, "sy": sy,
            "width": width, "height": height,
            "key": f"f{file_num}_{sx}_{sy}_{width}_{height}"}


def body_frames(body_id: int, bodies: dict, molds: dict, grh: dict) -> tuple[dict, dict]:
    source = bodies.get(f"body{body_id}")
    if not source:
        return {}, {}

    std = integer(source, "std")
    result = {}
    if std:
        mold = molds.get(f"molde{std}")
        file_num = integer(source, "filenum")
        if mold is None or file_num <= 0:
            return {}, source
        sx, sy = integer(mold, "x"), integer(mold, "y")
        width, height = integer(mold, "width"), integer(mold, "height")
        if width <= 0 or height <= 0:
            return {}, source
        for direction, heading in enumerate(BODY_HEADINGS, 1):
            count = integer(mold, f"dir{direction}")
            if count > 0:
                result[heading] = [frame(file_num, sx + i * width, sy,
                                         width, height) for i in range(count)]
            sy += height
    else:
        for heading in range(1, 5):
            grh_id = integer(source, f"walk{heading}")
            if grh_id > 0:
                resolved, _ = resolve_grh(grh_id, grh)
                if resolved:
                    result[heading] = resolved
    return result, source


def head_frames(head_id: int, heads: dict, grh: dict) -> dict:
    source = heads.get(f"head{head_id}")
    if not source:
        return {}
    result = {}
    for heading in range(1, 5):
        grh_id = integer(source, f"head{heading}")
        if grh_id > 0:
            resolved, _ = resolve_grh(grh_id, grh)
            if resolved:
                result[heading] = resolved
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()

    bodies = sections(SOURCE / "init/cuerpos.dat")
    molds = sections(SOURCE / "init/moldes.ini")
    heads = sections(SOURCE / "init/cabezas.ini")
    grh = graphics_index(SOURCE / "init/graficos.ini")
    sizes: dict[int, tuple[int, int] | None] = {}

    def valid(frames: list[dict]) -> bool:
        for spec in frames:
            number = spec["fileNum"]
            if number not in sizes:
                path = SOURCE / "Graficos" / f"{number}.png"
                if path.is_file():
                    with Image.open(path) as image:
                        sizes[number] = image.size
                else:
                    sizes[number] = None
            dimensions = sizes[number]
            if dimensions is None or spec["sx"] < 0 or spec["sy"] < 0 or \
                    spec["width"] <= 0 or spec["height"] <= 0 or \
                    spec["sx"] + spec["width"] > dimensions[0] or \
                    spec["sy"] + spec["height"] > dimensions[1]:
                return False
        return True

    counts = Counter()
    filled_ids: set[int] = set()
    remaining_ids: set[int] = set()
    missing_graphics: set[int] = set()
    copied: set[int] = set()
    changed_maps: list[str] = []

    archive = zipfile.ZipFile(BACKUP, "x", compression=zipfile.ZIP_DEFLATED,
                              compresslevel=3) if args.apply and not BACKUP.exists() else None
    try:
        for path in sorted(MAPS.glob("map_*.json")):
            if not MAP_FILE.fullmatch(path.name):
                continue
            data = json.loads(path.read_text("utf-8"))
            changed = False
            for npc in data.get("npcs", []):
                directions = npc.get("directions") or []
                if not directions:
                    directions = [{"heading": heading, "body": [], "head": [],
                                   "helmet": [], "weapon": [], "shield": []}
                                  for heading in range(1, 5)]
                    npc["directions"] = directions
                by_heading = {part["heading"]: part for part in directions}
                npc_id = npc["npcIndex"]

                if not any(part.get("body") for part in directions):
                    body, source = body_frames(npc.get("body", 0), bodies, molds, grh)
                    if body and all(valid(frames) for frames in body.values()):
                        for heading, frames in body.items():
                            by_heading[heading]["body"] = frames
                            copied.update(spec["fileNum"] for spec in frames)
                        npc["headOffsetX"] = integer(source, "headoffsetx")
                        npc["headOffsetY"] = integer(source, "headoffsety")
                        counts["body_placements_filled"] += 1
                        filled_ids.add(npc_id)
                        changed = True
                    else:
                        remaining_ids.add(npc_id)
                        for frames in body.values():
                            missing_graphics.update(
                                spec["fileNum"] for spec in frames
                                if sizes.get(spec["fileNum"]) is None)

                if npc.get("head", 0) > 0 and \
                        not any(part.get("head") for part in directions):
                    head = head_frames(npc["head"], heads, grh)
                    if head and all(valid(frames) for frames in head.values()):
                        for heading, frames in head.items():
                            by_heading[heading]["head"] = frames
                            copied.update(spec["fileNum"] for spec in frames)
                        counts["head_placements_filled"] += 1
                        changed = True

            if changed:
                changed_maps.append(path.name)
                if args.apply:
                    if archive is not None:
                        archive.write(path, path.name)
                    temporary = path.with_name(path.name + ".writing")
                    temporary.write_text(json.dumps(data, ensure_ascii=False,
                                                    separators=(",", ":")), "utf-8")
                    temporary.replace(path)
    finally:
        if archive is not None:
            archive.close()

    if args.apply:
        TEXTURES.mkdir(parents=True, exist_ok=True)
        for number in sorted(copied):
            target = TEXTURES / f"tex_{number}.png"
            if not target.exists():
                shutil.copy2(SOURCE / "Graficos" / f"{number}.png", target)
                counts["textures_copied"] += 1

    report = {"applied": args.apply, "maps_changed": len(changed_maps),
              "npc_indices_filled": len(filled_ids),
              "npc_indices_remaining": len(remaining_ids),
              "remaining_indices": sorted(remaining_ids),
              "missing_graphics": sorted(missing_graphics),
              "textures_needed": len(copied), **counts}
    if args.apply:
        REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2), "utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
