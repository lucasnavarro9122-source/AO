"""Remaster HD de pisos y paredes del dungeon de la demo, piso por piso con Lucas (nube, 25/09).

Reusa ulla_piloto.py (bloques de 128, 4x, Seedream 5 Pro, validación de piezas) con tres diferencias:
- entran las paredes: las piezas con transparencia van sobre fucsia (#FF00FF) y al importar se recortan
  con la silueta original (mismo tamaño y forma);
- el piso no se repite: se generan variantes del mismo juego de 4x4 con los bordes iguales, en una textura
  nueva solo de la demo (tex_9xxxx), que el builder mezcla en el piso;
- los rectángulos negros (vacío, GRH 1) dentro del piso se rellenan con un "abismo" del tema, también en la
  textura nueva. El tex_1 del juego (el negro de todos los mapas) no se toca.

  python Tools/hd_remake/dungeon_hd.py preparar P1 [--salida DIR]   # hojas de entrada + prompts + manifest
  python Tools/hd_remake/dungeon_hd.py costo P1                      # hojas y créditos estimados

Solo lee el juego y escribe en --salida: no gasta créditos ni toca Assets."""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import ulla_piloto as up  # noqa: E402

ROOT = up.ROOT
BLOCK, SCALE = up.BLOCK, up.SCALE
MAGENTA = (255, 0, 255, 255)
CREDITS_PER_SHEET = 2.5   # Seedream 5 Pro, 2k, 3:2 (medido en las zonas de Ullathorpe)
REFS = ROOT / "docs/claude/nube/hd/referencias"

# Por piso: mapa de la demo, texturas propias (piso y paredes), el juego de 4x4 del piso, tema y referencias.
PISOS = {
    "P1": {
        "mapa": 1011, "texturas": [5095], "piso": {"tex": 5095, "sx": 512, "sy": 288}, "textura_nueva": 90001,
        "omitir": [[5095, 4, 3]],   # franja de piso (sale de las variantes) y sombras semitransparentes (sin detalle)
        "referencias": ["p1_idea_hielo.webp", "ref3_bosque_nevado.webp"],
        "tema": ("a FROZEN ICE DUNGEON: blue-grey stone bricks and carved marble covered in frost, thin snow resting on "
                 "the top edges of walls, ledges, balustrades and pillar capitals, small icicles hanging from edges, a few "
                 "small glowing blue ice crystals at the foot of walls. Dark or black areas inside the pieces become deep "
                 "dark-blue frozen mist (a cold abyss), never pure black."),
        "variantes": ["the same floor with fine cracks and frost in the mortar",
                      "the same floor with thin drifts of snow along some joints",
                      "the same floor with a few patches of clear blue ice over the stones",
                      "the same floor with a few tiny blue ice crystals and frozen pebbles"],
        "abismo": "a deep dark-blue frozen abyss seen from above: cold blue mist, faint ice shards far below, no floor",
    },
}


def prompt_piezas(tema: str) -> str:
    return (
        "The FIRST reference image is the source: a sheet of 6 square blocks (3 columns x 2 rows) cut from the texture "
        "atlas of a top-down RPG dungeon: floors, walls, pillars and wall edges. Some neighbouring blocks form one bigger "
        "piece: keep them continuous. Redraw THIS source sheet as high-detail dark-fantasy pixel art, like modern "
        f"souls-like HD pixel-art games, as {tema} Keep EXACTLY the same composition: same pieces in the same "
        "positions, same silhouettes, shapes, edges and proportions; every MAGENTA (#FF00FF) area is transparent "
        "background and must stay flat pure magenta, never painted over. Rich micro-detail (stone grain, cracks, frost "
        "crystals, snow). The other images are STYLE references only: copy their pixel-art rendering, palette and "
        "detail density, but do NOT copy their layout, objects or lighting. Flat neutral lighting: no cast shadows, no "
        "light spots, no vignette, no glow. Orthographic top-down view as in the source. No borders, gaps or text."
    )


def prompt_variantes(tema: str, variantes: list[str], abismo: str) -> str:
    slots = "; ".join(f"block {i + 1}: {v}" for i, v in enumerate(variantes))
    n = len(variantes)
    return (
        "The FIRST reference image is the source: a sheet of 6 square blocks (3 columns x 2 rows) of a top-down RPG "
        f"dungeon floor. Blocks 1 to {n} are the SAME seamless floor tile; redraw each one as high-detail dark-fantasy "
        f"pixel art, like modern souls-like HD pixel-art games, as {tema} Every floor block keeps EXACTLY the same "
        "stone and brick layout, joints and proportions as the source (so they fit together seamlessly), and only the "
        f"surface detail changes: {slots}. Blocks {n + 1} and {n + 2}: {abismo}, seamless. The other images are STYLE "
        "references only: copy their pixel-art rendering, palette and detail density, not their layout. Flat neutral "
        "lighting: no cast shadows, no light spots, no vignette, no glow. Orthographic top-down view. No borders, gaps "
        "or text between blocks."
    )


def componentes(piso: dict) -> list[dict]:
    """Bloques de 128 de las texturas propias que usa el mapa (capas 1-4), unidos si un sprite ocupa varios."""
    m = json.loads((up.MAPS_DIR / f"map_{piso['mapa']}.json").read_text("utf-8-sig"))
    spr = {s["id"]: s for s in m["sprites"]}
    parent, uses = {}, {}

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    for c in m["cells"]:
        s = spr.get(c["sprite"])
        if not s or s["fileNum"] not in piso["texturas"]:
            continue
        blocks = [(s["fileNum"], bx, by) for by in range(s["sy"] // BLOCK, (s["sy"] + s["height"] - 1) // BLOCK + 1)
                  for bx in range(s["sx"] // BLOCK, (s["sx"] + s["width"] - 1) // BLOCK + 1)]
        for b in blocks:
            parent.setdefault(b, b)
            uses[b] = uses.get(b, 0) + 1
        for b in blocks[1:]:
            parent[find(b)] = find(blocks[0])
    comps = {}
    for b in parent:
        comps.setdefault(find(b), set()).add(b)
    out = []
    for c in comps.values():
        f = next(iter(c))[0]
        xs, ys = [b[1] for b in c], [b[2] for b in c]
        out.append({"tex": f, "bx": min(xs), "by": min(ys), "w": max(xs) - min(xs) + 1, "h": max(ys) - min(ys) + 1,
                    "uses": sum(uses[b] for b in c)})
    skip = {tuple(o) for o in piso.get("omitir", [])}
    out = [c for c in out if not any((c["tex"], c["bx"] + i, c["by"] + j) in skip for i in range(c["w"]) for j in range(c["h"]))]
    return sorted(out, key=lambda c: (-(c["w"] * c["h"]), c["tex"], c["by"], c["bx"]))


def sobre_magenta(img: Image.Image) -> Image.Image:
    bg = Image.new("RGBA", img.size, MAGENTA)
    bg.alpha_composite(img.convert("RGBA"))
    return bg.convert("RGB")


def abismo_fuente(seed: int) -> Image.Image:
    """Base oscura azulada con ruido suave: le dice al modelo dónde va el abismo (el negro puro lo copia tal cual)."""
    rng = np.random.default_rng(seed)
    small = rng.normal(0, 1, (8, 8, 1))
    field = np.asarray(Image.fromarray(((small[..., 0] - small.min()) / np.ptp(small) * 255).astype(np.uint8))
                       .resize((BLOCK, BLOCK), Image.BICUBIC), np.float32)[..., None] / 255
    rgb = np.array([8, 14, 32], np.float32) + field * np.array([22, 34, 60], np.float32)
    return Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8))


def preparar(nombre: str, out: Path):
    piso = PISOS[nombre]
    out.mkdir(parents=True, exist_ok=True)
    comps = componentes(piso)
    grandes = [c for c in comps if c["w"] > 3 or c["h"] > 2]
    if grandes:
        print("AVISO: piezas más grandes que 3x2 quedan afuera:", grandes)
    sheets = up.zone_sheets([c for c in comps if c not in grandes])
    for i, sh in enumerate(sheets):
        img = Image.new("RGBA", (3 * BLOCK, 2 * BLOCK), MAGENTA)
        for c in sh["comps"]:
            img.alpha_composite(sobre_magenta(up.source_region(c["tex"], c["bx"], c["by"], c["w"], c["h"])).convert("RGBA"),
                                (c["x"] * BLOCK, c["y"] * BLOCK))
        img.resize((img.width * SCALE, img.height * SCALE), Image.NEAREST).convert("RGB").save(out / f"piezas_{i:02d}_entrada.png")
    # Hoja de variantes: el juego de 4x4 del piso en los primeros bloques, el abismo en los 2 últimos.
    p = piso["piso"]
    src = Image.open(up.TEX / f"tex_{p['tex']}.png").convert("RGBA").crop((p["sx"], p["sy"], p["sx"] + BLOCK, p["sy"] + BLOCK))
    var = Image.new("RGB", (3 * BLOCK, 2 * BLOCK))
    n = len(piso["variantes"])
    for i in range(n):
        var.paste(sobre_magenta(src), ((i % 3) * BLOCK, (i // 3) * BLOCK))
    for j in range(2):
        k = n + j
        var.paste(abismo_fuente(j), ((k % 3) * BLOCK, (k // 3) * BLOCK))
    var.resize((var.width * SCALE, var.height * SCALE), Image.NEAREST).save(out / "variantes_entrada.png")
    (out / "piezas_prompt.txt").write_text(prompt_piezas(piso["tema"]) + "\n", "utf-8")
    (out / "variantes_prompt.txt").write_text(prompt_variantes(piso["tema"], piso["variantes"], piso["abismo"]) + "\n", "utf-8")
    manifest = {"piso": nombre, **{k: piso[k] for k in ("mapa", "texturas", "piso", "textura_nueva", "referencias")},
                "hojas_piezas": sheets, "variantes": n, "abismos": 2}
    (out / "manifest.json").write_text(json.dumps(manifest, indent=1), "utf-8")
    total = len(sheets) + 1
    print(f"{nombre}: {len(sheets)} hojas de piezas ({sum(len(s['comps']) for s in sheets)} piezas) + 1 de variantes "
          f"= {total} hojas, ~{total * CREDITS_PER_SHEET:.1f} créditos con {up.MODEL}. En {out}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("preparar")
    p.add_argument("piso", choices=sorted(PISOS))
    p.add_argument("--salida", type=Path, default=ROOT.parent / "AO_HD/dungeon")
    c = sub.add_parser("costo")
    c.add_argument("piso", choices=sorted(PISOS))
    a = ap.parse_args()
    if a.cmd == "preparar":
        preparar(a.piso, a.salida / a.piso)
    else:
        comps = componentes(PISOS[a.piso])
        n = len(up.zone_sheets([x for x in comps if x["w"] <= 3 and x["h"] <= 2])) + 1
        print(f"{a.piso}: {n} hojas, ~{n * CREDITS_PER_SHEET:.1f} créditos")


if __name__ == "__main__":
    main()
