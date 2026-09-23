"""Audit bytes beyond the CSM records declared in each map header."""

from __future__ import annotations

import hashlib
import json
import struct
from collections import Counter
from pathlib import Path

from map_migration import CSMError, MAP_NAME, Reader


ROOT = Path(__file__).resolve().parents[1]
MAPS = ROOT / "Archivos Originales/Recursos-master/Recursos-master/Mapas"
OUTPUT = ROOT / "MigrationReports/csm_tail_audit.json"


def audit(path: Path) -> dict:
    raw = path.read_bytes()
    reader = Reader(raw)
    counts = reader.take("<11i")
    if any(count < 0 or count > 100000 for count in counts):
        raise CSMError("Conteos CSM inválidos")
    reader.take("<4h")
    reader.string()
    reader.take("<B")
    reader.string()
    reader.take("<iiB")
    for _ in range(3):
        reader.string()
    reader.take("<4i")
    reader.string()
    reader.take("<3B")
    # Exact order and fixed record sizes in ModCargaIAO.bas, GrabarMapaCSM.
    for index, fmt in ((0, "<hhB"), (1, "<hhi"), (2, "<hhi"),
                       (3, "<hhi"), (4, "<hhi"), (5, "<hhh"),
                       (7, "<hhi"), (6, "<hhiB"), (9, "<hhhh"),
                       (8, "<hhh"), (10, "<hhhhh")):
        size = counts[index] * struct.calcsize(fmt)
        if reader.pos + size > len(raw):
            raise CSMError(f"Bloque {index} truncado")
        reader.pos += size
    tail = raw[reader.pos:]
    return {
        "map": int(MAP_NAME.fullmatch(path.name).group(1)),
        "fileBytes": len(raw),
        "declaredEndOffset": reader.pos,
        "unreferencedTailBytes": len(tail),
        "tailSha256": hashlib.sha256(tail).hexdigest() if tail else "",
    }


def main() -> None:
    rows = []
    invalid = []
    for path in sorted(MAPS.glob("mapa*.csm"),
                       key=lambda p: int(MAP_NAME.fullmatch(p.name).group(1))):
        if not raw_size(path):
            invalid.append({"map": int(MAP_NAME.fullmatch(path.name).group(1)),
                            "reason": "empty file"})
            continue
        try:
            rows.append(audit(path))
        except (CSMError, ValueError) as exc:
            invalid.append({"map": int(MAP_NAME.fullmatch(path.name).group(1)),
                            "reason": str(exc)})
    tails = [row for row in rows if row["unreferencedTailBytes"]]
    report = {
        "source": str(MAPS),
        "validMaps": len(rows),
        "invalidMaps": invalid,
        "mapsWithUnreferencedTail": len(tails),
        "totalUnreferencedBytes": sum(r["unreferencedTailBytes"] for r in tails),
        "commonTailLengths": Counter(r["unreferencedTailBytes"] for r in tails).most_common(10),
        "largestTails": sorted(tails, key=lambda r: r["unreferencedTailBytes"],
                               reverse=True)[:10],
        "maps": rows,
    }
    OUTPUT.write_text(json.dumps(report, ensure_ascii=False, indent=2), "utf-8")
    print(f"{len(rows)} mapas válidos; {len(invalid)} inválidos; "
          f"{len(tails)} con cola no referenciada; "
          f"{report['totalUnreferencedBytes']} bytes preservados en origen")


def raw_size(path: Path) -> int:
    return path.stat().st_size


if __name__ == "__main__":
    main()
