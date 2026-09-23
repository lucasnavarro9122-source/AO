"""Build local door interactions from downloaded AO objects and graphics."""

from __future__ import annotations

import json
import shutil
from pathlib import Path

from map_migration import graphics_index, resolve_grh
from npc_visual_migration import ROOT, SOURCE, sections


MAPS = ROOT / "Assets/Resources/AOMigrator/WorldV07/Maps"
TEXTURES = ROOT / "Assets/Resources/AOMigrator/WorldV07/Textures"
CATALOG = ROOT / "Assets/Resources/AOMigrator/WorldV07/door_catalog.json"


def main() -> None:
    source_objects = {
        int(key[3:]): value
        for key, value in sections(SOURCE / "init/localindex.dat").items()
        if key.startswith("obj") and value.get("objtype") == "6"
    }
    open_by_closed_grh: dict[int, int] = {}
    for index, opened in source_objects.items():
        closed = source_objects.get(index + 1, {})
        open_name = opened.get("name", "").lower()
        closed_name = closed.get("name", "").lower()
        if ("abierta" in open_name or "abierto" in open_name) and \
                ("cerrada" in closed_name or "cerrado" in closed_name) and \
                opened.get("grhindex") and closed.get("grhindex"):
            open_by_closed_grh[int(closed["grhindex"])] = int(opened["grhindex"])

    placed: dict[int, int] = {}
    for map_path in sorted(MAPS.glob("map_*.json")):
        data = json.loads(map_path.read_text(encoding="utf-8"))
        for obj in data.get("objects", []):
            if obj.get("objType") == 6:
                placed[obj["objIndex"]] = obj["grhIndex"]

    graphics = graphics_index(SOURCE / "init/graficos.ini")
    result = []
    missing = []
    for index, closed_grh in sorted(placed.items()):
        source = source_objects.get(index)
        if source is None:
            missing.append(index)
            continue
        locked = source.get("llave", "0") not in ("", "0")
        opened_grh = open_by_closed_grh.get(closed_grh)
        if opened_grh is None and not locked:
            # A few doors are already open in the source maps.
            continue
        entry = {"objIndex": index, "locked": locked}
        if not locked:
            frames, _ = resolve_grh(opened_grh, graphics)
            if not frames:
                missing.append(index)
                continue
            frame = frames[0]
            original = SOURCE / "Graficos" / f"{frame['fileNum']}.png"
            if not original.is_file():
                missing.append(index)
                continue
            target = TEXTURES / f"tex_{frame['fileNum']}.png"
            if not target.is_file():
                shutil.copy2(original, target)
            entry["openFrame"] = frame
        result.append(entry)

    if missing:
        raise ValueError(f"Door graphics missing for IDs: {missing}")
    temporary = CATALOG.with_name(CATALOG.name + ".tmp")
    temporary.write_text(
        json.dumps({"doors": result}, ensure_ascii=False, separators=(",", ":")),
        encoding="utf-8",
    )
    temporary.replace(CATALOG)
    print(f"Door definitions: {len(result)} of {len(placed)} placed IDs")


if __name__ == "__main__":
    main()
