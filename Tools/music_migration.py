"""Copy available original MIDI tracks and preserve map music IDs."""

from __future__ import annotations

import json
import re
import shutil
from pathlib import Path

from map_migration import DEMO_MIN_MAP, keep_demo_entries, parse_csm
from npc_visual_migration import ROOT, SOURCE


MAPS = ROOT / "Assets/Resources/AOMigrator/WorldV07/Maps"
CATALOG = ROOT / "Assets/Resources/AOMigrator/AudioV190/map_music.json"
MUSIC = ROOT / "Assets/StreamingAssets/AOMigrator/Music"


def main() -> None:
    source_maps = {
        int(match.group(1)): path
        for path in (SOURCE / "Mapas").glob("*.csm")
        if (match := re.fullmatch(r"mapa([0-9]+)\.csm", path.name,
                                 re.IGNORECASE))
    }
    available = {
        int(path.stem): path
        for path in (SOURCE / "midi").iterdir()
        if path.suffix.lower() == ".mid" and path.stem.isdecimal()
    }
    entries = []
    needed = set()
    for map_path in MAPS.glob("map_*.json"):
        number = int(map_path.stem[4:])
        source_path = source_maps.get(number)
        if source_path is None or number >= DEMO_MIN_MAP:
            continue
        music_id = parse_csm(source_path)["meta"]["music_low"]
        if music_id in available:
            entries.append({"mapNumber": number, "musicId": music_id})
            needed.add(music_id)

    MUSIC.mkdir(parents=True, exist_ok=True)
    for music_id in needed:
        shutil.copy2(available[music_id], MUSIC / f"track_{music_id}.mid")
    CATALOG.parent.mkdir(parents=True, exist_ok=True)
    entries += keep_demo_entries(CATALOG)
    temporary = CATALOG.with_name(CATALOG.name + ".tmp")
    temporary.write_text(
        json.dumps({"maps": sorted(entries, key=lambda row: row["mapNumber"])},
                   separators=(",", ":")),
        encoding="utf-8",
    )
    temporary.replace(CATALOG)
    print(f"Music maps: {len(entries)}, original MIDI tracks: {len(needed)}")


if __name__ == "__main__":
    main()
