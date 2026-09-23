"""Check compatible AO Recursos forks for absent source files; read only."""

from __future__ import annotations

import concurrent.futures
import json
import re
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "MigrationReports/source_gap_forks.json"
HEADERS = {"User-Agent": "AO-Unity-migration-audit"}
MISSING_OBJECTS = {566, 567, 568, 570, 727, 757, 759, 1645}


def fetch(url: str, limit: int = 2_000_000) -> bytes | None:
    try:
        with urllib.request.urlopen(urllib.request.Request(url, headers=HEADERS),
                                    timeout=15) as response:
            data = response.read(limit + 1)
            return data if len(data) <= limit else None
    except (urllib.error.URLError, TimeoutError):
        return None


def inspect(fork: dict) -> dict:
    name, branch = fork["full_name"], fork["default_branch"]
    base = f"https://raw.githubusercontent.com/{name}/{branch}/"
    paths = ["Mapas/mapa844.csm"] + [f"Mapas/mapa{i}.csm" for i in range(3000, 3005)]
    sizes = {}
    for path in paths:
        data = fetch(base + path)
        sizes[path] = len(data) if data is not None else None
    obj = fetch(base + "Dat/obj.dat", 4_000_000)
    ids = set()
    if obj is not None:
        ids = {int(m.group(1)) for m in re.finditer(rb"(?im)^\s*\[OBJ(\d+)\]", obj)}
    return {"repository": name, "branch": branch, "files": sizes,
            "objectFileBytes": len(obj) if obj is not None else None,
            "objectSectionCount": len(ids),
            "missingObjectIdsPresent": sorted(MISSING_OBJECTS & ids)}


def main() -> None:
    raw = fetch("https://api.github.com/repos/ao-org/Recursos/forks?per_page=100",
                1_000_000)
    if raw is None:
        raise RuntimeError("GitHub API unavailable")
    forks = json.loads(raw)[:25]
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        rows = list(pool.map(inspect, forks))
    OUTPUT.write_text(json.dumps({"checked": len(rows), "forks": rows},
                                 ensure_ascii=False, indent=2), "utf-8")
    for row in rows:
        present = {p: s for p, s in row["files"].items() if s and s > 0}
        if present or row["missingObjectIdsPresent"]:
            print(row["repository"], present, row["missingObjectIdsPresent"],
                  row["objectSectionCount"])
    print("forks checked:", len(rows))


if __name__ == "__main__":
    main()
