"""Corrige `defense` de los NPC en los mapas migrados (bug DEF 0 de npc_entry).

npc_entry leia la clave `defensa`, pero en npcs.dat se llama `DEF`: casi todos
los NPC quedaron con defensa 0. Este script toca SOLO el campo `defense` de
cada NPC de WorldV07/Maps/map_*.json, con el DEF de npcs.dat.

Sin argumentos simula y reporta; con --apply escribe. Solo reescribe un mapa
si al volver a serializarlo sin cambios da los mismos bytes (formato intacto).
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from map_migration import atomic_json, npc_dat

ROOT = Path(__file__).resolve().parent.parent
MAPS = ROOT / "Assets/Resources/AOMigrator/WorldV07/Maps"
NPCS = ROOT / "Archivos Originales/Recursos-master/Recursos-master/Dat/npcs.dat"
SAMPLE = (544, 587, 595, 924, 536, 1001)


def serialize(data: dict) -> bytes:
    return json.dumps(data, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


def original_def(raw: dict[str, str]) -> int:
    try:
        return int(raw.get("def", 0))
    except ValueError:
        return 0


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()

    npcs = npc_dat(NPCS)
    changed_npcs = changed_maps = unchanged = missing = 0
    skipped_format = []
    sample = {}
    for path in sorted(MAPS.glob("map_*.json")):
        raw_bytes = path.read_bytes()
        data = json.loads(raw_bytes)
        if serialize(data) != raw_bytes:
            skipped_format.append(path.name)
            continue
        touched = False
        for npc in data.get("npcs", []):
            raw = npcs.get(npc.get("npcIndex"))
            if raw is None:
                missing += 1
                continue
            new = original_def(raw)
            old = npc.get("defense", 0)
            if old == new:
                unchanged += 1
                continue
            npc["defense"] = new
            changed_npcs += 1
            touched = True
            if npc["npcIndex"] in SAMPLE:
                sample[npc["npcIndex"]] = (npc.get("name"), old, new)
        if touched:
            changed_maps += 1
            if args.apply:
                atomic_json(path, data)
    print(json.dumps({
        "applied": args.apply,
        "npcsChanged": changed_npcs,
        "mapsChanged": changed_maps,
        "npcsAlreadyOk": unchanged,
        "npcsWithoutDat": missing,
        "mapsSkippedFormat": skipped_format,
        "sample": {str(k): {"name": v[0], "old": v[1], "new": v[2]}
                   for k, v in sorted(sample.items())},
    }, ensure_ascii=False, indent=1))


if __name__ == "__main__":
    main()
