"""Check migrated map JSON, sprite crops, and exit destinations without Unity."""

import json
import re
import struct
import time
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
RESOURCES = ROOT / "Assets" / "Resources" / "AOMigrator" / "WorldV07"
MAPS = RESOURCES / "Maps"
TEXTURES = RESOURCES / "Textures"
OUTPUT = Path(__file__).with_name("local_full_audit.json")


def dimensions(path):
    with path.open("rb") as image:
        header = image.read(24)
    if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"Invalid PNG: {path.name}")
    return struct.unpack(">II", header[16:24])


def run():
    start = time.perf_counter()
    paths = sorted(MAPS.glob("map_*.json"), key=lambda p: int(p.stem[4:]))
    texture_sizes = {
        int(path.stem[4:]): dimensions(path)
        for path in TEXTURES.glob("tex_*.png")
    }
    bounds = {}
    exits = []
    counts = Counter()
    issues = []
    map_rows = []
    used_textures = set()

    def issue(number, kind, detail):
        counts[kind] += 1
        if len(issues) < 200:
            issues.append({"map": number, "kind": kind, "detail": detail})

    def frame(number, spec):
        counts["frames"] += 1
        if not spec:
            issue(number, "null_frame", "null")
            return
        file_num = spec.get("fileNum", 0)
        size = texture_sizes.get(file_num)
        if size is None:
            issue(number, "missing_texture", str(file_num))
            return
        used_textures.add(file_num)
        x, y = spec.get("sx", 0), spec.get("sy", 0)
        width, height = spec.get("width", 0), spec.get("height", 0)
        if (x < 0 or y < 0 or width <= 0 or height <= 0
                or x + width > size[0] or y + height > size[1]):
            issue(number, "invalid_crop",
                  f"tex={file_num} {x},{y} {width}x{height}")

    for path in paths:
        number = int(path.stem[4:])
        try:
            data = json.loads(path.read_text(encoding="utf-8-sig"))
            cells = data["cells"]
            sprites = data["sprites"]
            npcs = data["npcs"]
            objects = data["objects"]
            map_exits = data["exits"]
            minimum_x, maximum_x = data["xmin"], data["xmax"]
            minimum_y, maximum_y = data["ymin"], data["ymax"]
        except (OSError, ValueError, KeyError, TypeError) as error:
            issue(number, "invalid_map", str(error))
            continue
        if data.get("mapNumber") != number:
            issue(number, "wrong_number", str(data.get("mapNumber")))
        if minimum_x > maximum_x or minimum_y > maximum_y:
            issue(number, "invalid_bounds", "inverted")
        bounds[number] = (minimum_x, maximum_x, minimum_y, maximum_y)
        sprite_ids = set()
        for sprite in sprites:
            sprite_id = sprite.get("id")
            if sprite_id in sprite_ids:
                issue(number, "duplicate_sprite", str(sprite_id))
            sprite_ids.add(sprite_id)
            for item in sprite.get("frames") or [sprite]:
                frame(number, item)
        for cell in cells:
            x, y, layer = cell["x"], cell["y"], cell["layer"]
            if (x < minimum_x or x > maximum_x or y < minimum_y
                    or y > maximum_y or layer not in (1, 2, 3, 4)
                    or cell["sprite"] not in sprite_ids):
                issue(number, "invalid_cell",
                      f"{x},{y} layer={layer} sprite={cell['sprite']}")
        for npc in npcs:
            for direction in npc.get("directions") or []:
                for part in ("body", "head", "helmet", "weapon", "shield"):
                    for item in direction.get(part) or []:
                        frame(number, item)
        for obj in objects:
            for item in obj.get("frames") or []:
                frame(number, item)
        exits.extend((number, item) for item in map_exits)
        counts["cells"] += len(cells)
        counts["npcs"] += len(npcs)
        counts["objects"] += len(objects)
        map_rows.append({"number": number, "cells": len(cells),
                         "sprites": len(sprites), "npcs": len(npcs),
                         "objects": len(objects), "exits": len(map_exits)})

    for number, exit_item in exits:
        destination = exit_item["destMap"]
        if destination <= 0:
            counts["special_exits"] += 1
        elif destination not in bounds:
            issue(number, "missing_destination", str(destination))
        else:
            x, y = exit_item["destX"], exit_item["destY"]
            min_x, max_x, min_y, max_y = bounds[destination]
            if not (min_x <= x <= max_x and min_y <= y <= max_y):
                issue(number, "invalid_destination_coordinates",
                      f"map={destination} @ {x},{y}")
            else:
                counts["valid_exits"] += 1

    report = {
        "checked_maps": len(map_rows), "expected_maps": len(paths),
        "available_textures": len(texture_sizes),
        "used_textures": len(used_textures), "exits": len(exits),
        "seconds": round(time.perf_counter() - start, 2),
        "counts": dict(counts), "issues": issues, "maps": map_rows,
    }
    OUTPUT.write_text(json.dumps(report, ensure_ascii=False, indent=2),
                      encoding="utf-8")
    print(json.dumps({key: value for key, value in report.items()
                      if key not in ("issues", "maps")}, ensure_ascii=False))


if __name__ == "__main__":
    run()
