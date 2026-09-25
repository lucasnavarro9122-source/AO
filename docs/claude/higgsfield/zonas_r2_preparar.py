"""Ronda 2 de zonas HD (Higgsfield): arma las hojas desde las pendientes de la ronda 1 sin tocar ulla_piloto.py.

Cambios contra la ronda 1 (Seedream pintaba negro sobre el pasto y corría el color al azul):
  - los huecos vacíos de la hoja se rellenan con copias de piezas de la misma hoja (no hay negro en la entrada);
  - el prompt ya no habla de huecos negros, prohíbe el negro y pide el mismo tono de cada material.
Después se importa igual que siempre: ulla_piloto.py zonas-importar <carpeta> --ronda 2 (sin --aplicar).
"""
import json
import shutil
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "Tools/hd_remake"))
import ulla_piloto as U  # noqa: E402

W = ROOT.parent / "AO_HD/ullathorpe_zonas"
RONDA = 2

PROMPT = (
    "The FIRST reference image is the source: a sheet of 6 square blocks (3 columns x 2 rows) cut from the texture "
    "atlas of a top-down RPG: ground tiles and transition pieces. Some neighbouring blocks form one bigger piece: keep "
    "them continuous. Redraw THIS source sheet as high-detail dark-fantasy pixel art, like modern souls-like HD "
    "pixel-art games, slightly darker and moodier than the source. Keep EXACTLY the same composition and the same "
    "colors: same blocks in the same positions, same shapes, edges, patches and color zones in every block, and the "
    "same hue for each material (olive and yellow-green grass stays olive and yellow-green, never shifted to blue). "
    "Every block is fully covered ground from edge to edge: never add black areas, holes, voids, steps or empty "
    "patches. Rich micro-detail appropriate to each material (grass blades, tiny flowers, pebbles, soil grain). The "
    "other images are STYLE references only: copy their pixel-art rendering and detail density, but do NOT copy their "
    "layout, night lighting, lanterns, glow, fog, objects, buildings or characters. Flat neutral lighting: no cast "
    "shadows, no light spots, no vignette, no glow. Orthographic top-down view, no perspective. No borders, gaps or "
    "text between blocks."
)


def main():
    pend = W / "zonas_pendientes.json"
    backup = W / f"zonas_pendientes_r{RONDA - 1}.json"
    if not backup.exists():
        shutil.copy(pend, backup)
    comps = [{k: c[k] for k in ("tex", "bx", "by", "w", "h", "uses")} for c in json.loads(backup.read_text("utf-8"))]
    sheets = U.zone_sheets(comps)
    B = U.BLOCK
    for i, sh in enumerate(sheets):
        img = Image.new("RGBA", (3 * B, 2 * B), (0, 0, 0, 255))
        busy = set()
        singles = []
        for c in sh["comps"]:
            reg = U.source_region(c["tex"], c["bx"], c["by"], c["w"], c["h"])
            img.alpha_composite(reg, (c["x"] * B, c["y"] * B))
            busy.update((c["x"] + a, c["y"] + b) for a in range(c["w"]) for b in range(c["h"]))
            if c["w"] == 1 and c["h"] == 1:
                singles.append(reg)
        free = [(x, y) for y in range(2) for x in range(3) if (x, y) not in busy]
        for n, (x, y) in enumerate(free):  # relleno: no se importa, solo evita huecos negros
            if singles:
                img.alpha_composite(singles[n % len(singles)], (x * B, y * B))
        img.resize((img.width * U.SCALE, img.height * U.SCALE), Image.NEAREST).convert("RGB").save(
            W / f"r{RONDA}_hoja_{i:02d}_entrada.png")
        print(f"hoja {i:02d}: {len(sh['comps'])} piezas, {len(free)} huecos rellenados, "
              f"tex {sorted({c['tex'] for c in sh['comps']})}")
    (W / f"zonas_manifest_r{RONDA}.json").write_text(json.dumps(sheets, indent=1), "utf-8")
    (W / f"zonas_prompt_r{RONDA}.txt").write_text(PROMPT + "\n", "utf-8")
    print(f"{len(sheets)} hojas, {sum(len(s['comps']) for s in sheets)} piezas en {W}")


if __name__ == "__main__":
    main()
