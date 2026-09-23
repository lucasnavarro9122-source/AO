"""Extract per-map ambient light and weather flags from original CSM maps."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from map_migration import MAP_NAME, parse_csm


ROOT = Path(__file__).resolve().parent.parent
MAPS = ROOT / "Archivos Originales/Recursos-master/Recursos-master/Mapas"
OUTPUT = ROOT / "Assets/Resources/AOMigrator/WorldV07/map_environment.json"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()

    entries = []
    empty = []
    for path in sorted(MAPS.iterdir()):
        if not MAP_NAME.fullmatch(path.name):
            continue
        if path.stat().st_size == 0:
            empty.append(path.name)
            continue
        data = parse_csm(path)
        meta = data["meta"]
        entries.append({"mapNumber": data["number"],
                        "baseLight": meta["base_light"],
                        "rain": bool(meta["rain"]),
                        "snow": bool(meta["snow"]),
                        "fog": bool(meta["fog"])})
    result = {"version": "1.0", "maps": entries}
    if args.apply:
        OUTPUT.write_text(json.dumps(result, ensure_ascii=False,
                                     separators=(",", ":")), "utf-8")
    print(json.dumps({"maps": len(entries), "empty": empty,
                      "applied": args.apply}, ensure_ascii=False))


if __name__ == "__main__":
    main()
